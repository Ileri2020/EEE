using CommunityToolkit.Mvvm.ComponentModel;

namespace EESimulator.Core.Models;

public enum WaveformSignalKind
{
    ComponentVoltage,
    ComponentCurrent,
    PinVoltage
}

/// <summary>One component or terminal signal that can be plotted in the waveform drawer.</summary>
public partial class WaveformSignal : ObservableObject
{
    public ComponentNode Component { get; }
    public string SignalId { get; }
    public string Label { get; }
    public WaveformSignalKind Kind { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    public WaveformSignal(ComponentNode component, string signalId, string label, WaveformSignalKind kind)
    {
        Component = component;
        SignalId = signalId;
        Label = label;
        Kind = kind;
    }
}
