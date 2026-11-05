using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System;
using CommunityToolkit.Mvvm.Input;

namespace ESPWebBridge.Services.Options;

public interface IServiceOption
{
    string Label { get; }
    bool IsQuickOption { get; }
}

public partial class OptionGroup(string label, IReadOnlyList<IServiceOption> options) : IServiceOption
{
    public string Label { get; } = label;
    public bool IsQuickOption => false;

    public IReadOnlyList<IServiceOption> Options { get; } = options;

    public int OptionsNum => Options.Count;
}

public sealed partial class TextOption(string label, bool isQuickOption = false, string? defaultValue = null) : ObservableObject, IServiceOption
{
    public string Label { get; } = label;
    public bool IsQuickOption { get; } = isQuickOption;

    [ObservableProperty]
    private string value = defaultValue ?? string.Empty;
}

public sealed partial class ToggleOption(string label, bool isQuickOption = false, bool defaultValue = false) : ObservableObject, IServiceOption
{
    public string Label { get; } = label;
    public bool IsQuickOption { get; } = isQuickOption;

    [ObservableProperty]
    private bool value = defaultValue;

}

public sealed partial class NumberOption(string label, bool isQuickOption = false, double defaultValue = 0) : ObservableObject, IServiceOption
{
    public string Label { get; } = label;
    public bool IsQuickOption { get; } = isQuickOption;

    [ObservableProperty]
    private double value = defaultValue;
}

public sealed partial class ComboBoxOption(string label, IReadOnlyList<string> choices, bool isQuickOption = false, int defaultSelectionIndex = 0) : ObservableObject, IServiceOption
{
    public string Label { get; } = label;
    public bool IsQuickOption { get; } = isQuickOption;

    public IReadOnlyList<string> Choices { get; } = choices;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value))]
    private int selectionIndex = defaultSelectionIndex;
    public string Value => Choices[SelectionIndex];
}

public delegate void ButtonOnClickFunction();
public sealed partial class ButtonOption(string label, ButtonOnClickFunction onClick, bool isQuickOption = false) : IServiceOption
{
    public string Label { get; } = label;
    public bool IsQuickOption { get; } = isQuickOption;
    private ButtonOnClickFunction _onClick = onClick;

    [RelayCommand]
    private void OnClick()
    {
        _onClick();
    }
}
