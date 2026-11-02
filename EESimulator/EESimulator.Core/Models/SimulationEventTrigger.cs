using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EESimulator.Core.Models;

/// <summary>Component measurement condition that can activate a UI-only global event.</summary>
public partial class SimulationEventTrigger : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _name = "Event";
    [ObservableProperty] private string _measurement = "Voltage";
    [ObservableProperty] private string _comparison = ">=";
    [ObservableProperty] private double _threshold;
    [ObservableProperty] private string _target = "Chat bubble";
    [ObservableProperty] private string _message = "Event triggered";
    [ObservableProperty] private string _mode = "Latched";

    [System.Text.Json.Serialization.JsonIgnore]
    [ObservableProperty]
    [property: JsonIgnore]
    private bool _isFired;

    [JsonIgnore] public IReadOnlyList<string> MeasurementOptions { get; } = new[] { "Voltage", "Current", "State" };
    [JsonIgnore] public IReadOnlyList<string> ComparisonOptions { get; } = new[] { ">", "<", ">=", "<=", "=", "!=" };
    [JsonIgnore] public IReadOnlyList<string> TargetOptions { get; } = new[] { "Dialog", "Waveform drawer", "Description sidebar", "Control sidebar", "Chat bubble" };
    [JsonIgnore] public IReadOnlyList<string> ModeOptions { get; } = new[] { "Latched", "While true" };
}
