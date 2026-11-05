

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ESPWebBridge.Services.Options;

namespace ESPWebBridge.Services;

public enum ServiceStatus
{
    Stopped,
    Running,
    Paused,
    Error
}

public interface IManagedService
{
    string Name { get; }
    ServiceStatus Status { get; }
    event EventHandler? StatusChanged;

    IReadOnlyList<IServiceOption> Options {get;}
    event EventHandler? OptionsChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    void Kill();
}