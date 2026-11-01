using System;

namespace EESimulator.Core.UI;

/// <summary>
/// Sidebar UI node. Represents a dockable, collapsible panel in the UI hierarchy.
/// Scriptable via Python: e.g. eesim.ui.sidebar('tools_panel').toggle()
/// </summary>
public class SidebarNode : UINode
{
    private bool _isCollapsed = false;
    private string _dockPosition = "Left"; // Left, Right, Bottom
    private double _expandedWidth = 260.0;
    private double _collapsedWidth = 48.0;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                var old = _isCollapsed;
                _isCollapsed = value;
                NotifyModified(nameof(IsCollapsed), old, value);
                TriggerStateChanged();
            }
        }
    }

    public string DockPosition
    {
        get => _dockPosition;
        set
        {
            if (_dockPosition != value)
            {
                var old = _dockPosition;
                _dockPosition = value;
                NotifyModified(nameof(DockPosition), old, value);
            }
        }
    }

    public double ExpandedWidth
    {
        get => _expandedWidth;
        set
        {
            if (_expandedWidth != value)
            {
                var old = _expandedWidth;
                _expandedWidth = value;
                NotifyModified(nameof(ExpandedWidth), old, value);
            }
        }
    }

    public double CollapsedWidth
    {
        get => _collapsedWidth;
        set
        {
            if (_collapsedWidth != value)
            {
                var old = _collapsedWidth;
                _collapsedWidth = value;
                NotifyModified(nameof(CollapsedWidth), old, value);
            }
        }
    }

    public SidebarNode()
    {
        Name = "Sidebar";
        Width = 260.0;
    }

    public SidebarNode(string name, string dock = "Left") : this()
    {
        Name = name;
        DockPosition = dock;
    }

    public void Expand()
    {
        IsCollapsed = false;
        Width = _expandedWidth;
    }

    public void Collapse()
    {
        IsCollapsed = true;
        Width = _collapsedWidth;
    }

    public override void Toggle()
    {
        if (IsCollapsed)
            Expand();
        else
            Collapse();
    }
}
