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

namespace ESPWebBridge.Services.WebPreview;

public sealed class WebPreviewService : IManagedService
{
    public string Name => "WebPage Preview";
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public event EventHandler? StatusChanged;

    public TextOption EspAddress { get; } = new("ESP Address", "http://localhost:5084");

    public IReadOnlyList<IServiceOption> Options => [EspAddress];

    private WebApplication? _app;
    private InMemoryConfigProvider? _proxyConfig;

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
        }
    };


    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;


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