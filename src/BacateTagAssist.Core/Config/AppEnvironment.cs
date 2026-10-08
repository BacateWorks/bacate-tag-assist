using System.Text.Json;
using System.Text.Json.Serialization;

namespace BacateTagAssist.Core.Config;

public enum AppMode { Desktop, Server }

/// <summary>Raiz navegável exibida no navegador de pastas.</summary>
public sealed record BrowseRoot(string Name, string Path, string Kind);

/// <summary>Configuração de execução (não persistida): modo, raízes permitidas e diretório de dados.</summary>
public sealed class AppEnvironment
{
    public required AppMode Mode { get; init; }
    public required string ConfigDir { get; init; }

    /// <summary>Raízes às quais o acesso é restrito. Null = sem restrição (modo desktop).</summary>
    public IReadOnlyList<string>? RestrictedRoots { get; init; }

    public string? StartupFolder { get; init; }

    public static string Version =>
        typeof(AppEnvironment).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";

    public static bool InContainer =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);

    public static AppEnvironment Create(AppMode mode, string? startupFolder = null)
    {
        var configDir = Environment.GetEnvironmentVariable("BTA_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configDir))
        {
            configDir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BacateTagAssist")
                : Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                    ? xdg
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"), "bacate-tag-assist");
        }
        Directory.CreateDirectory(configDir);

        IReadOnlyList<string>? roots = null;
        var rootsVar = Environment.GetEnvironmentVariable("BTA_ROOTS");
        if (!string.IsNullOrWhiteSpace(rootsVar))
        {
            roots = rootsVar.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Path.GetFullPath).Distinct().ToList();
        }
        else if (mode == AppMode.Server && InContainer)
        {
            roots = ["/media"];
        }

        return new AppEnvironment { Mode = mode, ConfigDir = configDir, RestrictedRoots = roots, StartupFolder = startupFolder };
    }

    /// <summary>Raízes exibidas no navegador de pastas.</summary>
    public IReadOnlyList<BrowseRoot> BrowseRoots()
    {
        if (RestrictedRoots is { Count: > 0 })
        {
            return RestrictedRoots.Where(Directory.Exists)
                .Select(r => new BrowseRoot(System.IO.Path.GetFileName(r.TrimEnd('/', '\\')) is { Length: > 0 } n ? n : r, r, "root"))
                .ToList();
        }

        var list = new List<BrowseRoot>();
        void AddFolder(Environment.SpecialFolder folder, string name)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) list.Add(new BrowseRoot(name, path, "folder"));
        }

        if (OperatingSystem.IsWindows())
        {
            AddFolder(Environment.SpecialFolder.MyVideos, "Vídeos");
            var downloads = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads)) list.Add(new BrowseRoot("Downloads", downloads, "folder"));
            AddFolder(Environment.SpecialFolder.Desktop, "Área de trabalho");
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disco" : drive.VolumeLabel;
                    list.Add(new BrowseRoot($"{label} ({drive.Name.TrimEnd('\\')})", drive.RootDirectory.FullName, "drive"));
                }
                catch { /* unidade indisponível */ }
            }
        }
        else
        {
            AddFolder(Environment.SpecialFolder.UserProfile, "Início");
            foreach (var p in new[] { "/media", "/mnt", "/srv", "/" })
                if (Directory.Exists(p)) list.Add(new BrowseRoot(p, p, p == "/" ? "drive" : "root"));
        }
        return list;
    }
}

/// <summary>Configurações persistidas em settings.json.</summary>
public sealed class AppSettings
{
    public string? TmdbKey { get; set; }
    public string TmdbLanguage { get; set; } = "pt-BR";
    /// <summary>Qual título usar ao escolher um resultado do TMDB: en, original ou pt.</summary>
    public string TitlePreference { get; set; } = "en";
    public string DefaultGroup { get; set; } = "";
    public bool DefaultDots { get; set; } = true;
    public string? MediaInfoPath { get; set; }
    public List<string> Recent { get; set; } = [];
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly Lock _lock = new();
    private AppSettings _current;

    public SettingsStore(AppEnvironment env)
    {
        _path = Path.Combine(env.ConfigDir, "settings.json");
        _current = Load();
    }

    public AppSettings Current { get { lock (_lock) return Clone(_current); } }

    /// <summary>Chave efetiva do TMDB (variável de ambiente tem prioridade).</summary>
    public (string? Key, string? Source) TmdbKey()
    {
        var env = Environment.GetEnvironmentVariable("TMDB_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return (env.Trim(), "env");
        var key = Current.TmdbKey;
        return string.IsNullOrWhiteSpace(key) ? (null, null) : (key, "settings");
    }

    public AppSettings Update(Action<AppSettings> change)
    {
        lock (_lock)
        {
            var copy = Clone(_current);
            change(copy);
            copy.Recent = copy.Recent.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(copy, Json));
            File.Move(tmp, _path, overwrite: true);
            _current = copy;
            return Clone(copy);
        }
    }

    public void AddRecent(string path) => Update(s =>
    {
        s.Recent.RemoveAll(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
        s.Recent.Insert(0, path);
    });

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Json) ?? new AppSettings();
        }
        catch { /* arquivo corrompido: volta ao padrão */ }
        return new AppSettings();
    }

    private static AppSettings Clone(AppSettings s) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s, Json), Json)!;
}
