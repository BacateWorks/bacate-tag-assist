using BacateTagAssist.Core.Media;

namespace BacateTagAssist.Core.Naming;

public sealed record Issue(string Level, string Field, string Message);

public sealed record FilePreview(string RelPath, string NewName, string Id, string Status);

public sealed record FolderPreview(string Name, IReadOnlyList<NameToken> Tokens, string Current, bool Locked);

public sealed record PreviewResult(
    FolderPreview Folder,
    IReadOnlyList<FilePreview> Files,
    IReadOnlyList<Issue> Issues,
    string PostTitle,
    PreviewStats Stats);

public sealed record PreviewStats(int Total, int Ready, int Pending, int Conflicts, int Unchanged);

/// <summary>Calcula a prévia completa (pasta + arquivos) e as validações do formulário.</summary>
public static class PreviewService
{
    public static PreviewResult Build(ReleaseFields rawFields, NamingOptions options, ScanResult? scan)
    {
        var fields = rawFields.Normalized();
        var files = scan?.Files ?? [];
        var firstDetected = files.FirstOrDefault(f => f.Kind == "video" && f.SeasonEpisode is not null)?.SeasonEpisode
                            ?? files.FirstOrDefault(f => f.SeasonEpisode is not null)?.SeasonEpisode;

        var folderId = NamingEngine.FolderIdentifier(fields, options.Mode, firstDetected);
        var folder = NamingEngine.Build(fields, options, folderId);
        var post = NamingEngine.Build(fields, options with { Dots = false }, folderId).Name;

        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        var previews = new List<FilePreview>(files.Count);
        foreach (var file in files)
        {
            var id = NamingEngine.EpisodeId(file.SeasonEpisode, fields, options.Mode);
            var pending = options.Mode != ReleaseMode.Movie && id.Length == 0;
            if (!cache.TryGetValue(id, out var baseName))
            {
                baseName = NamingEngine.Build(fields, options, id).Name;
                cache[id] = baseName;
            }
            var suffix = file.LangSuffix is { Length: > 0 } ? "." + file.LangSuffix : "";
            var newName = baseName + suffix + file.Extension.ToLowerInvariant();
            var status = pending ? "pending" : string.Equals(newName, file.Name, StringComparison.Ordinal) ? "unchanged" : "ok";
            previews.Add(new FilePreview(file.RelPath, newName, id, status));
        }

        // Nomes duplicados dentro da mesma pasta.
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var duplicates = previews
            .Where(p => p.Status != "pending")
            .GroupBy(p => Path.Combine(Path.GetDirectoryName(p.RelPath) ?? "", p.NewName), comparer)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(p => p.RelPath))
            .ToHashSet(StringComparer.Ordinal);
        if (duplicates.Count > 0)
            previews = previews.Select(p => duplicates.Contains(p.RelPath) ? p with { Status = "conflict" } : p).ToList();

        var stats = new PreviewStats(
            previews.Count,
            previews.Count(p => p.Status == "ok"),
            previews.Count(p => p.Status == "pending"),
            previews.Count(p => p.Status == "conflict"),
            previews.Count(p => p.Status == "unchanged"));

