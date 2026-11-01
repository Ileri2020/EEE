using System;
using System.Collections.Generic;
using EESimulator.Core.Models;

namespace EESimulator.Core.UI;

/// <summary>
/// Base class for all UI elements in EESimulator.
/// Inherits from RootNode so that the complete user interface is part of the Blender-like
/// scene/node graph and fully scriptable via the Python interface.
/// </summary>
public abstract class UINode : RootNode
{
    private string _title = string.Empty;
    private double _width = double.NaN;
    private double _height = double.NaN;
    private bool _isOpen = true;
    private string _themeClass = "dark";

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                var old = _title;
                _title = value;
                NotifyModified(nameof(Title), old, value);
            }
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                var old = _width;
                _width = value;
                NotifyModified(nameof(Width), old, value);
            }
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                var old = _height;
                _height = value;
                NotifyModified(nameof(Height), old, value);
            }
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen != value)
            {
                var old = _isOpen;
                _isOpen = value;
                NotifyModified(nameof(IsOpen), old, value);
            }
        }
    }

    public string ThemeClass
    {
        get => _themeClass;
        set
        {
            if (_themeClass != value)
            {
                var old = _themeClass;
                _themeClass = value;
                NotifyModified(nameof(ThemeClass), old, value);
            }
        }
    }

    // Interactive event hooks accessible from both C# and Python
    public event Action<UINode>? OnClick;
    public event Action<UINode, object?>? OnValueChanged;
    public event Action<UINode>? OnStateChanged;

    public virtual void TriggerClick()
    {
        OnClick?.Invoke(this);
        NotifyModified(nameof(OnClick));
    }

    public virtual void TriggerValueChanged(object? newValue)
    {
        OnValueChanged?.Invoke(this, newValue);
        NotifyModified(nameof(OnValueChanged), null, newValue);
    }

    public virtual void TriggerStateChanged()
    {
        OnStateChanged?.Invoke(this);
        NotifyModified(nameof(OnStateChanged));
    }

    public virtual void Show()
    {
        Visible = true;
        IsOpen = true;
        TriggerStateChanged();
    }

    public virtual void Hide()
    {
        Visible = false;
        IsOpen = false;
        TriggerStateChanged();
    }

    public virtual void Toggle()
    {
        if (Visible && IsOpen)
            Hide();
        else
            Show();
    }
}
