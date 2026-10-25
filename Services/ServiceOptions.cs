using CommunityToolkit.Mvvm.ComponentModel;

namespace ESPWebBridge.Services.Options;

public interface IServiceOption
{
    string Label { get; }
}

public sealed partial class TextOption : ObservableObject, IServiceOption
{
    public TextOption(string label) => Label = label;

    public string Label { get; }

    [ObservableProperty]
    private string value = string.Empty;
}

public sealed partial class ToggleOption : ObservableObject, IServiceOption
{
    public ToggleOption(string label) => Label = label;

    public string Label { get; }

    [ObservableProperty]
    private bool value;
}

public sealed partial class NumberOption : ObservableObject, IServiceOption
{
    public NumberOption(string label) => Label = label;

    public string Label { get; }

    [ObservableProperty]
    private double value;
}
