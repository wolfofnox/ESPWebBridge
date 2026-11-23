using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
        _service.OptionsChanged += (_,_) =>
        {
            OnPropertyChanged(nameof(QuickOptions));
            OnPropertyChanged(nameof(OtherOptions));
        };
    }
    public string Name => _service.Name;

    private ServiceStatus Status => _service.Status;

    public IReadOnlyList<IServiceOption> QuickOptions => _service.Options
        .Where(opt => opt.IsQuickOption)
        .ToArray();
    
    public IReadOnlyList<IServiceOption> OtherOptions => _service.Options
        .Where(opt => opt.IsQuickOption == false)
        .ToArray();

    [ObservableProperty]
    private bool showOtherOptions = false;

    private static readonly IImage ChevronUp = new Bitmap(AssetLoader.Open(
        new Uri("avares://ESPWebBridge/Assets/Icons/chevron-up.png")));
    private static readonly IImage ChevronDown = new Bitmap(AssetLoader.Open(
        new Uri("avares://ESPWebBridge/Assets/Icons/chevron-down.png")));

    public IImage OtherOptionsChevron => ShowOtherOptions ? ChevronUp : ChevronDown;

    partial void OnShowOtherOptionsChanged(bool value) => OnPropertyChanged(nameof(OtherOptionsChevron));

    [RelayCommand]
    private void ToggleOtherOptions() => ShowOtherOptions = !ShowOtherOptions;

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
