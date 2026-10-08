using System.Text.Json;
using BacateTagAssist.Core.Config;
using BacateTagAssist.Core.FileSystem;
using BacateTagAssist.Core.Media;
using BacateTagAssist.Core.Naming;
using BacateTagAssist.Core.Rename;
using BacateTagAssist.Core.Tmdb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BacateTagAssist.Web;

public static class BacateHost
{
    public static WebApplication CreateApp(BacateWebOptions options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = options.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Configuração de ambiente e serviços essenciais
        var env = AppEnvironment.Create(options.Mode, options.StartupFolder);
        builder.Services.AddSingleton(env);
        builder.Services.AddSingleton<SettingsStore>();
        builder.Services.AddSingleton<PathGuard>();
        builder.Services.AddSingleton<DirectoryBrowser>();
        builder.Services.AddSingleton<HistoryStore>();
        builder.Services.AddSingleton<RenameService>();
        builder.Services.AddSingleton<MediaInfoService>();
        builder.Services.AddSingleton<TmdbClient>();
        builder.Services.AddSingleton<ScanCache>();

        if (options.Desktop is not null)
        {
            builder.Services.AddSingleton(options.Desktop);
        }

        builder.Services.AddHttpClient();

        if (!string.IsNullOrWhiteSpace(options.Urls))
        {
            builder.WebHost.UseUrls(options.Urls);
        }

        var app = builder.Build();

        ConfigureRoutes(app, options);

        return app;
    }

    private static void ConfigureRoutes(WebApplication app, BacateWebOptions options)
    {
        var env = app.Services.GetRequiredService<AppEnvironment>();
        var settings = app.Services.GetRequiredService<SettingsStore>();
        var guard = app.Services.GetRequiredService<PathGuard>();
        var browser = app.Services.GetRequiredService<DirectoryBrowser>();
        var renameService = app.Services.GetRequiredService<RenameService>();
        var mediaInfo = app.Services.GetRequiredService<MediaInfoService>();
        var tmdb = app.Services.GetRequiredService<TmdbClient>();
        var scanCache = app.Services.GetRequiredService<ScanCache>();
        var desktop = app.Services.GetService<IDesktopBridge>();

        var api = app.MapGroup("/api");

        // -------------------------------------------------------------
        // Status & Sistema
        // -------------------------------------------------------------
        api.MapGet("/status", async () =>
        {
            var miStatus = await mediaInfo.StatusAsync();
            var (tmdbKey, tmdbSrc) = settings.TmdbKey();

            return Results.Ok(new
            {
                version = AppEnvironment.Version,
                mode = env.Mode.ToString().ToLowerInvariant(),
                inContainer = AppEnvironment.InContainer,
                isDesktop = desktop is not null,
                mediaInfo = miStatus,
                tmdb = new
                {
                    configured = tmdbKey is not null,
                    source = tmdbSrc,
                    language = settings.Current.TmdbLanguage,
                    titlePreference = settings.Current.TitlePreference,
                },
                browseRoots = env.BrowseRoots(),
                recent = settings.Current.Recent,
                defaultGroup = settings.Current.DefaultGroup,
                defaultDots = settings.Current.DefaultDots,
                startupFolder = options.StartupFolder,
            });
        });

        // -------------------------------------------------------------
        // Seleção e Navegação de Pastas
        // -------------------------------------------------------------
        api.MapPost("/select-folder", async () =>
        {
            if (desktop is not null)
            {
                var picked = await desktop.PickFolderAsync();
                if (!string.IsNullOrEmpty(picked))
                {
                    settings.AddRecent(picked);
                    return Results.Ok(new { path = picked });
                }
                return Results.Ok(new { path = (string?)null });
            }

            return Results.Json(new { error = "O seletor nativo só está disponível no modo desktop. Use a navegação web integrada." }, statusCode: 400);
        });

        api.MapGet("/browse", (string? path) =>
        {
            try
            {
                var target = string.IsNullOrWhiteSpace(path)
                    ? env.BrowseRoots().FirstOrDefault()?.Path ?? (OperatingSystem.IsWindows() ? "C:\\" : "/")
                    : path;

                var listing = browser.List(target);
                return Results.Ok(listing);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });

        api.MapPost("/reveal", (PathPayload payload) =>
        {
            if (desktop is not null && !string.IsNullOrWhiteSpace(payload.Path))
            {
                desktop.Reveal(payload.Path);
                return Results.Ok(new { success = true });
            }
            return Results.Json(new { error = "Abertura no explorador disponível apenas no modo desktop." }, statusCode: 400);
        });

        // -------------------------------------------------------------
        // Varredura de Mídia (Scan)
        // -------------------------------------------------------------
        api.MapPost("/scan", (PathPayload payload) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Path))
                return Results.Json(new { error = "Informe o caminho da pasta." }, statusCode: 400);

            try
            {
                var full = guard.Resolve(payload.Path);
                var isLocked = guard.IsLocked(full);
                var scan = MediaScanner.Scan(full, isLocked);

                scanCache.Put(scan);
                settings.AddRecent(full);

                return Results.Ok(scan);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });

