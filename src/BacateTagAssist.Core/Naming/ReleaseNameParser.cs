using System.Text.RegularExpressions;

namespace BacateTagAssist.Core.Naming;

/// <summary>Informações deduzidas do nome atual da pasta/arquivo. Servem apenas como sugestão.</summary>
public sealed record ParsedRelease
{
    public string? Title { get; init; }
    public string? Year { get; init; }
    public string? Season { get; init; }
    public string? Source { get; init; }
    public string? ReleaseType { get; init; }
    public string? Streaming { get; init; }
    public string? Edition { get; init; }
    public string? DualMulti { get; init; }
    public string? Group { get; init; }
    public string? OriginalGroup { get; init; }
    public bool? TwoGroups { get; init; }
    public bool? Repack { get; init; }
    public bool? Hybrid { get; init; }
    public bool IsEmpty => Title is null && Year is null && Source is null && Group is null && Streaming is null;
}

/// <summary>Lê nomes como "Serie.S01.1080p.NF.WEB-DL.DDP5.1.H.264-GRUPO".</summary>
public static partial class ReleaseNameParser
{
    private static readonly (Regex Pattern, string Value)[] Sources =
    [
        (new(@"(?<![a-z])UHD[ .-]?Blu-Ray(?![a-z])", RegexOptions.IgnoreCase), "UHD Blu-Ray"),
        (new(@"(?<![a-z])UHD[ .-]?BluRay(?![a-z])", RegexOptions.IgnoreCase), "UHD BluRay"),
        (new(@"(?<![a-z])Blu-Ray(?![a-z])", RegexOptions.IgnoreCase), "Blu-Ray"),
        (new(@"(?<![a-z])BluRay(?![a-z])", RegexOptions.IgnoreCase), "BluRay"),
        (new(@"(?<![a-z])BRRip(?![a-z])", RegexOptions.IgnoreCase), "BRRip"),
        (new(@"(?<![a-z])WEB[ .-]?Rip(?![a-z])", RegexOptions.IgnoreCase), "WEBRip"),
        (new(@"(?<![a-z])WEB[ .-]?DL(?![a-z])", RegexOptions.IgnoreCase), "WEB-DL"),
        (new(@"(?<![a-z])WEB(?![a-z-])", RegexOptions.IgnoreCase), "WEB-DL"),
        (new(@"(?<![a-z])DVDRip(?![a-z])", RegexOptions.IgnoreCase), "DVDRip"),
        (new(@"(?<![a-z])DVD9(?![a-z0-9])", RegexOptions.IgnoreCase), "DVD9"),
        (new(@"(?<![a-z])DVD5(?![a-z0-9])", RegexOptions.IgnoreCase), "DVD5"),
        (new(@"(?<![a-z])HDTV(?![a-z])", RegexOptions.IgnoreCase), "HDTV"),
        (new(@"(?<![a-z])PDTV(?![a-z])", RegexOptions.IgnoreCase), "PDTV"),
        (new(@"(?<![a-z])TVRip(?![a-z])", RegexOptions.IgnoreCase), "TVRip"),
        (new(@"(?<![a-z])VHSRip(?![a-z])", RegexOptions.IgnoreCase), "VHSRip"),
        (new(@"(?<![a-z])LDRip(?![a-z])", RegexOptions.IgnoreCase), "LDRip"),
        (new(@"(?<![a-z])DVD(?![a-z0-9])", RegexOptions.IgnoreCase), "DVD"),
    ];

    private static readonly string[] Streamings = ["AMZN", "ATVP", "ATV", "CR", "DSNP", "GLBO", "HMAX", "HULU", "NF", "PMTP", "VIKI"];

    private static readonly (Regex Pattern, string Value)[] Editions =
    [
        (new(@"(?<![a-z])Director'?s[ .]Cut(?![a-z])", RegexOptions.IgnoreCase), "Director's Cut"),
        (new(@"(?<![a-z])Extended(?:[ .]Cut|[ .]Edition)?(?![a-z])", RegexOptions.IgnoreCase), "Extended"),
        (new(@"(?<![a-z])Theatrical(?![a-z])", RegexOptions.IgnoreCase), "Theatrical"),
        (new(@"(?<![a-z])Unrated(?![a-z])", RegexOptions.IgnoreCase), "Unrated"),
        (new(@"(?<![a-z])Uncut(?![a-z])", RegexOptions.IgnoreCase), "Uncut"),
        (new(@"(?<![a-z])Remastered(?![a-z])", RegexOptions.IgnoreCase), "Remastered"),
        (new(@"(?<![a-z])IMAX(?![a-z])", RegexOptions.IgnoreCase), "IMAX"),
        (new(@"(?<![a-z])Criterion(?![a-z])", RegexOptions.IgnoreCase), "Criterion"),
    ];

