using Avalonia.Controls;
using Avalonia.Input;
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
        if (e.Key == Key.Enter && sender is TextBox addressBox && DataContext is WebViewModel viewModel)
        {
            viewModel.Navigate(addressBox.Text ?? string.Empty);
            Browser.Focus();
        }
    }
}
