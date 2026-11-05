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

public sealed class WebPreviewService : IManagedService
{
    public WebPreviewService(int vitePort)
    {
        _vitePort = vitePort;
        AddRoutingOptionButton = new("Add routing option", onAddRoutingOptionClick);

        RoutingOptions.CollectionChanged += (_,_) => OptionsChanged?.Invoke(this, EventArgs.Empty);
    }
    public string Name => "WebPage Preview";
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public event EventHandler? StatusChanged;
    public event EventHandler? OptionsChanged;

    public TextOption EspAddress { get; } = new("ESP Address", true, "http://localhost:5084");
    public TextOption WebPageDir { get; } = new("WebPage Directory", true, "/home/viktorpotuznik/Documents/Coding/Projects/ESPWebBridge/Web");
    public ObservableCollection<RoutingOption> RoutingOptions { get; } = [new(["esp", "vite"], "/api/{**catch-all}", 0), new(["esp", "vite"], "{**catch-all}", 1)];
    public ButtonOption AddRoutingOptionButton { get; }
    public IReadOnlyList<IServiceOption> Options => [EspAddress, WebPageDir, .. RoutingOptions, AddRoutingOptionButton];

    private WebApplication? _app;
    private InMemoryConfigProvider? _proxyConfig;
    private int _vitePort;
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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;

        // TODO throw exception or create directory
        if (!Directory.Exists(WebPageDir.Value)) throw new InvalidDataException("Invalid webpage directry: no such directory");

        // TODO throw exception
        if (!Uri.TryCreate(EspAddress.Value, UriKind.Absolute, out var uri) &&
        (uri?.Scheme == Uri.UriSchemeHttp || uri?.Scheme == Uri.UriSchemeHttps)) return;

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

        var routes =
            RoutingOptions.Select((routing, index) => new RouteConfig
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

        builder.Services
            .AddReverseProxy()
            .LoadFromMemory(routes, _clusters);

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
