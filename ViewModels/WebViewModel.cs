using System;
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

    [ObservableProperty]
    private string address = $"http://localhost:{previewPort}";

    public void Navigate(string address)
    {
        address = address.Trim();

        if (!address.Contains("://"))
            address = $"http://{address}";

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        Address = uri.AbsoluteUri;
        WebPrewiewUri = uri.AbsoluteUri;
    }
}
