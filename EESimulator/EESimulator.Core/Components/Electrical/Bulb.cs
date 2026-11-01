using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Incandescent Light Bulb component.
/// Electrically behaves as a resistive filament load that illuminates when powered.
/// </summary>
public class BulbComponent : ElectricalComponent
{
    public const string StableType = "electrical.bulb";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Bulb";
    public override string SpicePrefix => "R";   // Bulb modelled as a resistive load in SPICE
    public override string Description => "Incandescent Light Bulb";


    public double Resistance
    {
        get => ElectricalParameters.TryGetValue("Resistance", out var val) ? val : 50.0;
        set => ElectricalParameters["Resistance"] = value;
    }

    public double ThresholdVoltage
    {
        get => ElectricalParameters.TryGetValue("ThresholdVoltage", out var val) ? val : 1.0;
        set => ElectricalParameters["ThresholdVoltage"] = value;
    }

    public double RatedVoltage
    {
        get => ElectricalParameters.TryGetValue("RatedVoltage", out var val) ? val : 6.0;
        set => ElectricalParameters["RatedVoltage"] = value;
    }

    /// <summary>
    /// Indicates whether the light bulb is currently illuminated.
    /// </summary>
    public bool IsOn
    {
        get
        {
            if (CustomProperties.TryGetValue("IsOn", out var val) && val is bool bVal)
                return bVal;

            if (ElectricalParameters.TryGetValue("IsOn", out var pVal))
                return pVal > 0.5;

            return LiveVoltage >= ThresholdVoltage || LiveCurrent >= 0.02;
        }
        set
        {
            CustomProperties["IsOn"] = value;
            ElectricalParameters["IsOn"] = value ? 1.0 : 0.0;
            NotifyModified(nameof(IsOn));
        }
    }

    public BulbComponent()
    {
        Pins.Add(new ComponentPin("pin1", "1", -20, 0));
        Pins.Add(new ComponentPin("pin2", "2", 20, 0));
        Resistance = 50.0;
        ThresholdVoltage = 1.0;
        RatedVoltage = 6.0;
    }

    public BulbComponent(string name, double resistance = 50.0, double ratedVoltage = 6.0) : this()
    {
        Name = name;
        Reference = name;
        Resistance = resistance;
        RatedVoltage = ratedVoltage;
    }
}
