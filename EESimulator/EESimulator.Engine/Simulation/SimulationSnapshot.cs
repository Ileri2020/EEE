using System;
using System.Collections.Generic;
using EESimulator.Core.Models;

namespace EESimulator.Engine.Simulation;

/// <summary>
/// Immutable snapshot of electrical simulation results and component states.
/// Published by the simulation worker to decouple electrical solves from UI rendering.
/// </summary>
public class SimulationSnapshot
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public AnalysisType Analysis { get; init; }
    public double SolverTimeSeconds { get; init; }
    public double DeviceTimeSeconds { get; init; }

    public IReadOnlyDictionary<string, double> OperatingPointVoltages { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> OperatingPointCurrents { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<Guid, ComponentLiveState> ComponentStates { get; init; } = new Dictionary<Guid, ComponentLiveState>();
}

public readonly record struct ComponentLiveState(
    double LiveVoltage,
    double LiveCurrent,
    bool IsOn,
    double Brightness,
    bool IsBrokenDown
);

public static class SimulationSnapshotFactory
{
    public static SimulationSnapshot CreateSnapshot(CircuitNode circuit, SimulationSettings settings, SimulationResultsData results)
    {
        var states = new Dictionary<Guid, ComponentLiveState>();
        if (circuit?.Components != null)
        {
            foreach (var comp in circuit.Components)
            {
                bool isOn = false;
                double brightness = 0.0;
                if (comp is EESimulator.Core.Components.Electronics.LEDComponent led)
                    isOn = led.IsOn;
                else if (comp is EESimulator.Core.Components.Electrical.BulbComponent bulb)
                {
                    isOn = bulb.IsOn;
                    brightness = bulb.Brightness;
                }
                else if (comp is EESimulator.Core.Components.Electronics.LogicOutputComponent lout)
                    isOn = lout.IsOn;

                states[comp.Id] = new ComponentLiveState(
                    comp.LiveVoltage,
                    comp.LiveCurrent,
                    isOn,
                    brightness,
                    comp.IsBrokenDown
                );
            }
        }

        return new SimulationSnapshot
        {
            Success = results?.Success ?? false,
            Message = results?.Message ?? string.Empty,
            Analysis = settings.Analysis,
            SolverTimeSeconds = settings.StopTime,
            DeviceTimeSeconds = settings.DeviceTimeSeconds,
            OperatingPointVoltages = results != null ? new Dictionary<string, double>(results.OperatingPointVoltages) : new Dictionary<string, double>(),
            OperatingPointCurrents = results != null ? new Dictionary<string, double>(results.OperatingPointCurrents) : new Dictionary<string, double>(),
            ComponentStates = states
        };
    }
}

