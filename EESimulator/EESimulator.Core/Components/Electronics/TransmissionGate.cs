using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// CMOS Transmission Gate (Bilateral Analog Switch).
/// Composed of parallel N-MOSFET and P-MOSFET transistors controlled by complementary enable lines
/// (Ctrl and CtrlInv). When Ctrl is HIGH and CtrlInv is LOW, the switch is closed with low Ron;
/// otherwise, the switch is open (high impedance Roff).
/// </summary>
public class TransmissionGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.transmissiongate";

    public override string StableTypeId => StableType;
    public override string ComponentType => "TransmissionGate";
    public override string Description => "CMOS Transmission Gate (Bilateral Switch)";

    /// <summary>On-state resistance (Ω). Default 0.2 Ω.</summary>
    public double ResistanceOn
    {
        get => ElectricalParameters.TryGetValue("Ron", out var val) ? val : 0.2;
        set
        {
            ElectricalParameters["Ron"] = Math.Max(value, 1e-4);
            NotifyModified(nameof(ResistanceOn));
        }
    }

    /// <summary>Off-state resistance (Ω). Default 10 MΩ.</summary>
    public double ResistanceOff
    {
        get => ElectricalParameters.TryGetValue("Roff", out var val) ? val : 10e6;
        set
        {
            ElectricalParameters["Roff"] = Math.Max(value, 100.0);
            NotifyModified(nameof(ResistanceOff));
        }
    }

    /// <summary>Logisim Select Pin Location: TopRight or BottomLeft (StdAttr.SELECT_LOC).</summary>
    public GateLocation SelectLocation
    {
        get
        {
            if (CustomProperties.TryGetValue("SelectLocation", out var val) && val is GateLocation g)
                return g;
            if (CustomProperties.TryGetValue("SelectLocation", out var strVal) && Enum.TryParse<GateLocation>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return GateLocation.TopRight;
        }
        set
        {
            CustomProperties["SelectLocation"] = value;
            NotifyModified(nameof(SelectLocation));
        }
    }

    /// <summary>Logisim Bit Width (1 to 32 bits, default 1).</summary>
    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            NotifyModified(nameof(BitWidth));
        }
    }

    public TransmissionGateComponent()
    {
        // In (left), Out (right), Ctrl (top, active-high), CtrlInv (bottom, active-low)
        Pins.Add(new ComponentPin("in", "IN", -20, 0));
        Pins.Add(new ComponentPin("out", "OUT", 20, 0));
        Pins.Add(new ComponentPin("ctrl", "EN", 0, -18));
        Pins.Add(new ComponentPin("ctrl_inv", "EN~", 0, 18));

        ResistanceOn = 0.2;
        ResistanceOff = 10e6;
    }

    public TransmissionGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
