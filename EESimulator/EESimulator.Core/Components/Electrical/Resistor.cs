using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Linear electrical resistor with temperature and noise parameters matching SpiceSharp.
/// </summary>
public class ResistorComponent : ElectricalComponent
{
    public const string StableType = "electrical.resistor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Resistor";
    public override string SpicePrefix => "R";
    public override string Description => "Linear two-terminal electrical resistor with temperature modeling";

    public double Resistance
    {
        get => ElectricalParameters.TryGetValue("Resistance", out var val) ? val : 1000.0;
        set => ElectricalParameters["Resistance"] = value;
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

    public ResistorComponent()
    {
        Pins.Add(new ComponentPin("pin1", "1", -20, 0));
        Pins.Add(new ComponentPin("pin2", "2", 20, 0));
        Resistance = 1000.0;
        Temperature = 27.0;
        TemperatureCoefficient1 = 0.0;
        TemperatureCoefficient2 = 0.0;
    }

    public ResistorComponent(string name, double resistance = 1000.0) : this()
    {
        Name = name;
        Reference = name;
        Resistance = resistance;
    }
}
