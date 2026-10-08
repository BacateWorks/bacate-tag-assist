namespace BacateTagAssist.Core.Naming;

/// <summary>Tipo de release sendo renomeado.</summary>
public enum ReleaseMode
{
    Movie,
    Season,
    Episode,
}

/// <summary>
/// Campos preenchidos pelo usuário (ou sugeridos por MediaInfo/TMDB/nome do arquivo).
/// Os nomes das propriedades espelham os <c>data-field</c> da interface.
/// </summary>
public sealed record ReleaseFields
{
    public string Title { get; init; } = "";
    public string Year { get; init; } = "";
    public string Season { get; init; } = "";
    public string Episode { get; init; } = "";
    public string Edition { get; init; } = "";
    public string Resolution { get; init; } = "";
    public string Streaming { get; init; } = "";
    public string CustomStreaming { get; init; } = "";
    public string Source { get; init; } = "";
    public string ReleaseType { get; init; } = "normal";
    public string Special { get; init; } = "";
    public string Region { get; init; } = "";
    public string DiscCount { get; init; } = "";
    public string AudioCodec { get; init; } = "";
    public string Channels { get; init; } = "";
    public string Range { get; init; } = "";
    public string VideoCodec { get; init; } = "";
    public bool Atmos { get; init; }
    public bool Hfr { get; init; }
    public bool Hybrid { get; init; }
    public bool Repack { get; init; }
    public string DualMulti { get; init; } = "";
    public string Group { get; init; } = "";
    public string OriginalGroup { get; init; } = "";

    /// <summary>Retorna uma cópia com todos os textos aparados (trim) e nulos convertidos em vazio.</summary>
    public ReleaseFields Normalized() => this with
    {
        Title = T(Title), Year = T(Year), Season = T(Season), Episode = T(Episode), Edition = T(Edition),
        Resolution = T(Resolution), Streaming = T(Streaming), CustomStreaming = T(CustomStreaming),
        Source = T(Source), ReleaseType = string.IsNullOrWhiteSpace(ReleaseType) ? "normal" : T(ReleaseType),
        Special = T(Special), Region = T(Region), DiscCount = T(DiscCount), AudioCodec = T(AudioCodec),
        Channels = T(Channels), Range = T(Range), VideoCodec = T(VideoCodec), DualMulti = T(DualMulti),
        Group = T(Group), OriginalGroup = T(OriginalGroup),
    };

    /// <summary>Sigla de streaming efetiva ("custom" usa o campo livre em maiúsculas).</summary>
    public string EffectiveStreaming =>
        Streaming.Equals("custom", StringComparison.OrdinalIgnoreCase) ? CustomStreaming.Trim().ToUpperInvariant() : Streaming;

    private static string T(string? value) => (value ?? "").Trim();
}

/// <summary>Opções que não fazem parte do nome em si, mas mudam a montagem.</summary>
public sealed record NamingOptions(ReleaseMode Mode = ReleaseMode.Movie, bool Dots = true, bool TwoGroups = false);

/// <summary>Um pedaço do nome final, com o tipo semântico usado pela interface para colorir.</summary>
/// <param name="Text">Texto do token já no formato final (com pontos, se habilitado).</param>
/// <param name="Kind">Tipo semântico (title, year, id, resolution, source, audio...).</param>
/// <param name="Field">Campo do formulário que originou o token (para navegação).</param>
/// <param name="Sep">Separador que antecede o token (" " vira "." com pontos; "-" para grupos).</param>
public sealed record NameToken(string Text, string Kind, string Field, string Sep);

public sealed record BuiltName(string Name, IReadOnlyList<NameToken> Tokens);
