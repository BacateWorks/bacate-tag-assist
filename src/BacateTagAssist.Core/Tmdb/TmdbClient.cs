using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using BacateTagAssist.Core.Config;

namespace BacateTagAssist.Core.Tmdb;

public sealed record TmdbResult(int Id, string Type, string Title, string? OriginalTitle, string? Year, string? Poster, string? Backdrop, string? Overview, double Vote);

public sealed record TmdbSearch(IReadOnlyList<TmdbResult> Results, bool Direct);

public sealed record TmdbSeason(int Number, string Name, int Episodes, string? Year, string? Poster);

public sealed record TmdbTitles(string En, string Original, string? Pt, string OriginalLanguage);

public sealed record TmdbDetails(
    int Id, string Type, TmdbTitles Titles, string? Year, string? Poster, string? Backdrop, string? Overview,
    string? ImdbId, IReadOnlyList<TmdbSeason> Seasons, IReadOnlyList<string> Genres, int? Runtime, string Url);

public sealed class TmdbException(string message, HttpStatusCode status = HttpStatusCode.BadGateway) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>Cliente mínimo da API v3 do TMDB. Aceita chave v3 (api_key) ou token de leitura v4 (Bearer).</summary>
public sealed partial class TmdbClient(SettingsStore settings)
{
    private const string Base = "https://api.themoviedb.org/3/";
    private const string Images = "https://image.tmdb.org/t/p/";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public bool Configured => settings.TmdbKey().Key is not null;

    public async Task<(bool Ok, string Message)> TestAsync(string? key = null, CancellationToken ct = default)
    {
        try
        {
            using var doc = await GetAsync("configuration", new(), ct, key);
            return (true, "Conexão com o TMDB funcionando.");
        }
        catch (TmdbException ex) { return (false, ex.Message); }
    }

    public async Task<TmdbSearch> SearchAsync(string query, string type, CancellationToken ct = default)
    {
        query = (query ?? "").Trim();
        if (query.Length == 0) return new TmdbSearch([], false);
        type = type == "tv" ? "tv" : "movie";

        // Link do TMDB: themoviedb.org/movie/603-the-matrix  |  /tv/1396
        var link = TmdbLink().Match(query);
        if (link.Success)
        {
            var d = await DetailsAsync(link.Groups["type"].Value, int.Parse(link.Groups["id"].Value), ct);
            return new TmdbSearch([ToResult(d)], true);
        }

        // IMDb: tt1234567 ou link do IMDb
        var imdb = ImdbId().Match(query);
        if (imdb.Success)
        {
            using var found = await GetAsync($"find/{imdb.Value}", new() { ["external_source"] = "imdb_id", ["language"] = Lang }, ct);
            var results = new List<TmdbResult>();
            results.AddRange(ReadResults(found.RootElement, "movie_results", "movie"));
            results.AddRange(ReadResults(found.RootElement, "tv_results", "tv"));
            return new TmdbSearch(results, true);
        }

        // ID numérico do TMDB ("603", "tmdb:603", "tv:1396", "movie:603").
        // Sem prefixo, também fazemos a busca textual (há filmes chamados "1917", "2012"...).
        TmdbResult? direct = null;
        var numeric = NumericId().Match(query);
        if (numeric.Success)
        {
            var prefix = numeric.Groups["type"].Success ? numeric.Groups["type"].Value.ToLowerInvariant() : null;
            var t = prefix is "tv" or "movie" ? prefix : type;
            try
            {
                direct = ToResult(await DetailsAsync(t, int.Parse(numeric.Groups["id"].Value), ct));
                if (prefix is not null) return new TmdbSearch([direct], true);
            }
            catch (TmdbException ex) when (ex.Status == HttpStatusCode.NotFound) { /* cai na busca textual */ }
        }

        var parameters = new Dictionary<string, string> { ["query"] = query, ["language"] = Lang, ["include_adult"] = "false" };
        var yearMatch = TrailingYear().Match(query);
        if (yearMatch.Success)
        {
            parameters["query"] = query[..yearMatch.Index].Trim();
            parameters[type == "tv" ? "first_air_date_year" : "year"] = yearMatch.Groups[1].Value;
        }
        using var doc = await GetAsync($"search/{type}", parameters, ct);
        var list = ReadResults(doc.RootElement, "results", type).Take(12).ToList();
        if (direct is not null)
        {
            list.RemoveAll(x => x.Id == direct.Id && x.Type == direct.Type);
            list.Insert(0, direct);
        }
        return new TmdbSearch(list, false);
    }

