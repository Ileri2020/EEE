using System;
using System.Globalization;

namespace EESimulator.Core.UI;

/// <summary>
/// Input UI node. Represents a text or numeric input element in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.input('resistance_input').set_text('2200')
/// </summary>
public class InputNode : UINode
{
    private string _text = string.Empty;
    private string _placeholder = "Enter value...";
    private string _inputType = "text"; // text, number, password
    private double _numericValue = 0.0;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                var old = _text;
                _text = value;
                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                {
                    _numericValue = n;
                }
                NotifyModified(nameof(Text), old, value);
                TriggerValueChanged(value);
            }
        }
    }

    public double NumericValue
    {
        get => _numericValue;
        set
        {
            if (Math.Abs(_numericValue - value) > 1e-9)
            {
                var old = _numericValue;
                _numericValue = value;
                _text = value.ToString(CultureInfo.InvariantCulture);
                NotifyModified(nameof(NumericValue), old, value);
                TriggerValueChanged(value);
            }
        }
    }

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            if (_placeholder != value)
            {
                var old = _placeholder;
                _placeholder = value;
                NotifyModified(nameof(Placeholder), old, value);
            }
        }
    }

    public string InputType
    {
        get => _inputType;
        set
        {
            if (_inputType != value)
            {
                var old = _inputType;
                _inputType = value;
                NotifyModified(nameof(InputType), old, value);
            }
        }
    }

    public InputNode()
    {
        Name = "Input";
    }

    public InputNode(string name, string placeholder = "", string inputType = "text") : this()
    {
        Name = name;
        Placeholder = placeholder;
        InputType = inputType;
    }

    public void SetText(string text)
    {
        Text = text;
    }

    public void SetNumber(double number)
    {
        NumericValue = number;
    }

    public void Clear()
    {
        Text = string.Empty;
        NumericValue = 0.0;
    }
}