    private static readonly HashSet<string> NotGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "DL", "Rip", "Ray", "HD", "MA", "HRA", "X", "ES", "AC3", "DTS", "WEB", "TV", "264", "265", "HDR", "DV", "SDR", "AAC", "Atmos",
    };

    public static ParsedRelease Parse(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new ParsedRelease();
        var text = name.Trim();

        string? leadingGroup = null;
        var bracket = LeadingBracket().Match(text);
        if (bracket.Success)
        {
            leadingGroup = bracket.Groups["g"].Value.Trim();
            text = text[bracket.Length..].Trim();
        }

        var tokens = TokenSplit().Split(text).Where(t => t.Length > 0).ToArray();

        string? source = null;
        foreach (var (pattern, value) in Sources)
        {
            if (pattern.IsMatch(text)) { source = value; break; }
        }

        var streaming = tokens.Select(t => t.ToUpperInvariant()).FirstOrDefault(t => Streamings.Contains(t));
        string? edition = null;
        foreach (var (pattern, value) in Editions)
        {
            if (pattern.IsMatch(text)) { edition = value; break; }
        }

        string? dualMulti = null;
        if (tokens.Any(t => t.Equals("MULTi", StringComparison.OrdinalIgnoreCase))) dualMulti = "MULTi";
        else if (tokens.Any(t => t.Equals("DUAL", StringComparison.OrdinalIgnoreCase))) dualMulti = "DUAL";

        string? group = null, originalGroup = null;
        bool? twoGroups = null;
        var two = TwoGroupsSuffix().Match(text);
        if (two.Success)
        {
            originalGroup = two.Groups["orig"].Value;
            dualMulti = two.Groups["lang"].Value.Equals("DUAL", StringComparison.OrdinalIgnoreCase) ? "DUAL" : "MULTi";
            group = two.Groups["grp"].Value;
            twoGroups = true;
        }
        else
        {
            var g = GroupSuffix().Match(text);
            if (g.Success && !NotGroups.Contains(g.Groups["grp"].Value) && !g.Groups["grp"].Value.All(char.IsDigit))
                group = g.Groups["grp"].Value;
            else if (leadingGroup is { Length: > 0 and <= 30 })
                group = leadingGroup;
        }
        if (group is not null && group.Equals("NoGroup", StringComparison.OrdinalIgnoreCase)) group = null;

        var season = EpisodeDetector.SeasonFromFolder(text) ?? EpisodeDetector.SeasonOf(EpisodeDetector.Detect(text));

        // Título: tudo antes do ano / SxxEyy / primeiro token técnico.
        string? year = null;
        var cut = text.Length;
        var y = Year().Match(text);
        if (y.Success && y.Index > 0) { year = y.Value; cut = Math.Min(cut, y.Index); }
        var s = SeasonMarker().Match(text);
        if (s.Success && s.Index > 0) cut = Math.Min(cut, s.Index);
        var t = TechMarker().Match(text);
        if (t.Success && t.Index > 0) cut = Math.Min(cut, t.Index);
        var dash = AnimeDash().Match(text);
        if (dash.Success && dash.Index > 0) cut = Math.Min(cut, dash.Index);

        string? title = null;
        if (cut > 0 && (y.Success || s.Success || t.Success || dash.Success))
        {
            var raw = text[..cut];
            raw = Separators().Replace(raw, " ");
            raw = Brackets().Replace(raw, " ");
            raw = Spaces().Replace(raw, " ").Trim(' ', '-');
            if (raw.Length >= 1) title = raw;
        }

        return new ParsedRelease
        {
            Title = title,
            Year = year,
            Season = season,
            Source = source,
            ReleaseType = Remux().IsMatch(text) ? "remux" : null,
            Streaming = streaming,
            Edition = edition,
            DualMulti = dualMulti,
            Group = group,
            OriginalGroup = originalGroup,
            TwoGroups = twoGroups,
            Repack = RepackRx().IsMatch(text) ? true : null,
            Hybrid = HybridRx().IsMatch(text) ? true : null,
        };
    }

    [GeneratedRegex(@"^\[(?<g>[^\]]+)\]")] private static partial Regex LeadingBracket();
    [GeneratedRegex(@"[ ._\[\]()]+")] private static partial Regex TokenSplit();
    [GeneratedRegex(@"-(?<orig>[A-Za-z0-9]+)[ .](?<lang>DUAL|MULTi)-(?<grp>[A-Za-z0-9][A-Za-z0-9_]{0,30})$", RegexOptions.IgnoreCase)] private static partial Regex TwoGroupsSuffix();
    [GeneratedRegex(@"-(?<grp>[A-Za-z0-9][A-Za-z0-9_]{0,30})$")] private static partial Regex GroupSuffix();
    [GeneratedRegex(@"(?<![0-9])(?:19[2-9]\d|20[0-4]\d)(?![0-9p])")] private static partial Regex Year();
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:S\d{1,2}(?:E\d{1,3})?|\d{1,2}x\d{2,3}|Season[ ._]\d|Temporada[ ._]\d)(?![0-9])", RegexOptions.IgnoreCase)] private static partial Regex SeasonMarker();
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:2160p|1080[pi]|720p|576p|480p|4K|UHD|WEB|WEB-?DL|WEBRip|BluRay|Blu-Ray|BDRip|BRRip|HDTV|DVD\w*|REMUX|Hybrid|REPACK|PROPER|x26[45]|H\.?26[45]|HEVC|AVC)(?![A-Za-z0-9])", RegexOptions.IgnoreCase)] private static partial Regex TechMarker();
    [GeneratedRegex(@"\s-\s\d{1,3}(?:v\d)?(?=\s|\[|\(|$)")] private static partial Regex AnimeDash();
    [GeneratedRegex(@"[._]+")] private static partial Regex Separators();
    [GeneratedRegex(@"[\[\](){}]")] private static partial Regex Brackets();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"(?<![A-Za-z])REMUX(?![A-Za-z])", RegexOptions.IgnoreCase)] private static partial Regex Remux();
    [GeneratedRegex(@"(?<![A-Za-z])REPACK\d?(?![A-Za-z])", RegexOptions.IgnoreCase)] private static partial Regex RepackRx();
    [GeneratedRegex(@"(?<![A-Za-z])Hybrid(?![A-Za-z])", RegexOptions.IgnoreCase)] private static partial Regex HybridRx();
}
