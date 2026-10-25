using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ESPWebBridge.Services;
using ESPWebBridge.Services.Options;

namespace ESPWebBridge.ViewModels;

public partial class ServiceViewModel : ViewModelBase
{
    private readonly IManagedService _service;
    public ServiceViewModel(IManagedService service)
    {
        _service = service;
        _service.StatusChanged += (_, _) =>
        {
          OnPropertyChanged(nameof(Status));
          OnPropertyChanged(nameof(ButtonText));
          OnPropertyChanged(nameof(StatusBrush));  
        };
    }
    public string Name => _service.Name;

    private ServiceStatus Status => _service.Status;

    public IReadOnlyList<IServiceOption> Options => _service.Options;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public string ButtonText => Status switch
    {
        ServiceStatus.Stopped => "Start",
        ServiceStatus.Running => "Stop",
        ServiceStatus.Paused => "Resume",
        ServiceStatus.Error => "Retry",
        _ => "Start"
    };

    public IBrush StatusBrush => Status switch
    {
        ServiceStatus.Stopped => Brushes.Gray,
        ServiceStatus.Running => Brushes.Green,
        ServiceStatus.Paused => Brushes.Orange,
        ServiceStatus.Error => Brushes.Red,
        _ => Brushes.Gray
    };

    [RelayCommand]
    private async Task ToggleAsync()
    {
        ErrorMessage = null;
        
        try
        {
            if (Status == ServiceStatus.Running)
                await _service.StopAsync();
            else
                await _service.StartAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
    }
    
}
