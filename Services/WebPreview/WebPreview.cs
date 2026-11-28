using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ESPWebBridge.Services.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace ESPWebBridge.Services.WebPreview;

public sealed class RoutingOption : OptionGroup
{
    public RoutingOption(IReadOnlyList<string> destination_ids, string defaultPathRegex = "", int defaultDestinationIdIndex = 0)
        : this(
            new TextOption("Path pattern", defaultValue: defaultPathRegex),
            new ComboBoxOption("Method", ["any", "GET", "POST"]),
            new ComboBoxOption("Destination", destination_ids, defaultSelectionIndex:defaultDestinationIdIndex),
            new ToggleOption("Log Requests", defaultValue:true))
    {}

    private RoutingOption(
        TextOption pathRegex, 
        ComboBoxOption method, 
        ComboBoxOption destination_id,
        ToggleOption logRequests)
        : base("Routing", [pathRegex, method, destination_id, logRequests])
    {
        PathRegex = pathRegex;
        Method = method;
        Destination = destination_id;
        LogRequests = logRequests;
    }

    public TextOption PathRegex { get; }
    public ComboBoxOption Method { get; }
    public ComboBoxOption Destination { get; }
    public ToggleOption LogRequests { get; }
}

public sealed class WebPreviewService : ManagedService
{
    public override string Name { get; protected set; } = "WebPage Preview";

    public TextOption EspAddress { get; } = new("ESP Address", true, "http://localhost:5084");
    public TextOption WebPageDir { get; } = new("WebPage Directory", true, "/home/viktorpotuznik/Documents/Coding/Projects/ESPWebBridge/Web");
    public ObservableCollection<RoutingOption> RoutingOptions { get; } = [new(["esp", "vite"], "/api/{**catch-all}", 0), new(["esp", "vite"], "{**catch-all}", 1)];
    public ButtonOption AddRoutingOptionButton { get; }

