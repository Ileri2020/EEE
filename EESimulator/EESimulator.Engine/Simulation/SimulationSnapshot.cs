using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public IReadOnlyDictionary<Guid, ComponentWaveformState> ComponentWaveforms { get; init; } = new Dictionary<Guid, ComponentWaveformState>();
}

public readonly record struct ComponentLiveState(
    double LiveVoltage,
    double LiveCurrent,
    bool IsOn,
    double Brightness,
    bool IsBrokenDown,
    IReadOnlyDictionary<string, double> PinVoltages
);

public sealed record ComponentWaveformState(
    IReadOnlyList<double> Times,
    IReadOnlyList<double> Voltages,
    IReadOnlyList<double> Currents,
    IReadOnlyDictionary<string, IReadOnlyList<double>> NodeVoltages,
    IReadOnlyDictionary<string, IReadOnlyList<double>> PinCurrents
);

public static class SimulationSnapshotFactory
{
    public static SimulationSnapshot CreateSnapshot(CircuitNode circuit, SimulationSettings settings, SimulationResultsData results)
    {
        var states = new Dictionary<Guid, ComponentLiveState>();
        var waveforms = new Dictionary<Guid, ComponentWaveformState>();
        if (circuit != null)
        {
            var components = circuit.Components;
            var componentStates = new KeyValuePair<Guid, ComponentLiveState>[components.Count];
            void ExtractComponentState(int index)
            {
                var comp = components[index];
                bool isOn = comp switch
                {
                    EESimulator.Core.Components.Electronics.LEDComponent led => led.IsOn,
                    EESimulator.Core.Components.Electrical.BulbComponent visualBulb => visualBulb.IsOn,
                    EESimulator.Core.Components.Electrical.ACBulbComponent acBulb => acBulb.InitiallyOn && acBulb.ScheduleActive,
                    EESimulator.Core.Components.Electronics.LogicOutputComponent logicOutput => logicOutput.IsOn,
                    EESimulator.Core.Components.Electronics.LogicInputComponent logicInput => logicInput.State,
                    EESimulator.Core.Components.Electronics.ClockComponent clock => clock.State,
                    EESimulator.Core.Components.Electronics.PinComponent pin => pin.Value > 0,
                    EESimulator.Core.Components.Electronics.ConstantComponent constant => constant.Value > 0,
                    EESimulator.Core.Components.Electronics.ButtonComponent button => button.IsPressed,
                    _ => (comp.CustomProperties.TryGetValue("IsOn", out var customOn) &&
                          bool.TryParse(customOn?.ToString(), out var parsedOn) && parsedOn) ||
                         (comp.ElectricalParameters.TryGetValue("IsOn", out var parameterOn) && parameterOn > 0.5) ||
                         (comp.ComponentType == "LED" && comp.LiveVoltage >= 1.5) ||
                         (comp.ComponentType == "Bulb" && comp.LiveVoltage >= 1.0)
                };
                double brightness = 0.0;
                if (comp is EESimulator.Core.Components.Electrical.BulbComponent bulb)
                {
                    brightness = bulb.Brightness;
                }

                componentStates[index] = new KeyValuePair<Guid, ComponentLiveState>(
                    comp.Id,
                    new ComponentLiveState(
                        comp.LiveVoltage,
                        comp.LiveCurrent,
                        isOn,
                        brightness,
                        comp.IsBrokenDown,
                        new Dictionary<string, double>(comp.LivePinVoltages, StringComparer.OrdinalIgnoreCase)));
            }

            if (components.Count >= 8)
                Parallel.For(0, components.Count, ExtractComponentState);
            else
                for (int index = 0; index < components.Count; index++)
                    ExtractComponentState(index);

            foreach (var componentState in componentStates)
                states.Add(componentState.Key, componentState.Value);

            foreach (var component in components)
            {
                if (component.WaveformTime.Count == 0)
                    continue;

                var nodeVoltages = component.WaveformNodeVoltages.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<double>)Array.AsReadOnly(pair.Value.ToArray()),
                    StringComparer.OrdinalIgnoreCase);
                var pinCurrents = component.WaveformPinCurrents.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<double>)Array.AsReadOnly(pair.Value.ToArray()),
                    StringComparer.OrdinalIgnoreCase);
                waveforms[component.Id] = new ComponentWaveformState(
                    Array.AsReadOnly(component.WaveformTime.ToArray()),
                    Array.AsReadOnly(component.WaveformVoltage.ToArray()),
                    Array.AsReadOnly(component.WaveformCurrent.ToArray()),
                    nodeVoltages,
                    pinCurrents);
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
            ComponentStates = states,
            ComponentWaveforms = waveforms
        };
    }
}