        var issues = Validate(fields, options, scan, stats);
        var folderPreview = new FolderPreview(folder.Name, folder.Tokens, scan?.RootName ?? "", scan?.IsRootLocked ?? false);
        return new PreviewResult(folderPreview, previews, issues, post, stats);
    }

    public static List<Issue> Validate(ReleaseFields v, NamingOptions options, ScanResult? scan, PreviewStats? stats)
    {
        var issues = new List<Issue>();
        void Error(string field, string msg) => issues.Add(new Issue("error", field, msg));
        void Warn(string field, string msg) => issues.Add(new Issue("warning", field, msg));
        void Info(string field, string msg) => issues.Add(new Issue("info", field, msg));

        if (scan is null) Info("folder", "Nenhuma pasta carregada: a prévia mostra apenas o nome montado.");

        if (v.Title.Length == 0) Error("title", "Informe o nome da obra.");

        var yearOk = System.Text.RegularExpressions.Regex.IsMatch(v.Year, @"^(18|19|20)\d{2}$");
        if (options.Mode == ReleaseMode.Movie && v.Year.Length == 0) Error("year", "Ano é obrigatório para filmes (consulte o IMDb).");
        else if (v.Year.Length > 0 && !yearOk) Error("year", "Ano inválido: use quatro dígitos, como 2008.");

        if (options.Mode == ReleaseMode.Season && !NamingEngine.IsValidSeason(v.Season))
            Error("season", "Informe a temporada no formato S01.");

        if (options.Mode == ReleaseMode.Episode)
        {
            var detected = scan?.Files.FirstOrDefault(f => f.Kind == "video")?.SeasonEpisode;
            if (v.Episode.Length > 0 && !NamingEngine.IsValidEpisode(v.Episode))
                Error("episode", "Episódio no formato S01E01 (ou S01E01-E02).");
            else if (v.Episode.Length == 0 && NamingEngine.EpisodeId(detected, v, ReleaseMode.Episode).Length == 0)
                Error("episode", "Não foi possível identificar o episódio: preencha como S01E01.");
        }

        if (v.Source.Length == 0) Warn("source", "Origem não informada (WEB-DL, BluRay, DVD…).");

        var streaming = v.EffectiveStreaming;
        if (v.Streaming == "custom" && streaming.Length == 0) Warn("customStreaming", "Informe a sigla do streaming personalizado.");
        if (streaming.Length > 0 && v.Source.Length > 0 && !NamingEngine.IsWebSource(v.Source))
            Warn("streaming", "A sigla de streaming só entra em releases WEB-DL/WEBRip e será ignorada.");
        if (NamingEngine.IsWebSource(v.Source) && streaming.Length == 0)
            Info("streaming", "Releases WEB normalmente indicam o serviço (NF, AMZN, CR…).");

        if (v.DiscCount.Length > 0 && !NamingEngine.IsDiscCountSource(v.Source))
            Warn("discCount", "Quantidade de discos só é usada com DVD5/DVD9 e será ignorada.");
        if (v.Resolution.Length > 0 && NamingEngine.IsDvdSource(v.Source))
            Info("resolution", "Em DVD a resolução não entra no nome.");

        if (v.ReleaseType == "remux" && v.Source.Length > 0 && !(v.Source is "Blu-Ray" or "UHD Blu-Ray" or "DVD" or "DVD5" or "DVD9"))
            Warn("releaseType", "REMUX normalmente vem de Blu-Ray, UHD Blu-Ray ou DVD.");

        if (v.Channels.Length > 0 && v.AudioCodec.Length == 0)
            Warn("channels", "Canais sem codec de áudio não entram no nome.");
        if (v.Atmos && !(v.AudioCodec is "TrueHD" or "DDP"))
            Warn("atmos", "Atmos normalmente acompanha TrueHD ou DDP.");

        if (options.TwoGroups)
        {
            if (v.OriginalGroup.Length == 0) Warn("originalGroup", "Informe o grupo original para usar dois grupos.");
            if (v.DualMulti.Length == 0) Warn("dualMulti", "Dois grupos só aparecem no nome com DUAL ou MULTi.");
        }

        if (v.Group.Length == 0) Info("group", "Sem grupo informado: será usado NoGroup.");

        if (stats is not null && scan is not null)
        {
            if (stats.Conflicts > 0) Error("files", $"{stats.Conflicts} arquivo(s) ficariam com nomes iguais. Revise as linhas marcadas.");
            if (stats.Pending > 0) Warn("files", $"{stats.Pending} arquivo(s) sem episódio identificado ficarão desmarcados para revisão.");
            if (scan.Truncated) Warn("folder", $"A pasta tem mais de {MediaScanner.MaxFiles} arquivos; apenas os primeiros foram carregados.");
            if (scan.IsRootLocked) Info("folder", "Esta pasta é uma raiz/unidade e não será renomeada; apenas os arquivos.");
        }

        return issues;
    }
}
