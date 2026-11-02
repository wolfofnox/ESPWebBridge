using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ESPWebBridge.ViewModels;
using ESPWebBridge.Views;
using Microsoft.Extensions.Configuration;

namespace ESPWebBridge;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var vitePort = Program.Configuration.GetValue<int>("WebPrevier:VitePort", 5173);
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