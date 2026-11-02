using Avalonia;
using System;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ESPWebBridge;


sealed class Program
{
    public static IConfiguration Configuration { get; private set; } = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ESPWebBridge");

        Configuration = new ConfigurationBuilder()
            .AddIniFile(
                Path.Combine(AppContext.BaseDirectory, "config.ini"),
                optional: true)
            .AddIniFile(
                Path.Combine(userConfigDir, "config.ini"),
                optional: true)
            .Build();
    
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
