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

namespace ESPWebBridge.Services.WebPreview;

public sealed class WebPreviewService(int vitePort) : IManagedService
{
    public string Name => "WebPage Preview";
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public event EventHandler? StatusChanged;

    public TextOption EspAddress { get; } = new("ESP Address", "http://localhost:5173");
    public TextOption WebPageDir { get; } = new("WebPage Directory", "/home/viktorpotuznik/Documents/Coding/Projects/ESPWebBridge/Web");

    public IReadOnlyList<IServiceOption> Options => [EspAddress, WebPageDir];

    private WebApplication? _app;
    private InMemoryConfigProvider? _proxyConfig;
    private int _vitePort = vitePort;

    private IReadOnlyList<RouteConfig> _routes => new[]
    {
        new RouteConfig
        {
            RouteId = "esp-all",
            ClusterId = "esp",
            Match = new RouteMatch { Path = "{**catch-all}" }
        }
    };
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
                    Address = $"localhost:{_vitePort}"
                }
            }
        }
    };


    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;

        // TODO throw exception or create directory
        if (!Directory.Exists(WebPageDir.Value)) throw new InvalidDataException("Invalid webpage directry: no such directory");

        // TODO throw exception
        if (!Uri.TryCreate(EspAddress.Value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = WebPageDir.Value,
            Arguments = "--version"
        };
        var process = Process.Start(startInfo);
        if (process is null) throw new InvalidOperationException("Failed to start node --version");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"node failed: returned non-0 exit code: {process.ExitCode}");

        startInfo = new ProcessStartInfo
        {
            FileName = "npm",
            WorkingDirectory = WebPageDir.Value,
            Arguments = "--version"
        };
        process = Process.Start(startInfo);
        if (process is null) throw new InvalidOperationException("Failed to start npm --version");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"npm failed: returned non-0 exit code: {process.ExitCode}");

        startInfo = new ProcessStartInfo
        {
            FileName = "npm",
            WorkingDirectory = WebPageDir.Value,
            Arguments = "install"
        };
        process = Process.Start(startInfo);
        if (process is null) throw new InvalidOperationException("Failed to start npm --version");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"npm failed: returned non-0 exit code: {process.ExitCode}");
        
        startInfo = new ProcessStartInfo
        {
            FileName = "npm",
            WorkingDirectory = WebPageDir.Value,
            Arguments = $"run dev -- --host 127.0.0.1 --port {_vitePort} --strictPort"
        };
        process = Process.Start(startInfo);
        if (process is null) throw new InvalidOperationException("Failed to start npm --version");
        
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:5174");

        builder.Services
            .AddReverseProxy()
            .LoadFromMemory(_routes, _clusters);

        var app = builder.Build();
        app.MapReverseProxy();

        _proxyConfig = app.Services
            .GetRequiredService<InMemoryConfigProvider>();

        await app.StartAsync();
        _app = app;

        Status = ServiceStatus.Running;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
            return;

        await _app.StopAsync();
        await _app.DisposeAsync();

        _app = null;
        _proxyConfig = null;
        Status = ServiceStatus.Stopped;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return;
    }

    public void Kill()
    {
        Status = ServiceStatus.Stopped;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}