    public WebPreviewService(int vitePort, int appPort)
    {
        _vitePort = vitePort;
        _appPort = appPort;
        AddRoutingOptionButton = new("Add routing option", onAddRoutingOptionClick);

        AddOption(EspAddress);
        AddOption(WebPageDir);

        foreach (var option in RoutingOptions)
            AddOption(option);

        RoutingOptions.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (IServiceOption option in e.NewItems)
                    AddOption(option);

            if (e.OldItems != null)
                foreach (IServiceOption option in e.OldItems)
                    RemoveOption(option);
        };
        AddOption(AddRoutingOptionButton);
    }
    private WebApplication? _app;
    private InMemoryConfigProvider? _proxyConfig;
    private Process? _viteProcess;
    private Task<string>? _viteStandardOutput;
    private Task<string>? _viteStandardError;
    // This lock protects ownership transfer only. Never hold it while awaiting shutdown.
    private readonly SemaphoreSlim _resourceLock = new(1, 1);
    private int _vitePort;
    private int _appPort;
    private IReadOnlyList<ClusterConfig> _clusters => new[]
    {
        new ClusterConfig
        {
            ClusterId = "esp",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["esp-device"] = new DestinationConfig
                {
                    Address = EspAddress.Value
                }
            }
        },
        new ClusterConfig
        {
            ClusterId = "vite",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["vite-server"] = new DestinationConfig
                {
                    Address = $"http://localhost:{_vitePort}"
                }
            }
        }
    };

    private void onAddRoutingOptionClick() {RoutingOptions.Add(new RoutingOption(["esp", "vite"]));}

    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
            throw new InvalidOperationException("Reverse proxy already running.");

        if (!Directory.Exists(WebPageDir.Value)) throw new InvalidDataException("Invalid webpage directry: no such directory");

        if (!Uri.TryCreate(EspAddress.Value, UriKind.Absolute, out var uri) ||
        !(uri?.Scheme == Uri.UriSchemeHttp || uri?.Scheme == Uri.UriSchemeHttps))
            throw new ArgumentException("Invalid ESP address");

        try
        {
            await RunCommandAsync("node", "--version", cancellationToken);
            await RunCommandAsync("npm", "--version", cancellationToken);

            await EnsureViteProjectManifest(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var viteProcess = StartViteProcess();
            try
            {
                await WaitForViteAsync(viteProcess, cancellationToken);
                await PublishViteProcessAsync(viteProcess, cancellationToken);
            }
            catch
            {
                await KillProcessAsync(viteProcess);
                viteProcess.Dispose();
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{_appPort}");

            var routes = RoutingOptions.Select((routing, index) => new RouteConfig
            {
                RouteId = $"route-{index}",
                ClusterId = routing.Destination.Value,
                Match = new RouteMatch
                {
                    Path = routing.PathRegex.Value,
                    Methods = string.Equals(routing.Method.Value, "any", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : [routing.Method.Value]
                }
            }).ToArray();

            builder.Services.AddReverseProxy().LoadFromMemory(routes, _clusters);

            var app = builder.Build();
            try
            {
                app.MapReverseProxy();
                await app.StartAsync(cancellationToken);
                await PublishAppAsync(app, cancellationToken);
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await ForceCleanupAsync();
            throw;
        }
    }

    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var resources = await TakeResourcesAsync();
        try
        {
            await StopProxyGracefullyAsync(resources.App, cancellationToken);
            await StopViteGracefullyAsync(resources.ViteProcess, cancellationToken);

            if (resources.App is not null)
                await resources.App.DisposeAsync();
            resources.ViteProcess?.Dispose();
        }
        catch
        {
            // A kill cancels this token. Do not leave resources in the hands of a
            // cancelled graceful stop; escalate before returning to ManagedService.
            await ForceStopResourcesAsync(resources);
            throw;
        }
    }

    protected override async Task KillCoreAsync() => await ForceCleanupAsync();

    private async Task RunCommandAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = StartProcess(fileName, arguments, captureOutput: true);
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            await KillProcessAsync(process);
            await Task.WhenAll(standardOutput, standardError);
            throw;
        }

        var output = await standardOutput;
        var error = await standardError;

        if (process.ExitCode != 0)
        {
            var diagnostic = TailNonEmptyLines(error) ?? TailNonEmptyLines(output);
            var message = $"{fileName} {arguments} failed with exit code {process.ExitCode}.";
            if (diagnostic is not null)
                message += $"{Environment.NewLine}{diagnostic}";

            throw new InvalidOperationException(message);
        }
    }

    private Process StartProcess(string fileName, string arguments, bool captureOutput = false)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = WebPageDir.Value,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        });

        return process ?? throw new InvalidOperationException($"Failed to start {fileName} {arguments}.");
    }

    private static string? TailNonEmptyLines(string text, int count = 10)
    {
        var lines = text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .TakeLast(count)
        .ToArray();

        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private async Task EnsureViteProjectManifest(CancellationToken cancellationToken)
    {
        var packageJson = Path.Combine(WebPageDir.Value, "package.json");
        string vitePackage = Path.Combine( WebPageDir.Value, "node_modules", "vite", "package.json");
        
        // 1. Initialize npm if necessary
        if (!File.Exists(packageJson))
        {
            await RunCommandAsync("npm", "init -y", cancellationToken);
            await RunCommandAsync("npm", "pkg set private=true", cancellationToken);
        }

        // 2. Read package.json
        var manifest = JsonNode.Parse(File.ReadAllText(packageJson))
            ?? throw new InvalidOperationException("Invalid package.json");

        // 3. Ensure dev script exists
        if (manifest["scripts"]?["dev"] == null)
        {
            await RunCommandAsync("npm", "pkg set scripts.dev=vite", cancellationToken);
        }

        // 4. Ensure Vite is installed
        if (!File.Exists(vitePackage))
        {
            bool viteDeclared =
                manifest["dependencies"]?["vite"] != null ||
                manifest["devDependencies"]?["vite"] != null;

            if (!viteDeclared)
            {
                await RunCommandAsync("npm", "install --save-dev vite", cancellationToken);
            }   
        }
        await RunCommandAsync("npm", "install", cancellationToken);
    }

    private Process StartViteProcess()
    {
        // setsid gives npm and its Vite child a dedicated process group. SIGTERM can
        // therefore stop them together, while Kill(entireProcessTree: true) remains
        // the escalation path.
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "setsid",
            WorkingDirectory = WebPageDir.Value,
            Arguments = $"npm run dev -- --host 127.0.0.1 --port {_vitePort} --strictPort",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        var viteProcess = process ?? throw new InvalidOperationException("Failed to start Vite.");
        _viteStandardOutput = viteProcess.StandardOutput.ReadToEndAsync();
        _viteStandardError = viteProcess.StandardError.ReadToEndAsync();
        return viteProcess;
    }

    private async Task WaitForViteAsync(Process process, CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        while (true)
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", _vitePort, cancellationToken).AsTask();
            if (await Task.WhenAny(exitTask, connectTask) == exitTask)
            {
                await exitTask;
                var error = _viteStandardError is null ? "" : await _viteStandardError;
                var output = _viteStandardOutput is null ? "" : await _viteStandardOutput;
                var diagnostic = TailNonEmptyLines(error) ?? TailNonEmptyLines(output);
                throw new InvalidOperationException(
                    $"Vite exited with code {process.ExitCode}.{(diagnostic is null ? "" : $"{Environment.NewLine}{diagnostic}")}");
            }

            try
            {
                await connectTask;
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async Task PublishViteProcessAsync(Process process, CancellationToken cancellationToken)
    {
        await _resourceLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _viteProcess = process;
        }
        finally
        {
            _resourceLock.Release();
        }
    }

    private async Task PublishAppAsync(WebApplication app, CancellationToken cancellationToken)
    {
        await _resourceLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _app = app;
            _proxyConfig = app.Services.GetRequiredService<InMemoryConfigProvider>();
        }
        finally
        {
            _resourceLock.Release();
        }
    }

    private async Task<Resources> TakeResourcesAsync()
    {
        await _resourceLock.WaitAsync();
        try
        {
            var resources = new Resources(_app, _viteProcess);
            _app = null;
            _proxyConfig = null;
            _viteProcess = null;
            return resources;
        }
        finally
        {
            _resourceLock.Release();
        }
    }

    private async Task ForceCleanupAsync() => await ForceStopResourcesAsync(await TakeResourcesAsync());

    private static async Task StopProxyGracefullyAsync(WebApplication? app, CancellationToken cancellationToken)
    {
        if (app is null)
            return;

        await app.StopAsync(cancellationToken);
    }

    private static async Task StopViteGracefullyAsync(Process? process, CancellationToken cancellationToken)
    {
        if (process is null)
            return;

        if (!process.HasExited && kill(-process.Id, SigTerm) != 0)
            throw new InvalidOperationException("Could not send SIGTERM to the Vite process group.");

        await process.WaitForExitAsync(cancellationToken);
    }

    private static async Task ForceStopResourcesAsync(Resources resources)
    {
        try
        {
            if (resources.App is not null)
                await resources.App.DisposeAsync();
        }
        finally
        {
            if (resources.ViteProcess is not null)
            {
                await KillProcessAsync(resources.ViteProcess);
                resources.ViteProcess.Dispose();
            }
        }
    }

    private static async Task KillProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between HasExited and Kill.
        }

        await process.WaitForExitAsync(CancellationToken.None);
    }

    private sealed record Resources(WebApplication? App, Process? ViteProcess);

    private const int SigTerm = 15;

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);
}
