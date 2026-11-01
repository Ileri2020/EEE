using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Lossless Transmission Line (T-line).
/// SPICE Prefix: T.
/// Parameters: Z0 (characteristic impedance), TD (transmission delay), F (frequency), NL (normalized length).
/// </summary>
public class TransmissionLineComponent : ElectricalComponent
{
    public const string StableType = "electrical.transmissionline";

    public override string StableTypeId => StableType;
    public override string ComponentType => "TransmissionLine";
    public override string SpicePrefix => "T";
    public override string Description => "Lossless Transmission Line (T)";

    public double CharacteristicImpedance
    {
        get => ElectricalParameters.TryGetValue("Z0", out var val) ? val : 50.0;
        set => ElectricalParameters["Z0"] = value;
    }

    public double Delay
    {
        get => ElectricalParameters.TryGetValue("TD", out var val) ? val : 1e-9;
        set => ElectricalParameters["TD"] = value;
    }

    public double Frequency
    {
        get => ElectricalParameters.TryGetValue("F", out var val) ? val : 1e9;
        set => ElectricalParameters["F"] = value;
    }

    public double NormalizedLength
    {
        get => ElectricalParameters.TryGetValue("NL", out var val) ? val : 0.25;
        set => ElectricalParameters["NL"] = value;
    }

    public TransmissionLineComponent()
    {
        // Port 1 (Left terminals)
        Pins.Add(new ComponentPin("pos1", "P1+", -20, -10));
        Pins.Add(new ComponentPin("neg1", "P1−", -20, 10));

        // Port 2 (Right terminals)
        Pins.Add(new ComponentPin("pos2", "P2+", 20, -10));
        Pins.Add(new ComponentPin("neg2", "P2−", 20, 10));

        CharacteristicImpedance = 50.0;
        Delay = 1e-9;
        Frequency = 1e9;
        NormalizedLength = 0.25;
    }

    public TransmissionLineComponent(string name, double z0 = 50.0, double td = 1e-9) : this()
    {
        Name = name;
        Reference = name;
        CharacteristicImpedance = z0;
        Delay = td;
    }
}
