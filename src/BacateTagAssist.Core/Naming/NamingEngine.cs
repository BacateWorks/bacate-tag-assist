using System.Text;
using System.Text.RegularExpressions;

namespace BacateTagAssist.Core.Naming;

/// <summary>
/// Monta nomes de release seguindo o guia de nomenclatura do CapybaraBR
/// (também usado pelo Samaritano e por outros trackers com o mesmo padrão).
/// A ordem dos tokens é exatamente a mesma da versão 0.2 do BacateTagAssist.
/// </summary>
public static partial class NamingEngine
{
    private static readonly string[] PhysicalFullSources = ["Blu-Ray", "UHD Blu-Ray", "DVD5", "DVD9"];
    private static readonly string[] DvdSources = ["DVD", "DVD5", "DVD9"];
    private static readonly string[] DiscCountSources = ["DVD5", "DVD9"];
    private static readonly string[] WebSources = ["WEB-DL", "WEBRip"];
    private static readonly string[] TvSources = ["HDTV", "PDTV", "TVRip"];

    public static bool IsWebSource(string source) => WebSources.Contains(source);
    public static bool IsDvdSource(string source) => DvdSources.Contains(source);
    public static bool IsDiscCountSource(string source) => DiscCountSources.Contains(source);
    public static bool IsTvSource(string source) => TvSources.Contains(source);
    public static bool IsPhysicalFull(ReleaseFields v) => v.ReleaseType == "normal" && PhysicalFullSources.Contains(v.Source);

    /// <summary>Remove caracteres que os guias não aceitam no título.</summary>
    public static string CleanTitle(string value)
    {
        var text = (value ?? "").Replace("&", " and ");
        text = TitleForbidden().Replace(text, " ");
        return Whitespace().Replace(text, " ").Trim();
    }

    /// <summary>Monta o nome base (sem extensão) para um identificador (S01, S01E01 ou vazio).</summary>
    public static BuiltName Build(ReleaseFields fields, NamingOptions options, string? identifier)
    {
        var v = fields.Normalized();
        var tokens = new List<NameToken>();

        void Add(string? text, string kind, string field, string sep = " ")
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            tokens.Add(new NameToken(Whitespace().Replace(text.Trim(), " "), kind, field, sep));
        }

        Add(CleanTitle(v.Title), "title", "title");
        Add(v.Year, "year", "year");
        if (!string.IsNullOrWhiteSpace(identifier))
            Add(identifier.ToUpperInvariant(), "id", options.Mode == ReleaseMode.Episode ? "episode" : "season");
        Add(v.Edition, "edition", "edition");
        if (v.Special == "extras") Add("Extras", "special", "special");
        if (v.Special == "bonus") Add("Bonus Disc", "special", "special");
        if (v.Repack) Add("REPACK", "repack", "repack");

        AddTechnical(v, Add);
        AddCredits(v, options, Add);

