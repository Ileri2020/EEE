using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EESimulator.Core.Models;

/// <summary>Component measurement condition that can activate a UI-only global event.</summary>
public partial class SimulationEventTrigger : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] public partial string Name { get; set; } = "Event";
    [ObservableProperty] public partial string Measurement { get; set; } = "Voltage";
    [ObservableProperty] public partial string Comparison { get; set; } = ">=";
    [ObservableProperty] public partial double Threshold { get; set; }
    [ObservableProperty] public partial string Target { get; set; } = "Chat bubble";
    [ObservableProperty] public partial string Message { get; set; } = "Event triggered";
    [ObservableProperty] public partial string Mode { get; set; } = "Latched";

    [ObservableProperty]
    [JsonIgnore]
    public partial bool IsFired { get; set; }

    [JsonIgnore] public IReadOnlyList<string> MeasurementOptions { get; } = new[] { "Voltage", "Current", "State" };
    [JsonIgnore] public IReadOnlyList<string> ComparisonOptions { get; } = new[] { ">", "<", ">=", "<=", "=", "!=" };
    [JsonIgnore] public IReadOnlyList<string> TargetOptions { get; } = new[] { "Dialog", "Waveform drawer", "Description sidebar", "Control sidebar", "Chat bubble" };
    [JsonIgnore] public IReadOnlyList<string> ModeOptions { get; } = new[] { "Latched", "While true" };
}
