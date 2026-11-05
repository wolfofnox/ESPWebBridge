using ESPWebBridge.Services.WebPreview;

namespace ESPWebBridge.ViewModels;

public static class DesignData
{
    public static ServicesViewModel Services { get; } =
        new([new WebPreviewService(5173)]);

    public static MainViewModel Main { get; } =
        new([new WebPreviewService(5173)]);
}
