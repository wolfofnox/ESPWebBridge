using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ESPWebBridge.ViewModels;

namespace ESPWebBridge.Views;

public partial class WebView : UserControl
{
    public WebView()
    {
        InitializeComponent();
    }
    private void AddressBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox addressBox)
        {
            var address = (addressBox.Text ?? string.Empty).Trim();

            if (!address.Contains("://"))
                address = $"http://{address}";

            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }

            Browser.Navigate(uri);
            Browser.Focus();
            Debug.Print("Back " + Browser.CanGoBack.ToString() + ", Forward " + Browser.CanGoForward.ToString());
        }
    }

    private void Browser_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        AddressBox.Text = e.Request?.AbsoluteUri;
        BackButton.IsEnabled = Browser.CanGoBack;
        ForwardButton.IsEnabled = Browser.CanGoForward;
    }

    private void GoBack(object? sender, RoutedEventArgs e)
    {
        Browser.GoBack();
    }

    private void GoForward(object? sender, RoutedEventArgs e)
    {
        Browser.GoForward();
    }

    private void Reload(object? sender, RoutedEventArgs e)
    {
        Browser.Refresh();
    }

    private void GoHome(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WebViewModel viewModel)
            Browser.Navigate(new Uri(viewModel.ViteUri));
    }
}
