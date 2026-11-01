using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Light Emitting Diode component.
/// </summary>
public class LEDComponent : ElectronicComponent
{
    public const string StableType = "electronics.led";

    public override string StableTypeId => StableType;
    public override string ComponentType => "LED";
    public override string Description => "Light Emitting Diode";

    public double ForwardVoltage
    {
        get => ElectricalParameters.TryGetValue("Vf", out var val) ? val : 2.0;
        set => ElectricalParameters["Vf"] = value;
    }

    public string ColorName { get; set; } = "Red";

    /// <summary>
    /// Indicates whether the LED is currently illuminated (emitting light).
    /// </summary>
    public bool IsOn
    {
        get
        {
            if (CustomProperties.TryGetValue("IsOn", out var val) && val is bool bVal)
                return bVal;

            if (ElectricalParameters.TryGetValue("IsOn", out var pVal))
                return pVal > 0.5;

            // Lights up when forward voltage drop is near or above forward voltage rating, or current flows forward
            return LiveVoltage >= Math.Min(ForwardVoltage * 0.75, 1.5) || LiveCurrent >= 0.001;
        }
        set
        {
            CustomProperties["IsOn"] = value;
            ElectricalParameters["IsOn"] = value ? 1.0 : 0.0;
            NotifyModified(nameof(IsOn));
        }
    }

    public LEDComponent()
    {
        Pins.Add(new ComponentPin("anode", "A", -20, 0));
        Pins.Add(new ComponentPin("cathode", "K", 20, 0));
        ForwardVoltage = 2.0;
    }

    public LEDComponent(string name, string color = "Red") : this()
    {
        Name = name;
        Reference = name;
        ColorName = color;
    }
}
