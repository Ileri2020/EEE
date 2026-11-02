using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Coaxial Cable Transmission Line.
/// Models RF/HF coaxial cables (e.g. RG-58, RG-59, RG-6) with characteristic impedance Z0,
/// velocity factor VF (propagation velocity v = VF * c), physical length L, and attenuation.
/// </summary>
public class CoaxialCableComponent : ElectricalComponent
{
    public const string StableType = "electrical.coaxialcable";

    public override string StableTypeId => StableType;
    public override string ComponentType => "CoaxialCable";
    public override string SpicePrefix => "COAX";
    public override string Description => "Coaxial Cable Transmission Line";

    private const double SpeedOfLight = 299792458.0;

    /// <summary>Characteristic impedance in Ohms (e.g., 50.0 Ω or 75.0 Ω). Default 50 Ω.</summary>
    public double CharacteristicImpedance
    {
        get => ElectricalParameters.TryGetValue("Z0", out var val) ? val : 50.0;
        set => ElectricalParameters["Z0"] = Math.Max(value, 1.0);
    }

    /// <summary>Velocity factor (fraction of c, e.g. 0.66 for solid PE, 0.82 for foam PE). Default 0.66.</summary>
    public double VelocityFactor
    {
        get => ElectricalParameters.TryGetValue("VF", out var val) ? Math.Clamp(val, 0.1, 1.0) : 0.66;
        set => ElectricalParameters["VF"] = Math.Clamp(value, 0.1, 1.0);
    }

    /// <summary>Cable length in meters (m). Default 1.0 m.</summary>
    public double Length
    {
        get => ElectricalParameters.TryGetValue("Length", out var val) ? val : 1.0;
        set => ElectricalParameters["Length"] = Math.Max(value, 0.01);
    }

    /// <summary>Attenuation in dB/meter at nominal frequency. Default 0.05 dB/m.</summary>
    public double Attenuation
    {
        get => ElectricalParameters.TryGetValue("Attenuation", out var val) ? val : 0.05;
        set => ElectricalParameters["Attenuation"] = Math.Max(value, 0.0);
    }

    /// <summary>One-way delay TD = Length / (VF * c) in seconds.</summary>
    public double Delay => Length / (VelocityFactor * SpeedOfLight);

    public CoaxialCableComponent()
    {
        // Port 1 (Left): Center conductor (Core) & Outer Shield (GND)
        Pins.Add(new ComponentPin("core_in", "CORE_IN", -20, -10));
        Pins.Add(new ComponentPin("shield_in", "SHIELD_IN", -20, 10));

        // Port 2 (Right): Center conductor & Outer Shield
        Pins.Add(new ComponentPin("core_out", "CORE_OUT", 20, -10));
        Pins.Add(new ComponentPin("shield_out", "SHIELD_OUT", 20, 10));

        CharacteristicImpedance = 50.0;
        VelocityFactor = 0.66;
        Length = 1.0;
        Attenuation = 0.05;
    }

    public CoaxialCableComponent(string name, double z0 = 50.0, double length = 1.0, double vf = 0.66) : this()
    {
        Name = name;
        Reference = name;
        CharacteristicImpedance = z0;
        Length = length;
        VelocityFactor = vf;
    }
}
