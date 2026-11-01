using System;
using System.Collections.Generic;

namespace EESimulator.Core.UI;

/// <summary>
/// Select UI node. Represents a single- or multi-select chip or list element in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.select('filter_chips').select('Electrical')
/// </summary>
public class SelectNode : UINode
{
    private readonly List<string> _items = new();
    private readonly HashSet<string> _selectedItems = new();
    private bool _multiSelect = false;

    public List<string> Items => _items;
    public HashSet<string> SelectedItems => _selectedItems;

    public bool MultiSelect
    {
        get => _multiSelect;
        set
        {
            if (_multiSelect != value)
            {
                var old = _multiSelect;
                _multiSelect = value;
                NotifyModified(nameof(MultiSelect), old, value);
            }
        }
    }

    public string? SelectedItem => _selectedItems.Count > 0 ? _items.Find(i => _selectedItems.Contains(i)) : null;

    public SelectNode()
    {
        Name = "Select";
    }

    public SelectNode(string name, IEnumerable<string>? items = null, bool multiSelect = false) : this()
    {
        Name = name;
        _multiSelect = multiSelect;
        if (items != null)
        {
            _items.AddRange(items);
        }
    }

    public void Select(string item)
    {
        if (!_multiSelect)
        {
            _selectedItems.Clear();
        }
        if (_items.Contains(item))
        {
            _selectedItems.Add(item);
            NotifyModified(nameof(SelectedItems));
            TriggerValueChanged(item);
        }
    }

    public void Deselect(string item)
    {
        if (_selectedItems.Remove(item))
        {
            NotifyModified(nameof(SelectedItems));
            TriggerValueChanged(null);
        }
    }

    public void ToggleItem(string item)
    {
        if (_selectedItems.Contains(item))
            Deselect(item);
        else
            Select(item);
    }

    public void ClearSelection()
    {
        _selectedItems.Clear();
        NotifyModified(nameof(SelectedItems));
        TriggerValueChanged(null);
    }
}
