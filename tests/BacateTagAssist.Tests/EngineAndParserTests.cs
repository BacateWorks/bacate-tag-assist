using BacateTagAssist.Core.Config;
using BacateTagAssist.Core.FileSystem;
using BacateTagAssist.Core.Media;
using BacateTagAssist.Core.Naming;
using Xunit;

namespace BacateTagAssist.Tests;

public class EngineAndParserTests
{
    [Fact]
    public void Build_Movie_FollowsTrackerStandard()
    {
        var fields = new ReleaseFields
        {
            Title = "O Poderoso Chefão",
            Year = "1972",
            Resolution = "1080p",
            Source = "BluRay",
            AudioCodec = "DTS-HD MA",
            Channels = "5.1",
            VideoCodec = "x264",
            DualMulti = "DUAL",
            Group = "BiOMA",
        };

        var options = new NamingOptions(ReleaseMode.Movie, Dots: true, TwoGroups: false);
        var result = NamingEngine.Build(fields, options, identifier: "");

        // Exemplo esperado: O.Poderoso.Chefao.1972.1080p.BluRay.DTS-HD.MA.5.1.x264.DUAL-BiOMA
        // Nota: DTS-HD MA tem espaço entre HD e MA, que vira ponto.
        Assert.Contains("O.Poderoso.Chefão.1972.1080p.BluRay", result.Name);
        Assert.EndsWith("DUAL-BiOMA", result.Name);
    }

    [Fact]
    public void Build_SeasonPack_WithWebStreamingAndTwoGroups()
    {
        var fields = new ReleaseFields
        {
            Title = "Death March To The Parallel World Rhapsody",
            Season = "S01",
            Resolution = "1080p",
            Source = "WEB-DL",
            Streaming = "CR",
            AudioCodec = "DDP",
            Channels = "2.0",
            VideoCodec = "H.264",
            OriginalGroup = "FLUX",
            DualMulti = "DUAL",
            Group = "Kitsune",
        };

        var options = new NamingOptions(ReleaseMode.Season, Dots: true, TwoGroups: true);
        var result = NamingEngine.Build(fields, options, identifier: "S01");

        Assert.Equal(
            "Death.March.To.The.Parallel.World.Rhapsody.S01.1080p.CR.WEB-DL.DDP2.0.H.264-FLUX.DUAL-Kitsune",
            result.Name);
    }

    [Fact]
    public void Build_RemuxWithAtmosAndDV()
    {
        var fields = new ReleaseFields
        {
            Title = "Dune Part Two",
            Year = "2024",
            Resolution = "2160p",
            Source = "UHD Blu-Ray",
            ReleaseType = "remux",
            Range = "DV HDR10+",
            AudioCodec = "TrueHD",
            Channels = "7.1",
            Atmos = true,
            VideoCodec = "HEVC",
            DualMulti = "DUAL",
            Group = "BiOMA",
        };

        var options = new NamingOptions(ReleaseMode.Movie, Dots: true);
        var result = NamingEngine.Build(fields, options, identifier: "");

        // REMUX coloca REMUX logo após Source, depois Range, VideoCodec, Audio, Atmos.
        Assert.Equal("Dune.Part.Two.2024.2160p.UHD.Blu-Ray.REMUX.DV.HDR10+.HEVC.TrueHD7.1.Atmos.DUAL-BiOMA", result.Name);
    }

    [Fact]
    public void EpisodeDetector_DetectsVariousConventions()
    {
        Assert.Equal("S01E01", EpisodeDetector.Detect("Death.March.S01E01.mkv"));
        Assert.Equal("S01E02", EpisodeDetector.Detect("Death.March.1x02.mkv"));
        Assert.Equal("E05", EpisodeDetector.Detect("Episode 5"));
        Assert.Equal("E01", EpisodeDetector.Detect("01"));
        Assert.Equal("E05-E06", EpisodeDetector.Detect("5-6"));
        Assert.Equal("E12", EpisodeDetector.Detect("[Sub] Série - 12 [1080p]"));
    }

    [Fact]
    public void SubtitleSuffix_SplitsLanguageCodesProperly()
    {
        var (stem, suffix) = EpisodeDetector.SplitSubtitleSuffix("Death.March.S01E01.pt-BR");
        Assert.Equal("Death.March.S01E01", stem);
        Assert.Equal("pt-BR", suffix);

        var (s2, f2) = EpisodeDetector.SplitSubtitleSuffix("Filme.2020");
        Assert.Equal("Filme.2020", s2);
        Assert.Null(f2);
    }

    [Fact]
    public void ReleaseNameParser_ExtractsKeyInformation()
    {
        var parsed = ReleaseNameParser.Parse("Death.March.To.The.Parallel.World.Rhapsody.S01.1080p.CR.WEB-DL.DDP2.0.H.264-Kitsune");
        Assert.Equal("Death March To The Parallel World Rhapsody", parsed.Title);
        Assert.Equal("S01", parsed.Season);
        Assert.Equal("WEB-DL", parsed.Source);
        Assert.Equal("CR", parsed.Streaming);
        Assert.Equal("Kitsune", parsed.Group);
    }

    [Fact]
    public void MediaInfo_ResolutionAndRangeClassification()
    {
        Assert.Equal("2160p", MediaInfoService.ClassifyResolution(3840, 2160, false));
        Assert.Equal("1080p", MediaInfoService.ClassifyResolution(1920, 1080, false));
        Assert.Equal("1080i", MediaInfoService.ClassifyResolution(1920, 1080, true));
        Assert.Equal("720p", MediaInfoService.ClassifyResolution(1280, 720, false));

        Assert.Equal("DV HDR", MediaInfoService.ClassifyRange("Dolby Vision", "HDR10", "PQ"));
        Assert.Equal("DV HDR10+", MediaInfoService.ClassifyRange("Dolby Vision", "SMPTE ST 2094 App 4", "PQ"));
        Assert.Equal("HDR", MediaInfoService.ClassifyRange("SMPTE ST 2086", "", "PQ"));
        Assert.Equal("HLG", MediaInfoService.ClassifyRange("HLG", "", "HLG"));
        Assert.Equal("", MediaInfoService.ClassifyRange(null, null, "BT.709"));
    }

    [Fact]
    public void PathGuard_EnforcesRestrictedRoots()
    {
        var env = new AppEnvironment
        {
            Mode = AppMode.Server,
            ConfigDir = "C:\\test\\config",
            RestrictedRoots = ["C:\\media", "C:\\storage"],
        };
        var guard = new PathGuard(env);

        Assert.Equal("C:\\media\\filmes", guard.Resolve("C:\\media\\filmes"));
        Assert.Throws<AccessDeniedException>(() => guard.Resolve("C:\\Windows\\System32"));
    }
}
