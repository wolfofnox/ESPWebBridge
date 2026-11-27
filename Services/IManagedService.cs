

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using ESPWebBridge.Services.Options;

namespace ESPWebBridge.Services;

public enum ServiceStatus
{
    Stopped,
    Running,
    Paused,
    Error,
    Starting,
    Stopping,
    Killing
}

public interface IManagedService
{
    string Name { get; }
    ServiceStatus Status { get; }
    event EventHandler? StatusChanged;

    ReadOnlyObservableCollection<IServiceOption> Options {get;}

    Task StartAsync();
    Task StopAsync();
    Task KillAsync();
}

public abstract class ManagedService : IManagedService
{
    public abstract string Name {get; protected set;}
    private ServiceStatus _status = ServiceStatus.Stopped;

    public event EventHandler? StatusChanged;

    public ServiceStatus Status
    {
        get => _status;
        protected set
        {
            if (_status == value)
                return;

            _status = value;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly ObservableCollection<IServiceOption> _options = [];

    public ReadOnlyObservableCollection<IServiceOption> Options { get; }

    private CancellationTokenSource? _startupCts;
    private Task? _startupTask;
    private CancellationTokenSource? _shutdownCts;
    private Task? _shutdownTask;
    private Task? _killTask;
    private static readonly TimeSpan GracefulShutdownTimeout = TimeSpan.FromSeconds(10);

    protected ManagedService()
    {   
        Options = new ReadOnlyObservableCollection<IServiceOption>(_options);
    }

    protected void AddOption(IServiceOption option)
    {
        _options.Add(option);
    }

    protected void RemoveOption(IServiceOption option)
    {
        _options.Remove(option);
    }

    public async Task StartAsync()
    {
        if (Status == ServiceStatus.Running)
            return;
        if (Status == ServiceStatus.Starting)
        {
            if (_startupTask != null)
                await _startupTask;
            return;
        }
        if (Status != ServiceStatus.Stopped)
            throw new InvalidOperationException(
                $"Cannot start service while its status is {Status}, needs to be Stopped.");

        _startupCts = new CancellationTokenSource();

        Status = ServiceStatus.Starting;
        

        _startupTask = StartInternalAsync(_startupCts.Token);

        await _startupTask;
    }
    private async Task StartInternalAsync(CancellationToken ct)
    {
        try
        {
            await StartCoreAsync(ct);
            Status = ServiceStatus.Running;
            if(ct.IsCancellationRequested) await StopAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Status = ServiceStatus.Stopped;
        }
        catch
        {
            Status = ServiceStatus.Error;
            throw;
        }
        finally
        {
            _startupCts?.Dispose();
            _startupCts = null;
            _startupTask = null;

        }
    }

    public async Task StopAsync()
    {
        if (Status == ServiceStatus.Stopped)
            return;
        if (Status == ServiceStatus.Stopping)
        {
            if (_shutdownTask != null)
                await _shutdownTask;
            return;
        }
        if (Status == ServiceStatus.Starting)
        {
            _startupCts?.Cancel();
            if (_startupTask != null)
                await _startupTask;

            if (Status == ServiceStatus.Stopped)
                return;
        }
        if (Status != ServiceStatus.Running)
            throw new InvalidOperationException(
                $"Cannot stop service while its status is {Status}.");

        _shutdownCts = new CancellationTokenSource();
        
        Status = ServiceStatus.Stopping;

        _shutdownTask = StopInternalAsync(_shutdownCts.Token);

        _shutdownCts.CancelAfter(GracefulShutdownTimeout);

        await _shutdownTask;

    }
    private async Task StopInternalAsync(CancellationToken ct)
    {
        try
        {
            await StopCoreAsync(ct);
            Status = ServiceStatus.Stopped;
            ct.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Status = ServiceStatus.Stopped;
        }
        catch
        {
            Status = ServiceStatus.Error;
            throw;
        }
    }

    public async Task KillAsync()
    {
        if (Status == ServiceStatus.Stopped)
            return;
        if (Status == ServiceStatus.Killing)
        {
            if (_killTask != null)
                await _killTask;
            return;
        }
        _startupCts?.Cancel();
        _shutdownCts?.Cancel();

        _killTask = KillInternalAsync();

        Status = ServiceStatus.Killing;

        await _killTask;
        
    }

    private async Task KillInternalAsync()
    {
        try
        {
            await KillCoreAsync();
        }
        finally
        {
            Status = ServiceStatus.Stopped;
        }
    }

    protected abstract Task StartCoreAsync(CancellationToken cancellationToken);
    protected abstract Task StopCoreAsync(CancellationToken cancellationTokenOnKill);
    protected abstract Task KillCoreAsync();
}
