using LuminUI.Attributes;

namespace LuminUI.Samples.Inventory.View;

// Minimal platform controls so the sample compiles outside Unity.
// A Unity bridge would return Button, TMP_Text, Image, and other real components.
[UiClickEvent(nameof(Clicked))]
public sealed class Button
{
    public event Action? Clicked;
    public bool Enabled { get; set; } = true;
    public bool Selected { get; set; }
    public string Text { get; set; } = string.Empty;

    public void Click()
    {
        if (Enabled) Clicked?.Invoke();
    }
}

public sealed class Label
{
    public string Text { get; set; } = string.Empty;
    public int Number { get; private set; }
    public int SecondaryNumber { get; private set; }

    public void SetInt(int value) => Number = value;

    public void SetPair(int value, int secondary)
    {
        Number = value;
        SecondaryNumber = secondary;
    }
}

public sealed class ProgressBar
{
    public int Value { get; private set; }
    public int Maximum { get; private set; }

    public void SetValue(int value, int maximum)
    {
        Value = value;
        Maximum = maximum;
    }
}

public sealed class Panel
{
    public bool Visible { get; set; } = true;
}
