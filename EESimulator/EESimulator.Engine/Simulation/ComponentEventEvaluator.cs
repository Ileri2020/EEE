using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Models;

namespace EESimulator.Engine.Simulation;

public sealed record FiredComponentEvent(Guid ComponentId, string ComponentReference, string TriggerId, string Name, string Target, string Message);

/// <summary>Evaluates measurement triggers after a circuit solve; targets are intentionally UI-only.</summary>
public static class ComponentEventEvaluator
{
    public static IReadOnlyList<FiredComponentEvent> Evaluate(CircuitNode circuit)
    {
        var active = new List<FiredComponentEvent>();
        foreach (var component in circuit.Components)
        {
            foreach (var trigger in component.EventTriggers)
            {
                bool condition = Compare(ReadMeasurement(component, trigger.Measurement), trigger.Comparison, trigger.Threshold);
                if (trigger.Mode.Equals("While true", StringComparison.OrdinalIgnoreCase))
                    trigger.IsFired = condition;
                else if (condition)
                    trigger.IsFired = true;

                if (trigger.IsFired)
                    active.Add(new FiredComponentEvent(component.Id, component.Reference, trigger.Id, trigger.Name, trigger.Target, trigger.Message));
            }
        }
        return active;
    }

    private static double ReadMeasurement(ComponentNode component, string measurement)
    {
        if (measurement.Equals("Current", StringComparison.OrdinalIgnoreCase)) return component.LiveCurrent;
        if (measurement.Equals("State", StringComparison.OrdinalIgnoreCase)) return ReadState(component) ? 1 : 0;
        return component.LiveVoltage;
    }

    private static bool ReadState(ComponentNode component) => component switch
    {
        LogicInputComponent input => input.State,
        LogicOutputComponent output => output.IsOn,
        ClockComponent clock => clock.State,
        ButtonComponent button => button.IsPressed,
        ConstantComponent constant => constant.Value > 0,
        _ when component.CustomProperties.TryGetValue("IsOn", out var on) && bool.TryParse(on?.ToString(), out var parsed) => parsed,
        _ => false
    };

    private static bool Compare(double value, string comparison, double threshold) => comparison switch
    {
        ">" => value > threshold,
        "<" => value < threshold,
        ">=" => value >= threshold,
        "<=" => value <= threshold,
        "=" or "==" => Math.Abs(value - threshold) <= 1e-9,
        "!=" => Math.Abs(value - threshold) > 1e-9,
        _ => false
    };
}
