using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BacateTagAssist.Core.Config;

namespace BacateTagAssist.Core.Media;

public sealed record MediaInfoStatus(bool Available, string? Version, string? Path, bool CanInstall, string? Hint);

public sealed record VideoFacts(
    string Format, string? Profile, string CodecFamily, string? EncoderLibrary, int Width, int Height,
    string? ScanType, double FrameRate, int? BitDepth, string? HdrFormat, string? Transfer,
    string Resolution, string Range, bool Hfr);

public sealed record AudioFacts(
    int Index, string Format, string? Commercial, string Codec, string Channels, int ChannelCount,
    bool Atmos, string? Language, string? Title, bool IsDefault, bool IsCommentary, long? BitRate);

public sealed record TextFacts(string? Language, string Format, string? Title, bool Forced);

public sealed record MediaSuggestions(
    string? Resolution, string? Range, bool Hfr, string? AudioCodec, string? Channels, bool Atmos,
    string? DualMulti, int PrimaryAudio, string CodecFamily, string? EncoderLibrary);

public sealed record MediaReport(
    string File, string Container, double DurationSeconds, long SizeBytes,
    VideoFacts? Video, IReadOnlyList<AudioFacts> Audio, IReadOnlyList<TextFacts> Subtitles,
    MediaSuggestions Suggestions);

/// <summary>Executa o MediaInfo CLI e converte o resultado para os valores das listas do app.</summary>
public sealed partial class MediaInfoService(AppEnvironment env, SettingsStore settings)
{
    public const string BundledVersion = "26.10";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private string? _cachedPath;
    private string? _cachedVersion;

    private string ToolsDir => System.IO.Path.Combine(env.ConfigDir, "tools", "mediainfo");

    // ------------------------------------------------------------------ localização

    public string? Locate()
    {
        var candidates = new List<string?>
        {
            settings.Current.MediaInfoPath,
            Environment.GetEnvironmentVariable("BTA_MEDIAINFO"),
        };
        if (OperatingSystem.IsWindows())
        {
            candidates.Add(System.IO.Path.Combine(AppContext.BaseDirectory, "mediainfo", "MediaInfo.exe"));
            candidates.Add(System.IO.Path.Combine(AppContext.BaseDirectory, "MediaInfo.exe"));
            candidates.Add(System.IO.Path.Combine(ToolsDir, "MediaInfo.exe"));
        }
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) return c;

