using System;
using System.Collections.Generic;

namespace EESimulator.Core.UI;

/// <summary>
/// Dropdown UI node. Represents a dropdown picker/combobox in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.dropdown('analysis_type').select('Transient')
/// </summary>
public class DropdownNode : UINode
{
    private readonly List<string> _options = new();
    private int _selectedIndex = -1;

    public List<string> Options => _options;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value && value >= -1 && value < _options.Count)
            {
                var old = _selectedIndex;
                _selectedIndex = value;
                NotifyModified(nameof(SelectedIndex), old, value);
                TriggerValueChanged(SelectedItem);
            }
        }
    }

    public string? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < _options.Count ? _options[_selectedIndex] : null;
        set
        {
            if (value == null)
            {
                SelectedIndex = -1;
            }
            else
            {
                var idx = _options.IndexOf(value);
                if (idx >= 0)
                {
                    SelectedIndex = idx;
                }
            }
        }
    }

    public DropdownNode()
    {
        Name = "Dropdown";
    }

    public DropdownNode(string name, IEnumerable<string>? options = null) : this()
    {
        Name = name;
        if (options != null)
        {
            _options.AddRange(options);
            if (_options.Count > 0)
            {
                _selectedIndex = 0;
            }
        }
    }

    public void Select(int index)
    {
        SelectedIndex = index;
    }

    public void Select(string item)
    {
        SelectedItem = item;
    }

    public void AddOption(string option)
    {
        _options.Add(option);
        if (_selectedIndex == -1 && _options.Count > 0)
        {
            _selectedIndex = 0;
        }
        NotifyModified(nameof(Options));
    }
}
