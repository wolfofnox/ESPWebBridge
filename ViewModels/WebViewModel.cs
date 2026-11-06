using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ESPWebBridge.Services;

namespace ESPWebBridge.ViewModels;

public partial class WebViewModel(int previewPort) : ViewModelBase
{
    [ObservableProperty]
    private string webPrewiewUri = $"http://localhost:{previewPort}";
}
