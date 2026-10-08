using System.Text.Json;
using BacateTagAssist.Core.Config;
using BacateTagAssist.Core.FileSystem;

namespace BacateTagAssist.Core.Rename;

public sealed record RenameChange(string Source, string NewName);
public sealed record RenameRequest(string Root, string? NewRootName, IReadOnlyList<RenameChange> Changes);
public sealed record RenameResult(string Root, int Renamed, bool FolderRenamed, string OperationId);
public sealed record UndoResult(string Root, int Restored, bool FolderRestored);

public sealed record RenamedItem(string From, string To);

public sealed record HistoryEntry
{
    public required string Id { get; init; }
    public required DateTimeOffset At { get; init; }
    public required string RootBefore { get; init; }
    public required string RootAfter { get; init; }
    public required IReadOnlyList<RenamedItem> Items { get; init; }
    public bool Undone { get; init; }
}

public sealed class RenameException(string message, IReadOnlyList<string>? details = null) : Exception(message)
{
    public IReadOnlyList<string> Details { get; } = details ?? [];
}

/// <summary>Histórico persistido das operações (para "Desfazer").</summary>
public sealed class HistoryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;
    private readonly Lock _lock = new();
    private List<HistoryEntry> _entries;

    public HistoryStore(AppEnvironment env)
    {
        _path = Path.Combine(env.ConfigDir, "history.json");
        try { _entries = File.Exists(_path) ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_path), Json) ?? [] : []; }
        catch { _entries = []; }
    }

    public IReadOnlyList<HistoryEntry> All() { lock (_lock) return _entries.ToList(); }

    public HistoryEntry? LastUndoable() { lock (_lock) return _entries.FirstOrDefault(e => !e.Undone); }

    public void Add(HistoryEntry entry) => Mutate(list => list.Insert(0, entry));

    public void MarkUndone(string id) => Mutate(list =>
    {
        var i = list.FindIndex(e => e.Id == id);
        if (i >= 0) list[i] = list[i] with { Undone = true };
    });

    public void Clear() => Mutate(list => list.Clear());

    private void Mutate(Action<List<HistoryEntry>> change)
    {
        lock (_lock)
        {
            change(_entries);
            if (_entries.Count > 50) _entries = _entries.Take(50).ToList();
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Json));
            File.Move(tmp, _path, overwrite: true);
        }
    }
}

