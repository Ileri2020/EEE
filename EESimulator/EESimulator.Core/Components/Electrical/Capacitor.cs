using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Linear electrical capacitor with transient integration, initial condition (IC), and temperature modeling.
/// </summary>
public class CapacitorComponent : ElectricalComponent
{
    public const string StableType = "electrical.capacitor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Capacitor";
    public override string SpicePrefix => "C";
    public override string Description => "Linear two-terminal electrical capacitor with IC and transient support";

    public double Capacitance
    {
        get => ElectricalParameters.TryGetValue("Capacitance", out var val) ? val : 1e-6;
        set => ElectricalParameters["Capacitance"] = value;
    }

    public double InitialCondition
    {
        get => ElectricalParameters.TryGetValue("IC", out var val) ? val : 0.0;
        set => ElectricalParameters["IC"] = value;
    }

    public double Temperature
    {
        get => ElectricalParameters.TryGetValue("Temperature", out var val) ? val : 27.0;
        set => ElectricalParameters["Temperature"] = value;
    }

    public double TemperatureCoefficient1
    {
        get => ElectricalParameters.TryGetValue("TC1", out var val) ? val : 0.0;
        set => ElectricalParameters["TC1"] = value;
    }

    public double TemperatureCoefficient2
    {
        get => ElectricalParameters.TryGetValue("TC2", out var val) ? val : 0.0;
        set => ElectricalParameters["TC2"] = value;
    }

    public double EquivalentSeriesResistance
    {
        get => ElectricalParameters.TryGetValue("ESR", out var val) ? val : 0.0;
        set => ElectricalParameters["ESR"] = value;
    }

    public CapacitorComponent()
    {
        Pins.Add(new ComponentPin("pin1", "+", -15, 0));
        Pins.Add(new ComponentPin("pin2", "-", 15, 0));
        Capacitance = 1e-6;
        InitialCondition = 0.0;
        Temperature = 27.0;
        TemperatureCoefficient1 = 0.0;
        TemperatureCoefficient2 = 0.0;
        EquivalentSeriesResistance = 0.0;
    }

    public CapacitorComponent(string name, double capacitance = 1e-6) : this()
    {
        Name = name;
        Reference = name;
        Capacitance = capacitance;
    }
}
