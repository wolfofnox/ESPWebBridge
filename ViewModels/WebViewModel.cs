using CommunityToolkit.Mvvm.ComponentModel;

namespace ESPWebBridge.ViewModels;

public partial class WebViewModel(int previewPort) : ViewModelBase
{
    public string ViteUri { get; } = $"http://localhost:{previewPort}";

    [ObservableProperty]
    private string webPrewiewUri = $"http://localhost:{previewPort}";

}
