using System.Diagnostics;
using BacateTagAssist.Core.Config;
using BacateTagAssist.Web;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace BacateTagAssist.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
        for (var i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length)
            {
                port = args[i + 1];
            }
        }

        var hostUrl = Environment.GetEnvironmentVariable("BTA_URLS") ?? $"http://0.0.0.0:{port}";
        var noBrowser = args.Contains("--no-browser") || AppEnvironment.InContainer;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"
    ____                  __        ______            ___             _      __ 
   / __ )____ __________ _/ /____   /_  __/___ _____ _/   |  __________(_)____/ /_
  / __  / __ `/ ___/ __ `/ __/ _ \   / / / __ `/ __ `/ /| | / ___/ ___/ / ___/ __/
 / /_/ / /_/ / /__/ /_/ / /_/  __/  / / / /_/ / /_/ / ___ |(__  |__  ) (__  ) /_  
/_____/\__,_/\___/\__,_/\__/\___/  /_/  \__,_/\__, /_/  |_/____/____/_/____/\__/  
                                             /____/                               
        ");
        Console.ResetColor();

        Console.WriteLine($"[BTA] Versão: {AppEnvironment.Version} (Modo Servidor / Docker / NAS)");
        Console.WriteLine($"[BTA] Escutando em: {hostUrl}");

        var options = new BacateWebOptions
        {
            Args = args,
            Mode = AppMode.Server,
            Urls = hostUrl,
            Desktop = null,
            StartupFolder = args.FirstOrDefault(a => !a.StartsWith('-')),
        };

        var app = BacateHost.CreateApp(options);

        if (!noBrowser && OperatingSystem.IsWindows())
        {
            try
            {
                Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true });
            }
            catch { /* Ignora falha ao abrir navegador */ }
        }

        await app.RunAsync();
    }
}
