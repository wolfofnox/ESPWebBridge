using CommunityToolkit.Mvvm.ComponentModel;

namespace ESPWebBridge.Services.Options;

public interface IServiceOption
{
    string Label { get; }
}

public sealed partial class TextOption(string label, string? defaultValue = null) : ObservableObject, IServiceOption
{
    public string Label { get; } = label;

    [ObservableProperty]
    private string value = defaultValue ?? string.Empty;
}

public sealed partial class ToggleOption(string label, bool defaultValue = false) : ObservableObject, IServiceOption
{
    public string Label { get; } =label;

    [ObservableProperty]
    private bool value = defaultValue;

}

public sealed partial class NumberOption(string label, double defaultValue = 0) : ObservableObject, IServiceOption
{
    public string Label { get; } = label;

    [ObservableProperty]
    private double value = defaultValue;
}
