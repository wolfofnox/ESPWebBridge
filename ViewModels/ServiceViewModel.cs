using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
          OnPropertyChanged(nameof(StatusTooltip));
        };
        ((INotifyCollectionChanged)_service.Options).CollectionChanged += (_,_) =>
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
        _ => "Kill"
    };

    public string StatusTooltip => Status.ToString();

    public IBrush StatusBrush => Status switch
    {
        ServiceStatus.Stopped => Brushes.Gray,
        ServiceStatus.Running => Brushes.Green,
        ServiceStatus.Error => Brushes.Red,
        ServiceStatus.Starting => Brushes.Yellow,
        ServiceStatus.Stopping => Brushes.Orange,
        ServiceStatus.Killing => Brushes.Orange,
        _ => Brushes.Gray
    };

    

    [RelayCommand]
    private async Task ToggleAsync()
    {
        ErrorMessage = null;
        
        try
        {
            switch (Status)
            {
                case ServiceStatus.Stopped:
                    await _service.StartAsync();
                    break;
                case ServiceStatus.Running:
                    await _service.StopAsync();
                    break;
                default:
                    await _service.KillAsync();
                    break;

            };
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