/// <summary>
/// Renomeia arquivos em duas fases (origem → temporário → destino) para permitir trocas
/// e renomeações só de maiúsculas/minúsculas, com rollback em caso de falha.
/// </summary>
public sealed class RenameService(PathGuard guard, HistoryStore history)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly char[] InvalidNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public async Task<RenameResult> ApplyAsync(RenameRequest request)
    {
        await Gate.WaitAsync();
        try { return Apply(request); }
        finally { Gate.Release(); }
    }

    public async Task<UndoResult> UndoAsync()
    {
        await Gate.WaitAsync();
        try { return Undo(); }
        finally { Gate.Release(); }
    }

    private RenameResult Apply(RenameRequest request)
    {
        var root = guard.Resolve(request.Root);
        if (!Directory.Exists(root)) throw new RenameException("A pasta principal não existe mais. Carregue novamente.");

        // ---------- Validação completa antes de tocar em qualquer arquivo ----------
        var plan = new List<(string Src, string Dest, string RelFrom, string RelTo)>();
        var problems = new List<string>();

        foreach (var change in request.Changes)
        {
            var src = Path.GetFullPath(Path.Combine(root, change.Source));
            if (!PathGuard.IsWithin(src, root)) { problems.Add($"{change.Source}: caminho fora da pasta."); continue; }
            if (!File.Exists(src)) { problems.Add($"{change.Source}: arquivo não encontrado."); continue; }

            var ext = Path.GetExtension(src);
            var name = (change.NewName ?? "").Trim().TrimEnd('.', ' ');
            if (name.Length == 0) { problems.Add($"{change.Source}: novo nome vazio."); continue; }
            if (name.IndexOfAny(InvalidNameChars) >= 0 || name.Any(char.IsControl))
            {
                problems.Add($"{change.Source}: o novo nome contém caracteres inválidos (< > : \" / \\ | ? *).");
                continue;
            }
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) name += ext.ToLowerInvariant();
            if (name.Length > 250) { problems.Add($"{change.Source}: novo nome longo demais ({name.Length} caracteres)."); continue; }

            var dest = Path.Combine(Path.GetDirectoryName(src)!, name);
            if (string.Equals(src, dest, StringComparison.Ordinal)) continue;
            plan.Add((src, dest, Path.GetRelativePath(root, src), Path.GetRelativePath(root, dest)));
        }

        foreach (var dup in plan.GroupBy(p => p.Dest, PathComparer).Where(g => g.Count() > 1))
            problems.Add($"Mais de um arquivo ficaria com o nome {Path.GetFileName(dup.Key)}.");

        var sources = plan.Select(p => p.Src).ToHashSet(PathComparer);
        foreach (var p in plan)
        {
            if ((File.Exists(p.Dest) || Directory.Exists(p.Dest)) && !sources.Contains(p.Dest))
                problems.Add($"Já existe um arquivo chamado {Path.GetFileName(p.Dest)}.");
        }

        string? newRoot = null;
        var newRootName = (request.NewRootName ?? "").Trim().TrimEnd('.', ' ');
        var currentRootName = Path.GetFileName(root.TrimEnd('\\', '/'));
        if (newRootName.Length > 0 && !string.Equals(newRootName, currentRootName, StringComparison.Ordinal))
        {
            if (guard.IsLocked(root)) problems.Add("Esta pasta é uma unidade ou raiz liberada e não pode ser renomeada.");
            else if (newRootName.IndexOfAny(InvalidNameChars) >= 0) problems.Add("O nome da pasta contém caracteres inválidos.");
            else
            {
                newRoot = Path.Combine(Path.GetDirectoryName(root.TrimEnd('\\', '/'))!, newRootName);
                if (Directory.Exists(newRoot) && !PathGuard.SamePath(newRoot, root) && !SameDirectoryIgnoringCase(newRoot, root))
                    problems.Add($"Já existe uma pasta chamada {newRootName} ao lado da atual.");
                else
                {
                    try { guard.Resolve(newRoot); }
                    catch (AccessDeniedException) { problems.Add("O novo nome da pasta ficaria fora das pastas liberadas."); }
                }
            }
        }

        if (problems.Count > 0) throw new RenameException("Nada foi alterado: corrija os itens abaixo.", problems.Distinct().ToList());
        if (plan.Count == 0 && newRoot is null) throw new RenameException("Não há alterações para aplicar.");

        // ---------- Execução ----------
        var opId = Guid.NewGuid().ToString("N")[..12];
        MoveFilesTwoPhase(plan.Select(p => (p.Src, p.Dest)).ToList(), opId);

        var finalRoot = root;
        if (newRoot is not null)
        {
            try
            {
                MoveDirectory(root, newRoot, opId);
                finalRoot = newRoot;
            }
            catch (Exception ex)
            {
                // Desfaz os arquivos para não deixar a operação pela metade.
                try { MoveFilesTwoPhase(plan.Select(p => (p.Dest, p.Src)).ToList(), opId + "r"); } catch { /* melhor esforço */ }
                throw new RenameException($"Não foi possível renomear a pasta: {ex.Message}. Os arquivos foram restaurados.");
            }
        }

        history.Add(new HistoryEntry
        {
            Id = opId,
            At = DateTimeOffset.Now,
            RootBefore = root,
            RootAfter = finalRoot,
            Items = plan.Select(p => new RenamedItem(p.RelFrom, p.RelTo)).ToList(),
        });

        return new RenameResult(finalRoot, plan.Count, newRoot is not null, opId);
    }

    private UndoResult Undo()
    {
        var entry = history.LastUndoable() ?? throw new RenameException("Não há operação para desfazer.");
        var rootAfter = guard.Resolve(entry.RootAfter);
        var rootBefore = guard.Resolve(entry.RootBefore);
        if (!Directory.Exists(rootAfter))
            throw new RenameException($"A pasta {rootAfter} não foi encontrada. Ela foi movida ou renomeada depois da operação?");

        var plan = entry.Items
            .Select(i => (Src: Path.Combine(rootBefore, i.To), Dest: Path.Combine(rootBefore, i.From)))
            .ToList();

        var folderMoved = !PathGuard.SamePath(rootAfter, rootBefore) || !string.Equals(rootAfter, rootBefore, StringComparison.Ordinal);
        if (folderMoved && Directory.Exists(rootBefore) && !SameDirectoryIgnoringCase(rootAfter, rootBefore))
            throw new RenameException($"Já existe uma pasta em {rootBefore}; não é possível restaurar o nome anterior.");

        // Valida arquivos (considerando o caminho atual da pasta).
        var missing = entry.Items.Where(i => !File.Exists(Path.Combine(rootAfter, i.To))).Select(i => i.To).ToList();
        if (missing.Count > 0)
            throw new RenameException("Alguns arquivos não estão mais onde estavam. Nada foi alterado.", missing.Take(20).ToList());

        var targets = plan.Select(p => p.Src).ToHashSet(PathComparer);
        var occupied = entry.Items.Where(i => File.Exists(Path.Combine(rootAfter, i.From)) && !targets.Contains(Path.Combine(rootBefore, i.From)))
            .Select(i => i.From).ToList();
        if (occupied.Count > 0)
            throw new RenameException("Outros arquivos já ocupam os nomes anteriores. Nada foi alterado.", occupied.Take(20).ToList());

        var opId = entry.Id + "u";
        if (folderMoved) MoveDirectory(rootAfter, rootBefore, opId);
        try
        {
            MoveFilesTwoPhase(plan, opId);
        }
        catch
        {
            if (folderMoved) try { MoveDirectory(rootBefore, rootAfter, opId + "r"); } catch { /* melhor esforço */ }
            throw;
        }

        history.MarkUndone(entry.Id);
        return new UndoResult(rootBefore, plan.Count, folderMoved);
    }

    private static void MoveFilesTwoPhase(IReadOnlyList<(string Src, string Dest)> moves, string opId)
    {
        var temps = new List<(string Src, string Temp, string Dest)>();
        try
        {
            for (var i = 0; i < moves.Count; i++)
            {
                var (src, dest) = moves[i];
                var temp = Path.Combine(Path.GetDirectoryName(src)!, $".bta-{opId}-{i}.tmp");
                File.Move(src, temp);
                temps.Add((src, temp, dest));
            }
        }
        catch (Exception ex)
        {
            foreach (var t in temps) try { File.Move(t.Temp, t.Src); } catch { /* melhor esforço */ }
            throw new RenameException($"Falha ao renomear: {ex.Message}. Nenhuma alteração foi mantida.");
        }

        var done = new List<(string Src, string Temp, string Dest)>();
        try
        {
            foreach (var t in temps)
            {
                File.Move(t.Temp, t.Dest);
                done.Add(t);
            }
        }
        catch (Exception ex)
        {
            foreach (var t in done) try { File.Move(t.Dest, t.Temp); } catch { /* melhor esforço */ }
            foreach (var t in temps) try { File.Move(t.Temp, t.Src); } catch { /* melhor esforço */ }
            throw new RenameException($"Falha ao renomear: {ex.Message}. Os nomes anteriores foram restaurados.");
        }
    }

    private static void MoveDirectory(string from, string to, string opId)
    {
        var a = from.TrimEnd('\\', '/');
        var b = to.TrimEnd('\\', '/');
        if (SameDirectoryIgnoringCase(a, b) && !string.Equals(a, b, StringComparison.Ordinal))
        {
            var temp = Path.Combine(Path.GetDirectoryName(a)!, $".bta-{opId}.dir");
            Directory.Move(a, temp);
            Directory.Move(temp, b);
            return;
        }
        Directory.Move(a, b);
    }

    private static bool SameDirectoryIgnoringCase(string a, string b) =>
        OperatingSystem.IsWindows() && string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
