using BacateTagAssist.Core.Naming;

namespace BacateTagAssist.Core.Media;

public sealed record FileEntry(
    string RelPath,
    string Name,
    string Dir,
    string Extension,
    string Kind,
    long Size,
    string? SeasonEpisode,
    string? LangSuffix);

public sealed record ScanResult(
    string ScanId,
    string Root,
    string RootName,
    IReadOnlyList<FileEntry> Files,
    int Count,
    int Videos,
    int Subtitles,
    long TotalBytes,
    string SuggestedMode,
    string? SuggestedSeason,
    string? LargestVideo,
    bool IsRootLocked,
    bool Truncated,
    ParsedRelease Parsed);

/// <summary>Varre a pasta escolhida procurando vídeos e legendas compatíveis.</summary>
public static class MediaScanner
{
    public const int MaxFiles = 5000;

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".m2ts", ".webm" };

    public static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".srt", ".ass", ".ssa", ".vtt" };

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
        { "BDMV", "CERTIFICATE", "VIDEO_TS", "AUDIO_TS", "$RECYCLE.BIN", "System Volume Information", "@eaDir", ".git" };

    public static bool IsMedia(string path)
    {
        var ext = Path.GetExtension(path);
        return VideoExtensions.Contains(ext) || SubtitleExtensions.Contains(ext);
    }

    public static ScanResult Scan(string root, bool isRootLocked)
    {
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"Pasta não encontrada: {root}");

        var files = new List<FileEntry>();
        var truncated = false;
        var stack = new Stack<string>();
        stack.Push(full);

        while (stack.Count > 0 && !truncated)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            var subdirs = new List<string>();
            foreach (var entry in entries)
            {
                FileAttributes attrs;
                try { attrs = File.GetAttributes(entry); } catch { continue; }

                if (attrs.HasFlag(FileAttributes.Directory))
                {
                    var name = Path.GetFileName(entry);
                    if (IgnoredFolders.Contains(name) || attrs.HasFlag(FileAttributes.ReparsePoint)) continue;
                    subdirs.Add(entry);
                    continue;
                }

                if (!IsMedia(entry)) continue;
                if (files.Count >= MaxFiles) { truncated = true; break; }
                files.Add(Describe(full, entry));
            }

            subdirs.Sort(NaturalComparer.Instance);
            for (var i = subdirs.Count - 1; i >= 0; i--) stack.Push(subdirs[i]);
        }

        files.Sort((a, b) => NaturalComparer.Instance.Compare(a.RelPath, b.RelPath));

        var videos = files.Where(f => f.Kind == "video").ToList();
        var largest = videos.OrderByDescending(f => f.Size).FirstOrDefault();
        var withEpisode = videos.Count(f => f.SeasonEpisode is not null);
        var rootName = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(rootName)) rootName = full;

        var suggestedMode = videos.Count switch
        {
            > 1 when withEpisode > 0 => "season",
            1 when withEpisode == 1 && videos[0].SeasonEpisode!.Contains('E') => "episode",
            _ => "movie",
        };

        var parsed = ReleaseNameParser.Parse(rootName);
        if (parsed.IsEmpty && largest is not null)
            parsed = ReleaseNameParser.Parse(Path.GetFileNameWithoutExtension(largest.Name));

        var season = files.Select(f => EpisodeDetector.SeasonOf(f.SeasonEpisode)).FirstOrDefault(s => s is not null)
                     ?? parsed.Season
                     ?? EpisodeDetector.SeasonFromFolder(rootName);

        return new ScanResult(
            ScanId: Guid.NewGuid().ToString("N"),
            Root: full,
            RootName: rootName,
            Files: files,
            Count: files.Count,
            Videos: videos.Count,
            Subtitles: files.Count - videos.Count,
            TotalBytes: files.Sum(f => f.Size),
            SuggestedMode: suggestedMode,
            SuggestedSeason: season,
            LargestVideo: largest?.RelPath,
            IsRootLocked: isRootLocked,
            Truncated: truncated,
            Parsed: parsed);
    }

    private static FileEntry Describe(string root, string path)
    {
        var info = new FileInfo(path);
        var rel = Path.GetRelativePath(root, path);
        var dir = Path.GetDirectoryName(rel) ?? "";
        var ext = info.Extension;
        var kind = VideoExtensions.Contains(ext) ? "video" : "subtitle";
        var stem = Path.GetFileNameWithoutExtension(info.Name);
        string? suffix = null;
        if (kind == "subtitle") (stem, suffix) = EpisodeDetector.SplitSubtitleSuffix(stem);
        return new FileEntry(rel, info.Name, dir, ext, kind, info.Length, EpisodeDetector.Detect(stem), suffix);
    }
}

/// <summary>Ordenação "natural": 2.mkv antes de 10.mkv.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var si = i; while (i < x.Length && char.IsDigit(x[i])) i++;
                var sj = j; while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0'); var b = y[sj..j].TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var c = string.CompareOrdinal(a, b);
                if (c != 0) return c;
            }
            else
            {
                var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
