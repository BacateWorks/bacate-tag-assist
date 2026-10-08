using System.Collections.Concurrent;
using BacateTagAssist.Core.Media;

namespace BacateTagAssist.Web;

/// <summary>Recursos que só existem no app desktop do Windows.</summary>
public interface IDesktopBridge
{
    /// <summary>Abre o seletor de pastas nativo. Retorna null se cancelado.</summary>
    Task<string?> PickFolderAsync();

    /// <summary>Abre a pasta no Explorador de Arquivos.</summary>
    void Reveal(string path);

    bool ExplorerIntegrationEnabled { get; }

    /// <summary>Adiciona/remove "Renomear com BacateTagAssist" no menu de contexto do Explorer.</summary>
    void SetExplorerIntegration(bool enabled);
}

public sealed class BacateWebOptions
{
    public string[] Args { get; init; } = [];
    public Core.Config.AppMode Mode { get; init; } = Core.Config.AppMode.Server;
    public string? Urls { get; init; }
    public IDesktopBridge? Desktop { get; init; }
    public string? StartupFolder { get; init; }
}

/// <summary>Guarda as últimas varreduras para a prévia não precisar reenviar milhares de arquivos.</summary>
public sealed class ScanCache
{
    private readonly ConcurrentDictionary<string, ScanResult> _items = new();
    private readonly ConcurrentQueue<string> _order = new();

    public void Put(ScanResult scan)
    {
        _items[scan.ScanId] = scan;
        _order.Enqueue(scan.ScanId);
        while (_order.Count > 16 && _order.TryDequeue(out var old)) _items.TryRemove(old, out _);
    }

    public ScanResult? Get(string? id) => id is not null && _items.TryGetValue(id, out var s) ? s : null;
}
