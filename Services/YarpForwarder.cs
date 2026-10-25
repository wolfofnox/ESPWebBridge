using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ESPWebBridge.Services.Options;

namespace ESPWebBridge.Services;

public sealed class YarpForwarderService : IManagedService
{
    public string Name => "Yarp Forwarder";
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public event EventHandler? StatusChanged;

    public TextOption EspAddress { get; } = new("ESP Address");
    public ToggleOption RouteToVite { get; } = new("Route first to vite");
    public NumberOption Number {get;} = new("HAHA a number");

    public IReadOnlyList<IServiceOption> Options => [EspAddress, RouteToVite, Number];

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Status = ServiceStatus.Running;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Status = ServiceStatus.Stopped;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void Kill()
    {
        Status = ServiceStatus.Stopped;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}