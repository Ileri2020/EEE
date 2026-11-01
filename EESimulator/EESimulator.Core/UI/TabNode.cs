using System;
using System.Collections.Generic;

namespace EESimulator.Core.UI;

/// <summary>
/// Tab UI node. Represents a tab switcher/container in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.tabs('main_tabs').switch_tab('Script')
/// </summary>
public class TabNode : UINode
{
    private readonly List<string> _tabs = new();
    private int _activeTabIndex = 0;

    public List<string> Tabs => _tabs;

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set
        {
            if (_activeTabIndex != value && value >= 0 && value < _tabs.Count)
            {
                var old = _activeTabIndex;
                _activeTabIndex = value;
                NotifyModified(nameof(ActiveTabIndex), old, value);
                TriggerValueChanged(ActiveTabName);
            }
        }
    }

    public string? ActiveTabName
    {
        get => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;
        set
        {
            if (value != null)
            {
                var idx = _tabs.IndexOf(value);
                if (idx >= 0)
                {
                    ActiveTabIndex = idx;
                }
            }
        }
    }

    public TabNode()
    {
        Name = "Tabs";
    }

    public TabNode(string name, IEnumerable<string>? tabs = null) : this()
    {
        Name = name;
        if (tabs != null)
        {
            _tabs.AddRange(tabs);
        }
    }

    public void SwitchTab(int index)
    {
        ActiveTabIndex = index;
    }

    public void SwitchTab(string name)
    {
        ActiveTabName = name;
    }

    public void AddTab(string name)
    {
        _tabs.Add(name);
        NotifyModified(nameof(Tabs));
    }
}