    public async Task<TmdbDetails> DetailsAsync(string type, int id, CancellationToken ct = default)
    {
        type = type == "tv" ? "tv" : "movie";
        using var doc = await GetAsync($"{type}/{id}", new() { ["language"] = "en-US", ["append_to_response"] = "translations,external_ids" }, ct);
        var r = doc.RootElement;

        var en = S(r, type == "tv" ? "name" : "title") ?? "";
        var original = S(r, type == "tv" ? "original_name" : "original_title") ?? en;
        var originalLanguage = S(r, "original_language") ?? "";
        string? pt = null, overviewPt = null;
        if (r.TryGetProperty("translations", out var tr) && tr.TryGetProperty("translations", out var list))
        {
            foreach (var item in list.EnumerateArray())
            {
                if (S(item, "iso_639_1") != "pt") continue;
                var data = item.GetProperty("data");
                var name = S(data, type == "tv" ? "name" : "title");
                var ov = S(data, "overview");
                if (S(item, "iso_3166_1") == "BR" || pt is null)
                {
                    if (!string.IsNullOrWhiteSpace(name)) pt = name;
                    if (!string.IsNullOrWhiteSpace(ov)) overviewPt = ov;
                }
            }
        }

        string? imdbId = S(r, "imdb_id");
        if (imdbId is null && r.TryGetProperty("external_ids", out var ext)) imdbId = S(ext, "imdb_id");

        var seasons = new List<TmdbSeason>();
        if (type == "tv" && r.TryGetProperty("seasons", out var ss))
        {
            foreach (var s in ss.EnumerateArray())
            {
                seasons.Add(new TmdbSeason(
                    s.TryGetProperty("season_number", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0,
                    S(s, "name") ?? "",
                    s.TryGetProperty("episode_count", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 0,
                    Year(S(s, "air_date")),
                    Img(S(s, "poster_path"), "w185")));
            }
        }

        var genres = r.TryGetProperty("genres", out var g) ? g.EnumerateArray().Select(x => S(x, "name") ?? "").Where(x => x.Length > 0).ToList() : [];
        int? runtime = r.TryGetProperty("runtime", out var rt) && rt.ValueKind == JsonValueKind.Number ? rt.GetInt32() : null;

        return new TmdbDetails(
            id, type, new TmdbTitles(en, original, pt, originalLanguage),
            Year(S(r, type == "tv" ? "first_air_date" : "release_date")),
            Img(S(r, "poster_path"), "w342"), Img(S(r, "backdrop_path"), "w1280"),
            overviewPt ?? S(r, "overview"), imdbId, seasons, genres, runtime,
            $"https://www.themoviedb.org/{type}/{id}");
    }

    private string Lang => string.IsNullOrWhiteSpace(settings.Current.TmdbLanguage) ? "pt-BR" : settings.Current.TmdbLanguage;

    private static TmdbResult ToResult(TmdbDetails d) =>
        new(d.Id, d.Type, d.Titles.Pt ?? d.Titles.En, d.Titles.Original, d.Year, d.Poster, d.Backdrop, d.Overview, 0);

    private static IEnumerable<TmdbResult> ReadResults(JsonElement root, string prop, string type)
    {
        if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
        foreach (var r in arr.EnumerateArray())
        {
            yield return new TmdbResult(
                r.GetProperty("id").GetInt32(), type,
                S(r, type == "tv" ? "name" : "title") ?? "",
                S(r, type == "tv" ? "original_name" : "original_title"),
                Year(S(r, type == "tv" ? "first_air_date" : "release_date")),
                Img(S(r, "poster_path"), "w342"), Img(S(r, "backdrop_path"), "w780"),
                S(r, "overview"),
                r.TryGetProperty("vote_average", out var v) && v.ValueKind == JsonValueKind.Number ? Math.Round(v.GetDouble(), 1) : 0);
        }
    }

    private async Task<JsonDocument> GetAsync(string path, Dictionary<string, string> query, CancellationToken ct, string? overrideKey = null)
    {
        var key = overrideKey ?? settings.TmdbKey().Key;
        if (string.IsNullOrWhiteSpace(key)) throw new TmdbException("Configure sua chave do TMDB nas configurações.", HttpStatusCode.PreconditionRequired);
        key = key.Trim();

        var bearer = key.StartsWith("eyJ", StringComparison.Ordinal) && key.Length > 60;
        if (!bearer) query["api_key"] = key;
        var qs = string.Join('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}{path}?{qs}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (bearer) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        HttpResponseMessage response;
        try { response = await Http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new TmdbException("Não foi possível conectar ao TMDB. Verifique sua internet.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new TmdbException("Chave do TMDB inválida.", HttpStatusCode.Unauthorized);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new TmdbException("Título não encontrado no TMDB.", HttpStatusCode.NotFound);
            if ((int)response.StatusCode == 429) throw new TmdbException("Muitas requisições ao TMDB; aguarde alguns segundos.", HttpStatusCode.TooManyRequests);
            if (!response.IsSuccessStatusCode) throw new TmdbException($"O TMDB respondeu {(int)response.StatusCode}.");
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }

    private static string? S(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private static string? Year(string? date) => date is { Length: >= 4 } ? date[..4] : null;
    private static string? Img(string? path, string size) => path is null ? null : Images + size + path;

    [GeneratedRegex(@"themoviedb\.org/(?<type>movie|tv)/(?<id>\d+)", RegexOptions.IgnoreCase)] private static partial Regex TmdbLink();
    [GeneratedRegex(@"tt\d{5,10}", RegexOptions.IgnoreCase)] private static partial Regex ImdbId();
    [GeneratedRegex(@"^(?:(?<type>tv|movie|tmdb)[:/ ])?(?<id>\d{1,9})$", RegexOptions.IgnoreCase)] private static partial Regex NumericId();
    [GeneratedRegex(@"\s\(?((?:19|20)\d{2})\)?$")] private static partial Regex TrailingYear();
}
