using ESPWebBridge.Services;

namespace ESPWebBridge.ViewModels;

public static class DesignData
{
    public static ServicesViewModel Services { get; } =
        new([new YarpForwarderService()]);

    public static MainViewModel Main { get; } =
        new([new YarpForwarderService()]);
}
