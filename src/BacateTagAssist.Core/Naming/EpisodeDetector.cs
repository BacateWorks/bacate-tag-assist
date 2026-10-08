using System.Text.RegularExpressions;

namespace BacateTagAssist.Core.Naming;

/// <summary>
/// Lê o identificador de temporada/episódio no nome atual do arquivo.
/// Retorna "S01E01", "S01E05-E06", "E01" (sem temporada) ou null.
/// Não consulta TVDB: é apenas leitura do nome.
/// </summary>
public static partial class EpisodeDetector
{
    private static readonly HashSet<string> SubtitleLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "pt", "pt-BR", "pt-PT", "por", "pob", "en", "en-US", "en-GB", "eng", "es", "es-419", "es-ES", "spa",
        "ja", "jpn", "fr", "fre", "fra", "de", "ger", "deu", "it", "ita", "ko", "kor", "zh", "zh-CN", "zh-TW",
        "chi", "zho", "ru", "rus", "ar", "ara", "pl", "pol", "nl", "dut", "nld", "tr", "tur", "sv", "swe",
        "und", "forced", "sdh", "cc",
    };

    /// <summary>Detecta o identificador a partir do nome do arquivo (com ou sem extensão).</summary>
    public static string? Detect(string fileNameWithoutExtension)
    {
        var name = fileNameWithoutExtension ?? "";

        // 1) S01E01, S1E1, S01E05-E06, S01E05E06
        var m = SxxEyy().Match(name);
        if (m.Success)
        {
            var season = Pad(m.Groups["s"].Value);
            return season + Episode(m.Groups["e"].Value, m.Groups["e2"].Value);
        }

        // 2) 1x01
        m = NxNN().Match(name);
        if (m.Success)
            return Pad(m.Groups["s"].Value) + Episode(m.Groups["e"].Value, m.Groups["e2"].Value);

        // 3) EP 1, E1, Episode 1, Episodio 1, Episódio 1, EP 5-6
        m = Labeled().Match(name);
        if (m.Success) return Episode(m.Groups["e"].Value, m.Groups["e2"].Value);

        // 4) Nome apenas numérico: 1, 01, 100, 5-6
        m = OnlyNumber().Match(name);
        if (m.Success) return Episode(m.Groups["e"].Value, m.Groups["e2"].Value);

        // 5) Padrão comum de anime: "[Grupo] Série - 05 [1080p]"
        m = AnimeDash().Match(name);
        if (m.Success) return Episode(m.Groups["e"].Value, "");

        // 6) Começa com número seguido de separador: "01 - Piloto"
        m = LeadingNumber().Match(name);
        if (m.Success) return Episode(m.Groups["e"].Value, "");

        return null;
    }

    /// <summary>Extrai sufixo de idioma de legendas (ex.: "1.pt-BR" → ("1", "pt-BR")).</summary>
    public static (string Stem, string? Suffix) SplitSubtitleSuffix(string nameWithoutExtension)
    {
        var name = nameWithoutExtension ?? "";
        var parts = name.Split('.');
        if (parts.Length < 2) return (name, null);

        var suffix = new List<string>();
        var i = parts.Length - 1;
        while (i > 0 && suffix.Count < 2 && SubtitleLanguageCodes.Contains(parts[i]))
        {
            suffix.Insert(0, parts[i]);
            i--;
        }
        if (suffix.Count == 0) return (name, null);
        return (string.Join('.', parts.Take(i + 1)), string.Join('.', suffix));
    }

    /// <summary>Temporada S01 contida em um identificador, se houver.</summary>
    public static string? SeasonOf(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var m = SeasonPrefix().Match(id);
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    /// <summary>Tenta achar uma temporada no nome de pasta: "Season 2", "Temporada 02", "S02".</summary>
    public static string? SeasonFromFolder(string folderName)
    {
        var m = FolderSeason().Match(folderName ?? "");
        return m.Success ? Pad(m.Groups["s"].Value) : null;
    }

    private static string Pad(string season) => "S" + int.Parse(season).ToString("00");

    private static string Episode(string first, string second)
    {
        var a = "E" + Num(first);
        if (string.IsNullOrEmpty(second)) return a;
        var b = Num(second);
        return int.Parse(second) > int.Parse(first) ? $"{a}-E{b}" : a;
    }

    private static string Num(string value) => int.Parse(value).ToString(value.TrimStart('0').Length >= 3 ? "000" : "00");

    [GeneratedRegex(@"(?<![A-Za-z0-9])S(?<s>\d{1,2})[ ._-]?E(?<e>\d{1,3})(?:-?E(?<e2>\d{1,3}))?(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex SxxEyy();

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?<s>\d{1,2})x(?<e>\d{2,3})(?:-(?:\d{1,2}x)?(?<e2>\d{2,3}))?(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex NxNN();

    [GeneratedRegex(@"(?<![A-Za-z])(?:Epis[oó]dio|Episode|Ep|E)[ ._-]*(?<e>\d{1,3})(?:[ ]*-[ ]*(?:E|Ep)?(?<e2>\d{1,3}))?(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex Labeled();

    [GeneratedRegex(@"^\s*(?<e>\d{1,3})(?:\s*-\s*(?<e2>\d{1,3}))?\s*$")]
    private static partial Regex OnlyNumber();

    [GeneratedRegex(@"\s-\s(?<e>\d{1,3})(?:v\d)?(?=\s|\[|\(|$|\.)")]
    private static partial Regex AnimeDash();

    [GeneratedRegex(@"^(?<e>\d{1,3})(?=\s*[-_.]\s*\D|\s+\D)")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^S\d{2}", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonPrefix();

    [GeneratedRegex(@"(?:(?<![A-Za-z])S|Season[ ._-]*|Temporada[ ._-]*)(?<s>\d{1,2})(?!\d)(?![ ._-]?E\d)", RegexOptions.IgnoreCase)]
    private static partial Regex FolderSeason();
}
