using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ESPWebBridge.ViewModels;
using ESPWebBridge.Views;

namespace ESPWebBridge;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new Services.IManagedService[]
        {
            new Services.WebPreview.WebPreviewService()
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(services),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}