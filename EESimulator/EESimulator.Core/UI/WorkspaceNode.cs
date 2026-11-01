using System;

namespace EESimulator.Core.UI;

/// <summary>
/// Workspace UI node. Represents the main application workspace in the RootNode hierarchy.
/// All viewports, sidebars, toolbars, and overlays are children of this node,
/// allowing complete introspection and automation via the Python script interface.
/// </summary>
public class WorkspaceNode : UINode
{
    private string _activeViewport = "Schematic";
    private double _zoom = 1.0;
    private double _panX = 0.0;
    private double _panY = 0.0;
    private bool _showWaveformDrawer = false;
    private bool _showScriptConsole = true;

    public string ActiveViewport
    {
        get => _activeViewport;
        set
        {
            if (_activeViewport != value)
            {
                var old = _activeViewport;
                _activeViewport = value;
                NotifyModified(nameof(ActiveViewport), old, value);
                TriggerValueChanged(value);
            }
        }
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Math.Abs(_zoom - value) > 1e-6)
            {
                var old = _zoom;
                _zoom = value;
                NotifyModified(nameof(Zoom), old, value);
            }
        }
    }

    public double PanX
    {
        get => _panX;
        set
        {
            if (Math.Abs(_panX - value) > 1e-6)
            {
                var old = _panX;
                _panX = value;
                NotifyModified(nameof(PanX), old, value);
            }
        }
    }

    public double PanY
    {
        get => _panY;
        set
        {
            if (Math.Abs(_panY - value) > 1e-6)
            {
                var old = _panY;
                _panY = value;
                NotifyModified(nameof(PanY), old, value);
            }
        }
    }

    public bool ShowWaveformDrawer
    {
        get => _showWaveformDrawer;
        set
        {
            if (_showWaveformDrawer != value)
            {
                var old = _showWaveformDrawer;
                _showWaveformDrawer = value;
                NotifyModified(nameof(ShowWaveformDrawer), old, value);
                TriggerStateChanged();
            }
        }
    }

    public bool ShowScriptConsole
    {
        get => _showScriptConsole;
        set
        {
            if (_showScriptConsole != value)
            {
                var old = _showScriptConsole;
                _showScriptConsole = value;
                NotifyModified(nameof(ShowScriptConsole), old, value);
                TriggerStateChanged();
            }
        }
    }

    public WorkspaceNode()
    {
        Name = "Workspace";
        Title = "EE Schematic & Python Workspace";
    }

    public void SetViewport(string viewport)
    {
        ActiveViewport = viewport;
    }

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(zoom, 0.1, 10.0);
    }

    public void ToggleWaveform()
    {
        ShowWaveformDrawer = !ShowWaveformDrawer;
    }

    public void ToggleScriptConsole()
    {
        ShowScriptConsole = !ShowScriptConsole;
    }

    public void ResetView()
    {
        Zoom = 1.0;
        PanX = 0.0;
        PanY = 0.0;
    }
}