        var exe = OperatingSystem.IsWindows() ? "MediaInfo.exe" : "mediainfo";
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var p = System.IO.Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(p)) return p;
            }
            catch { /* entrada inválida no PATH */ }
        }
        return null;
    }

    public async Task<MediaInfoStatus> StatusAsync()
    {
        var path = Locate();
        if (path is null)
        {
            return new MediaInfoStatus(false, null, null, OperatingSystem.IsWindows(),
                OperatingSystem.IsWindows()
                    ? "MediaInfo não encontrado. Clique em instalar para baixar automaticamente (≈4 MB)."
                    : "Instale o pacote 'mediainfo' (a imagem Docker oficial já inclui).");
        }
        if (path != _cachedPath)
        {
            _cachedPath = path;
            _cachedVersion = null;
            try
            {
                var (code, output) = await RunAsync(path, ["--Version"], TimeSpan.FromSeconds(10));
                var m = VersionRx().Match(output);
                _cachedVersion = code == 0 && m.Success ? m.Groups[1].Value : null;
            }
            catch { _cachedVersion = null; }
        }
        return new MediaInfoStatus(true, _cachedVersion, path, false, null);
    }

    /// <summary>Baixa o MediaInfo CLI oficial (Windows) para a pasta de dados do app.</summary>
    public async Task<MediaInfoStatus> InstallAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Instalação automática disponível apenas no Windows.");

        var version = BundledVersion;
        try
        {
            var page = await Http.GetStringAsync("https://mediaarea.net/en/MediaInfo/Download/Windows", ct);
            var m = CliZipRx().Match(page);
            if (m.Success) version = m.Groups[1].Value;
        }
        catch { /* usa a versão conhecida */ }

        var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "ARM64" : "x64";
        var url = $"https://mediaarea.net/download/binary/mediainfo/{version}/MediaInfo_CLI_{version}_Windows_{arch}.zip";
        Directory.CreateDirectory(ToolsDir);
        var zip = System.IO.Path.Combine(ToolsDir, "mediainfo.zip");
        await using (var stream = await Http.GetStreamAsync(url, ct))
        await using (var file = File.Create(zip))
            await stream.CopyToAsync(file, ct);
        ZipFile.ExtractToDirectory(zip, ToolsDir, overwriteFiles: true);
        File.Delete(zip);
        _cachedPath = null;
        return await StatusAsync();
    }

    // ------------------------------------------------------------------ análise

    public async Task<MediaReport> AnalyzeAsync(string file, CancellationToken ct = default)
    {
        var path = Locate() ?? throw new InvalidOperationException("MediaInfo não está disponível. Instale-o nas configurações.");
        var (code, output) = await RunAsync(path, ["--Output=JSON", file], TimeSpan.FromSeconds(90), ct);
        if (code != 0 || string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("O MediaInfo não conseguiu ler este arquivo.");
        return Parse(output, file);
    }

    internal static MediaReport Parse(string json, string file)
    {
        using var doc = JsonDocument.Parse(json);
        var media = doc.RootElement.GetProperty("media");
        var tracks = media.GetProperty("track").EnumerateArray().ToList();

        var general = tracks.FirstOrDefault(t => Str(t, "@type") == "General");
        var videoTrack = tracks.FirstOrDefault(t => Str(t, "@type") == "Video");
        var audioTracks = tracks.Where(t => Str(t, "@type") == "Audio").ToList();
        var textTracks = tracks.Where(t => Str(t, "@type") == "Text").ToList();

        VideoFacts? video = videoTrack.ValueKind == JsonValueKind.Object ? ParseVideo(videoTrack) : null;
        var audio = audioTracks.Select(ParseAudio).ToList();
        var subs = textTracks.Select(t => new TextFacts(Str(t, "Language"), Str(t, "Format") ?? "?", Str(t, "Title"), Str(t, "Forced") == "Yes")).ToList();

        var primary = audio.FirstOrDefault(a => a.IsDefault && !a.IsCommentary) ?? audio.FirstOrDefault(a => !a.IsCommentary) ?? audio.FirstOrDefault();

        var languages = audio.Where(a => !a.IsCommentary && !string.IsNullOrEmpty(a.Language))
            .Select(a => BaseLang(a.Language!)).Where(l => l is not "und" and not "zxx" and not "mul").Distinct().ToList();
        string? dualMulti = null;
        if (languages.Contains("pt"))
        {
            if (languages.Count == 2) dualMulti = "DUAL";
            else if (languages.Count >= 3) dualMulti = "MULTi";
        }

        var suggestions = new MediaSuggestions(
            Resolution: video?.Resolution is { Length: > 0 } r ? r : null,
            Range: video is { Range.Length: > 0 } ? video.Range : null,
            Hfr: video?.Hfr ?? false,
            AudioCodec: primary is { Codec.Length: > 0 } ? primary.Codec : null,
            Channels: primary is { Channels.Length: > 0 } ? primary.Channels : null,
            Atmos: primary?.Atmos ?? false,
            DualMulti: dualMulti,
            PrimaryAudio: primary?.Index ?? -1,
            CodecFamily: video?.CodecFamily ?? "",
            EncoderLibrary: video?.EncoderLibrary);

        return new MediaReport(
            File: System.IO.Path.GetFileName(file),
            Container: Str(general, "Format") ?? "",
            DurationSeconds: Dbl(general, "Duration"),
            SizeBytes: (long)Dbl(general, "FileSize"),
            Video: video,
            Audio: audio,
            Subtitles: subs,
            Suggestions: suggestions);
    }

    private static VideoFacts ParseVideo(JsonElement t)
    {
        var format = Str(t, "Format") ?? "";
        var family = format.ToUpperInvariant() switch
        {
            "AVC" => "avc",
            "HEVC" => "hevc",
            "VC-1" => "vc1",
            "AV1" => "av1",
            "MPEG VIDEO" => "mpeg2",
            "MPEG-4 VISUAL" => "xvid",
            _ => "other",
        };

        var lib = $"{Str(t, "Encoded_Library_Name")} {Str(t, "Encoded_Library")}";
        string? encoder = lib.Contains("x265", StringComparison.OrdinalIgnoreCase) ? "x265"
            : lib.Contains("x264", StringComparison.OrdinalIgnoreCase) ? "x264" : null;

        var width = (int)Dbl(t, "Width");
        var height = (int)Dbl(t, "Height");
        var scan = Str(t, "ScanType");
        var fps = Dbl(t, "FrameRate");
        if (fps <= 0) fps = Dbl(t, "FrameRate_Original");
        int? depth = Dbl(t, "BitDepth") is var d and > 0 ? (int)d : null;
        var hdr = Str(t, "HDR_Format");
        var compat = Str(t, "HDR_Format_Compatibility");
        var transfer = Str(t, "transfer_characteristics");

        var interlaced = scan is not null && !scan.Equals("Progressive", StringComparison.OrdinalIgnoreCase);
        var hdrText = string.Join(" / ", new[] { hdr, compat }.Where(s => !string.IsNullOrEmpty(s)));
        return new VideoFacts(format, Str(t, "Format_Profile"), family, encoder, width, height, scan, fps, depth,
            hdrText.Length > 0 ? hdrText : null, transfer,
            ClassifyResolution(width, height, interlaced), ClassifyRange(hdr, compat, transfer), fps >= 47);
    }

    internal static string ClassifyResolution(int width, int height, bool interlaced)
    {
        if (width <= 0 && height <= 0) return "";
        if (width >= 3200 || height >= 1900) return "2160p";
        if (width >= 1700 || height >= 1000) return interlaced ? "1080i" : "1080p";
        if (width >= 1100 || height >= 700) return "720p";
        if (height >= 540) return "576p";
        return "480p";
    }

    internal static string ClassifyRange(string? hdrFormat, string? compat, string? transfer)
    {
        var all = $"{hdrFormat} {compat}";
        var tr = transfer ?? "";
        var dv = all.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase);
        var hdr10Plus = all.Contains("2094", StringComparison.OrdinalIgnoreCase) || all.Contains("HDR10+", StringComparison.OrdinalIgnoreCase);
        var pq = tr.Contains("PQ", StringComparison.OrdinalIgnoreCase) || tr.Contains("2084", StringComparison.OrdinalIgnoreCase);
        var hdr10 = all.Contains("2086", StringComparison.OrdinalIgnoreCase) || all.Contains("HDR10", StringComparison.OrdinalIgnoreCase) || pq;
        var hlg = tr.Contains("HLG", StringComparison.OrdinalIgnoreCase) || tr.Contains("B67", StringComparison.OrdinalIgnoreCase)
                  || all.Contains("HLG", StringComparison.OrdinalIgnoreCase);

        if (dv && hdr10Plus) return "DV HDR10+";
        if (dv && hdr10) return "DV HDR";
        if (dv) return "DV";
        if (hdr10Plus) return "HDR10+";
        if (hlg) return "HLG";
        if (hdr10) return "HDR";
        return "";
    }

    private static AudioFacts ParseAudio(JsonElement t, int index)
    {
        var format = Str(t, "Format") ?? "";
        var commercial = Str(t, "Format_Commercial_IfAny") ?? Str(t, "Format_Commercial");
        var features = Str(t, "Format_AdditionalFeatures") ?? "";
        var profile = Str(t, "Format_Profile") ?? "";
        var all = $"{format} {commercial} {features} {profile}";

        var atmosFlag = all.Contains("Atmos", StringComparison.OrdinalIgnoreCase)
                        || features.Contains("JOC", StringComparison.OrdinalIgnoreCase)
                        || features.Contains("16-ch", StringComparison.OrdinalIgnoreCase);

        var upper = format.ToUpperInvariant();
        string codec;
        if (upper == "AC-3") codec = "DD";
        else if (upper == "E-AC-3") codec = "DDP";
        else if (upper is "MLP FBA" or "TRUEHD" || all.Contains("TrueHD", StringComparison.OrdinalIgnoreCase)) codec = "TrueHD";
        else if (upper.StartsWith("DTS", StringComparison.Ordinal)) codec = MapDts(all);
        else if (upper == "AAC") codec = "AAC";
        else if (upper == "FLAC") codec = "FLAC";
        else if (upper == "PCM") codec = "LPCM";
        else if (upper == "MPEG AUDIO" && profile.Contains("Layer 3", StringComparison.OrdinalIgnoreCase)) codec = "MP3";
        else codec = "";

        var channelsRaw = Str(t, "Channels") ?? Str(t, "Channels_Original") ?? "";
        var count = int.TryParse(channelsRaw.Split(' ', '/')[0], out var c) ? c : 0;
        var channels = count switch
        {
            1 => "1.0",
            2 or 3 => "2.0",
            >= 4 and <= 6 => "5.1",
            >= 7 => "7.1",
            _ => "",
        };

        var title = Str(t, "Title");
        var commentary = title is not null && (title.Contains("comment", StringComparison.OrdinalIgnoreCase) || title.Contains("coment", StringComparison.OrdinalIgnoreCase));
        long? bitrate = Dbl(t, "BitRate") is var b and > 0 ? (long)b : null;
        var atmos = atmosFlag && (codec is "TrueHD" or "DDP");

        return new AudioFacts(index, format, commercial, codec, channels, count, atmos,
            Str(t, "Language"), title, Str(t, "Default") == "Yes", commentary, bitrate);
    }

    private static string MapDts(string all)
    {
        if (all.Contains("XLL X", StringComparison.OrdinalIgnoreCase) || all.Contains("DTS:X", StringComparison.OrdinalIgnoreCase)) return "DTS:X";
        if (all.Contains("XLL", StringComparison.OrdinalIgnoreCase) || all.Contains("Master Audio", StringComparison.OrdinalIgnoreCase)) return "DTS-HD MA";
        if (all.Contains("XBR", StringComparison.OrdinalIgnoreCase) || all.Contains("High Resolution", StringComparison.OrdinalIgnoreCase)) return "DTS-HD HRA";
        if (all.Contains("DTS-ES", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(all, @"\bES\b")) return "DTS-ES";
        return "DTS";
    }

    private static string BaseLang(string lang)
    {
        var l = lang.Split('-', '_')[0].ToLowerInvariant();
        return l switch
        {
            "por" or "pob" => "pt", "eng" => "en", "jpn" => "ja", "spa" => "es", "fre" or "fra" => "fr",
            "ger" or "deu" => "de", "ita" => "it", "kor" => "ko", "chi" or "zho" => "zh", _ => l,
        };
    }

    private static string? Str(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null;
    }

    private static double Dbl(JsonElement e, string name)
    {
        var s = Str(e, name);
        if (s is null) return 0;
        s = s.Split(' ', '/')[0];
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private static async Task<(int Code, string Output)> RunAsync(string exe, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o MediaInfo.");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = process.StandardError.ReadToEndAsync(cts.Token);
        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* já encerrado */ }
            throw new TimeoutException("O MediaInfo demorou demais para responder.");
        }
        await stderr;
        return (process.ExitCode, await stdout);
    }

    [GeneratedRegex(@"v(\d+(?:\.\d+)+)")] private static partial Regex VersionRx();
    [GeneratedRegex(@"MediaInfo_CLI_([\d\.]+)_Windows_x64\.zip")] private static partial Regex CliZipRx();
}
