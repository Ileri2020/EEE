using System;

namespace EESimulator.Core.UI;

/// <summary>
/// Button UI node. Represents a clickable button in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.button('run_sim').click()
/// </summary>
public class ButtonNode : UINode
{
    private string _label = "Button";
    private string _icon = string.Empty;
    private string _variant = "Primary"; // Primary, Secondary, Danger, Ghost

    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                var old = _label;
                _label = value;
                NotifyModified(nameof(Label), old, value);
            }
        }
    }

    public string Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                var old = _icon;
                _icon = value;
                NotifyModified(nameof(Icon), old, value);
            }
        }
    }

    public string Variant
    {
        get => _variant;
        set
        {
            if (_variant != value)
            {
                var old = _variant;
                _variant = value;
                NotifyModified(nameof(Variant), old, value);
            }
        }
    }

    public Action? CommandAction { get; set; }

    public ButtonNode()
    {
        Name = "Button";
    }

    public ButtonNode(string name, string label, Action? action = null) : this()
    {
        Name = name;
        Label = label;
        CommandAction = action;
    }

    public void Click()
    {
        if (!Enabled || !Visible) return;
        CommandAction?.Invoke();
        TriggerClick();
    }
}
