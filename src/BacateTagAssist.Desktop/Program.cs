using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using BacateTagAssist.Core.Config;
using BacateTagAssist.Web;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace BacateTagAssist.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var port = GetFreePort();
        var localUrl = $"http://127.0.0.1:{port}";

        var bridge = new WindowsDesktopBridge();

        var startupFolder = args.FirstOrDefault(a => !a.StartsWith('-'));

        var webOptions = new BacateWebOptions
        {
            Args = args,
            Mode = AppMode.Desktop,
            Urls = localUrl,
            Desktop = bridge,
            StartupFolder = startupFolder,
        };

        var app = BacateHost.CreateApp(webOptions);

        using var cts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            try { await Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.RunAsync(app, cts.Token); }
            catch (OperationCanceledException) { /* encerramento esperado */ }
        });

        using var form = new MainForm(localUrl, bridge);
        bridge.SetOwner(form);

        Application.Run(form);

        cts.Cancel();
        try { serverTask.Wait(TimeSpan.FromSeconds(3)); }
        catch { /* encerramento seguro */ }
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}

public sealed class MainForm : Form
{
    private readonly string _url;
    private readonly WebView2 _webView;

    public MainForm(string url, WindowsDesktopBridge bridge)
    {
        _url = url;
        Text = "BacateTagAssist - Nomenclatura para Trackers";
        Width = 1280;
        Height = 820;
        MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
        {
            try { Icon = new Icon(iconPath); } catch { /* ícone fallback */ }
        }

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(Path.GetTempPath(), "BacateTagAssist_WebView2"));
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

            _webView.Source = new Uri(_url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível inicializar o Microsoft Edge WebView2.\n\nDetalhes: {ex.Message}\n\nInstale o WebView2 Runtime pelo site da Microsoft.",
                "BacateTagAssist",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

public sealed class WindowsDesktopBridge : IDesktopBridge
{
    private Form? _owner;

    public void SetOwner(Form form) => _owner = form;

    public Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        if (_owner is not null && _owner.InvokeRequired)
        {
            _owner.BeginInvoke(new Action(() => tcs.SetResult(ShowDialog())));
        }
        else
        {
            tcs.SetResult(ShowDialog());
        }

        return tcs.Task;
    }

    private string? ShowDialog()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Selecione a pasta do release audiovisual",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        var result = _owner is not null ? dlg.ShowDialog(_owner) : dlg.ShowDialog();
        return result == DialogResult.OK ? dlg.SelectedPath : null;
    }

    public void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
        }
        catch { /* ignora falha */ }
    }

    public bool ExplorerIntegrationEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\BacateTagAssist");
                return key is not null;
            }
            catch { return false; }
        }
    }

    public void SetExplorerIntegration(bool enabled)
    {
        var keyPath = @"Software\Classes\Directory\shell\BacateTagAssist";
        try
        {
            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue("", "Renomear com BacateTagAssist");
                key.SetValue("Icon", exePath);

                using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{keyPath}\command");
                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
            }
        }
        catch { /* permissão */ }
    }
}
