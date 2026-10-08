using BacateTagAssist.Core.Config;
using BacateTagAssist.Core.Media;

namespace BacateTagAssist.Core.FileSystem;

public sealed class AccessDeniedException(string message) : Exception(message);

/// <summary>Garante que caminhos recebidos pela API ficam dentro das raízes permitidas.</summary>
public sealed class PathGuard(AppEnvironment env)
{
    private static readonly StringComparison Cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public string Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho vazio.");
        var cleaned = path.Trim().Trim('"');
        string full;
        try { full = Path.GetFullPath(cleaned); }
        catch (Exception) { throw new ArgumentException($"Caminho inválido: {path}"); }

        if (env.RestrictedRoots is { Count: > 0 } roots && !roots.Any(r => IsWithin(full, r)))
            throw new AccessDeniedException("Este caminho está fora das pastas liberadas (BTA_ROOTS).");
        return full;
    }

    /// <summary>Pastas que não podem ser renomeadas: unidades e as próprias raízes liberadas.</summary>
    public bool IsLocked(string fullPath)
    {
        var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.GetPathRoot(fullPath) is { } root && string.Equals(root.TrimEnd('\\', '/'), trimmed, Cmp)) return true;
        if (trimmed.Length == 0) return true;
        return env.RestrictedRoots?.Any(r => string.Equals(r.TrimEnd('\\', '/'), trimmed, Cmp)) ?? false;
    }

    public static bool IsWithin(string path, string root)
    {
        var r = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var p = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (r.Length == 0) return true; // raiz "/"
        return string.Equals(p, r, Cmp) || p.StartsWith(r + Path.DirectorySeparatorChar, Cmp);
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), Cmp);
}

public sealed record Crumb(string Name, string Path);
public sealed record DirEntry(string Name, string Path);
public sealed record DirectoryListing(string Path, string Name, string? Parent, IReadOnlyList<Crumb> Crumbs, IReadOnlyList<DirEntry> Dirs, int Videos, int Subtitles);

/// <summary>Navegador de pastas do lado do servidor (essencial no Docker, onde não há diálogo nativo).</summary>
public sealed class DirectoryBrowser(AppEnvironment env, PathGuard guard)
{
    public DirectoryListing List(string path)
    {
        var full = guard.Resolve(path);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"Pasta não encontrada: {path}");

        var dirs = new List<DirEntry>();
        int videos = 0, subs = 0;
        try
        {
            foreach (var d in Directory.EnumerateDirectories(full))
            {
                try
                {
                    var attrs = File.GetAttributes(d);
                    if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System)) continue;
                }
                catch { continue; }
                var name = System.IO.Path.GetFileName(d);
                if (name.StartsWith('.') || name.StartsWith('$') || name == "@eaDir") continue;
                dirs.Add(new DirEntry(name, d));
            }
            foreach (var f in Directory.EnumerateFiles(full))
            {
                var ext = System.IO.Path.GetExtension(f);
                if (MediaScanner.VideoExtensions.Contains(ext)) videos++;
                else if (MediaScanner.SubtitleExtensions.Contains(ext)) subs++;
            }
        }
        catch (UnauthorizedAccessException) { throw new AccessDeniedException("Sem permissão para ler esta pasta."); }

        dirs.Sort((a, b) => NaturalComparer.Instance.Compare(a.Name, b.Name));

        string? parent = Directory.GetParent(full)?.FullName;
        if (parent is not null && env.RestrictedRoots is { Count: > 0 } roots && !roots.Any(r => PathGuard.IsWithin(parent, r)))
            parent = null;

        return new DirectoryListing(full, System.IO.Path.GetFileName(full.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : full,
            parent, Crumbs(full), dirs, videos, subs);
    }

    private List<Crumb> Crumbs(string full)
    {
        var list = new List<Crumb>();
        var current = new DirectoryInfo(full);
        while (current is not null)
        {
            if (env.RestrictedRoots is { Count: > 0 } roots && !roots.Any(r => PathGuard.IsWithin(current.FullName, r))) break;
            list.Insert(0, new Crumb(current.Name.TrimEnd('\\') is { Length: > 0 } n ? n : current.FullName, current.FullName));
            current = current.Parent;
        }
        return list;
    }
}