        // -------------------------------------------------------------
        // Prévia e Validação de Nomenclatura
        // -------------------------------------------------------------
        api.MapPost("/preview", (PreviewRequest req) =>
        {
            try
            {
                var scan = scanCache.Get(req.ScanId);
                var mode = Enum.TryParse<ReleaseMode>(req.Mode, true, out var m) ? m : ReleaseMode.Movie;
                var options = new NamingOptions(mode, req.Dots, req.TwoGroups);
                var result = PreviewService.Build(req.Fields, options, scan);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });

        // -------------------------------------------------------------
        // Aplicação de Renomeação & Desfazer
        // -------------------------------------------------------------
        api.MapPost("/apply", async (RenameRequest req) =>
        {
            try
            {
                var result = await renameService.ApplyAsync(req);
                return Results.Ok(result);
            }
            catch (RenameException ex)
            {
                return Results.Json(new { error = ex.Message, details = ex.Details }, statusCode: 400);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        api.MapPost("/undo", async () =>
        {
            try
            {
                var result = await renameService.UndoAsync();
                return Results.Ok(result);
            }
            catch (RenameException ex)
            {
                return Results.Json(new { error = ex.Message, details = ex.Details }, statusCode: 400);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        // -------------------------------------------------------------
        // MediaInfo (Análise e Instalação)
        // -------------------------------------------------------------
        api.MapGet("/mediainfo/status", async () =>
        {
            var status = await mediaInfo.StatusAsync();
            return Results.Ok(status);
        });

        api.MapPost("/mediainfo/install", async (CancellationToken ct) =>
        {
            try
            {
                var status = await mediaInfo.InstallAsync(ct);
                return Results.Ok(status);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        api.MapPost("/mediainfo/analyze", async (PathPayload payload, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Path))
                return Results.Json(new { error = "Informe o arquivo para análise." }, statusCode: 400);

            try
            {
                var full = guard.Resolve(payload.Path);
                var report = await mediaInfo.AnalyzeAsync(full, ct);
                return Results.Ok(report);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });

        // -------------------------------------------------------------
        // TMDB (Busca & Detalhes)
        // -------------------------------------------------------------
        api.MapGet("/tmdb/search", async (string query, string? type, CancellationToken ct) =>
        {
            try
            {
                var search = await tmdb.SearchAsync(query, type ?? "movie", ct);
                return Results.Ok(search);
            }
            catch (TmdbException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: (int)ex.Status);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        api.MapGet("/tmdb/details", async (string type, int id, CancellationToken ct) =>
        {
            try
            {
                var details = await tmdb.DetailsAsync(type, id, ct);
                return Results.Ok(details);
            }
            catch (TmdbException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: (int)ex.Status);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        // -------------------------------------------------------------
        // Configurações (Settings)
        // -------------------------------------------------------------
        api.MapGet("/settings", () => Results.Ok(settings.Current));

        api.MapPost("/settings", async (UpdateSettingsPayload req) =>
        {
            var updated = settings.Update(s =>
            {
                if (req.TmdbKey is not null) s.TmdbKey = string.IsNullOrWhiteSpace(req.TmdbKey) ? null : req.TmdbKey.Trim();
                if (req.TmdbLanguage is not null) s.TmdbLanguage = req.TmdbLanguage.Trim();
                if (req.TitlePreference is not null) s.TitlePreference = req.TitlePreference.Trim();
                if (req.DefaultGroup is not null) s.DefaultGroup = req.DefaultGroup.Trim();
                if (req.DefaultDots.HasValue) s.DefaultDots = req.DefaultDots.Value;
                if (req.MediaInfoPath is not null) s.MediaInfoPath = string.IsNullOrWhiteSpace(req.MediaInfoPath) ? null : req.MediaInfoPath.Trim();
            });

            bool? tmdbValid = null;
            if (!string.IsNullOrWhiteSpace(updated.TmdbKey))
            {
                var (ok, _) = await tmdb.TestAsync(updated.TmdbKey);
                tmdbValid = ok;
            }

            return Results.Ok(new { settings = updated, tmdbValid });
        });

        // -------------------------------------------------------------
        // Arquivos Estáticos & SPA Fallback
        // -------------------------------------------------------------
        var embeddedProvider = new ManifestEmbeddedFileProvider(typeof(BacateHost).Assembly, "wwwroot");

        // Permite servir tanto arquivos embutidos quanto arquivos físicos caso estejam na pasta de execução
        var physicalPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        IFileProvider fileProvider = Directory.Exists(physicalPath)
            ? new CompositeFileProvider(new PhysicalFileProvider(physicalPath), embeddedProvider)
            : embeddedProvider;

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

        app.MapFallback(async context =>
        {
            var file = fileProvider.GetFileInfo("index.html");
            if (file.Exists)
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await using var stream = file.CreateReadStream();
                await stream.CopyToAsync(context.Response.Body);
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Página não encontrada.");
            }
        });
    }

    public sealed record PathPayload(string? Path);

    public sealed record PreviewRequest(
        string? ScanId,
        ReleaseFields Fields,
        string Mode,
        bool Dots,
        bool TwoGroups);

    public sealed record UpdateSettingsPayload(
        string? TmdbKey,
        string? TmdbLanguage,
        string? TitlePreference,
        string? DefaultGroup,
        bool? DefaultDots,
        string? MediaInfoPath);
}