        var raw = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (i > 0) raw.Append(tokens[i].Sep);
            raw.Append(tokens[i].Text);
        }

        var name = Finish(raw.ToString(), options.Dots);
        var display = tokens.Select(t => t with { Text = Finish(t.Text, options.Dots), Sep = options.Dots && t.Sep == " " ? "." : t.Sep }).ToList();
        return new BuiltName(name, display);
    }

    private static void AddTechnical(ReleaseFields v, Action<string?, string, string, string> add)
    {
        var src = v.Source;
        var isPhysicalFull = IsPhysicalFull(v);
        var isRemux = v.ReleaseType == "remux";
        var isWeb = IsWebSource(src);
        var isTv = IsTvSource(src);
        var streaming = v.EffectiveStreaming;

        if (v.Hybrid) add("Hybrid", "hybrid", "hybrid", " ");
        if (v.Resolution.Length > 0 && !IsDvdSource(src)) add(v.Resolution, "resolution", "resolution", " ");
        if (v.Region.Length > 0) add(v.Region.ToUpperInvariant(), "region", "region", " ");
        if (v.DiscCount.Length > 0 && IsDiscCountSource(src))
        {
            var count = TrailingX().Replace(v.DiscCount, "");
            add(count + "x" + src, "disc", "discCount", " ");
        }
        else if (src.Length > 0)
        {
            if (isWeb && streaming.Length > 0) add(streaming, "streaming", "streaming", " ");
            add(src, "source", "source", " ");
        }

        var audioHasSpace = v.AudioCodec.Contains(' ');
        var audio = v.AudioCodec.Length > 0 ? v.AudioCodec + (v.Channels.Length > 0 && !audioHasSpace ? v.Channels : "") : "";
        var separateChannels = v.AudioCodec.Length > 0 && v.Channels.Length > 0 && audioHasSpace ? v.Channels : "";

        if (isPhysicalFull || isRemux)
        {
            if (isRemux) add("REMUX", "remux", "releaseType", " ");
            add(v.Range, "range", "range", " ");
            if (v.Hfr) add("HFR", "hfr", "hfr", " ");
            if (v.VideoCodec.Length > 0 && !(IsDvdSource(src) && isRemux)) add(v.VideoCodec, "video", "videoCodec", " ");
            add(audio, "audio", "audioCodec", " ");
            add(separateChannels, "channels", "channels", " ");
            if (v.Atmos) add("Atmos", "atmos", "atmos", " ");
        }
        else
        {
            if (v.Hfr && isTv) add("HFR", "hfr", "hfr", " ");
            add(audio, "audio", "audioCodec", " ");
            add(separateChannels, "channels", "channels", " ");
            if (v.Atmos) add("Atmos", "atmos", "atmos", " ");
            add(v.Range, "range", "range", " ");
            if (v.Hfr && !isTv) add("HFR", "hfr", "hfr", " ");
            add(v.VideoCodec, "video", "videoCodec", " ");
        }
    }

    private static void AddCredits(ReleaseFields v, NamingOptions options, Action<string?, string, string, string> add)
    {
        if (v.DualMulti.Length > 0 && options.TwoGroups && v.OriginalGroup.Length > 0)
        {
            add(v.OriginalGroup, "origGroup", "originalGroup", "-");
            add(v.DualMulti, "lang", "dualMulti", " ");
        }
        else if (v.DualMulti.Length > 0)
        {
            add(v.DualMulti, "lang", "dualMulti", " ");
        }

        if (v.Group.Length > 0) add(v.Group, "group", "group", "-");
        else add("NoGroup", "nogroup", "group", "-");
    }

    /// <summary>Aplica pontos (se habilitado) e remove caracteres inválidos em nomes de arquivo.</summary>
    public static string Finish(string text, bool dots)
    {
        var value = Whitespace().Replace(text ?? "", " ").Trim();
        if (dots)
        {
            value = value.Replace(' ', '.');
            value = MultiDots().Replace(value, ".");
        }
        value = InvalidFileChars().Replace(value, "");
        return value.TrimEnd('.', ' ');
    }

    /// <summary>Identificador de episódio final para um arquivo (S01E01, S01E05-E06...).</summary>
    public static string EpisodeId(string? detected, ReleaseFields fields, ReleaseMode mode)
    {
        if (mode == ReleaseMode.Movie) return "";
        var v = fields.Normalized();
        var found = detected ?? "";

        string Combine(string value)
        {
            if (FullEpisodeId().IsMatch(value)) return value.ToUpperInvariant();
            if (BareEpisodeId().IsMatch(value) && SeasonId().IsMatch(v.Season)) return v.Season.ToUpperInvariant() + value.ToUpperInvariant();
            return "";
        }

        if (mode == ReleaseMode.Episode)
            return Combine(v.Episode.Length > 0 ? v.Episode : found);
        if (found.Length > 0 && HasEpisode().IsMatch(found)) return Combine(found);
        return "";
    }

    /// <summary>Identificador usado no nome da pasta principal.</summary>
    public static string FolderIdentifier(ReleaseFields fields, ReleaseMode mode, string? firstDetected)
    {
        var v = fields.Normalized();
        return mode switch
        {
            ReleaseMode.Movie => "",
            ReleaseMode.Season => v.Season.ToUpperInvariant(),
            _ => EpisodeId(firstDetected, v, ReleaseMode.Episode) is { Length: > 0 } id ? id : v.Episode.ToUpperInvariant(),
        };
    }

    public static bool IsValidSeason(string value) => SeasonId().IsMatch((value ?? "").Trim());
    public static bool IsValidEpisode(string value) => FullEpisodeId().IsMatch((value ?? "").Trim());

    [GeneratedRegex(@"[*!:;?""'()/\\|<>]")] private static partial Regex TitleForbidden();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"\.{2,}")] private static partial Regex MultiDots();
    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")] private static partial Regex InvalidFileChars();
    [GeneratedRegex(@"x$", RegexOptions.IgnoreCase)] private static partial Regex TrailingX();
    [GeneratedRegex(@"^S\d{2}E\d{2,3}(?:-E\d{2,3})?$", RegexOptions.IgnoreCase)] private static partial Regex FullEpisodeId();
    [GeneratedRegex(@"^E\d{2,3}(?:-E\d{2,3})?$", RegexOptions.IgnoreCase)] private static partial Regex BareEpisodeId();
    [GeneratedRegex(@"^S\d{2}$", RegexOptions.IgnoreCase)] private static partial Regex SeasonId();
    [GeneratedRegex(@"E\d{2}", RegexOptions.IgnoreCase)] private static partial Regex HasEpisode();
}
