using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Printed Circuit Board Microstrip Transmission Line.
/// Models a microstrip conductor above a ground plane separated by a dielectric substrate.
/// Parameters: Trace Width W (m), Substrate Height H (m), Dielectric Constant Er (e.g. 4.4 for FR-4),
/// and Length L (m). Automatically calculates effective dielectric constant (E_eff), characteristic impedance (Z0),
/// and propagation delay (TD) via Wheeler/Hammerstad microstrip synthesis formulas.
/// </summary>
public class MicrostripLineComponent : ElectricalComponent
{
    public const string StableType = "electrical.microstripline";

    public override string StableTypeId => StableType;
    public override string ComponentType => "MicrostripLine";
    public override string SpicePrefix => "USTRIP";
    public override string Description => "PCB Microstrip Line (Planar Transmission Line)";

    private const double SpeedOfLight = 299792458.0;

    /// <summary>Trace width W in meters (e.g. 1.5e-3 = 1.5 mm). Default 1.5 mm.</summary>
    public double TraceWidth
    {
        get => ElectricalParameters.TryGetValue("W", out var val) ? val : 1.5e-3;
        set => ElectricalParameters["W"] = Math.Max(value, 1e-6);
    }

    /// <summary>Substrate dielectric thickness H in meters (e.g. 0.8e-3 = 0.8 mm). Default 0.8 mm.</summary>
    public double SubstrateHeight
    {
        get => ElectricalParameters.TryGetValue("H", out var val) ? val : 0.8e-3;
        set => ElectricalParameters["H"] = Math.Max(value, 1e-6);
    }

    /// <summary>Relative dielectric permittivity εr (e.g. 4.4 for standard FR-4, 3.55 for Rogers 4003C). Default 4.4.</summary>
    public double DielectricConstant
    {
        get => ElectricalParameters.TryGetValue("Er", out var val) ? val : 4.4;
        set => ElectricalParameters["Er"] = Math.Max(value, 1.0);
    }

    /// <summary>Physical trace length L in meters (e.g. 0.05 = 50 mm). Default 0.05 m (50 mm).</summary>
    public double Length
    {
        get => ElectricalParameters.TryGetValue("Length", out var val) ? val : 0.05;
        set => ElectricalParameters["Length"] = Math.Max(value, 1e-4);
    }

    /// <summary>Effective relative dielectric permittivity (Hammerstad formula).</summary>
    public double EffectiveDielectric
    {
        get
        {
            double u = TraceWidth / SubstrateHeight;
            double er = DielectricConstant;
            return ((er + 1.0) / 2.0) + ((er - 1.0) / 2.0) * Math.Pow(1.0 + 12.0 / u, -0.5);
        }
    }

    /// <summary>Synthesized characteristic impedance Z0 in Ohms via Wheeler/Hammerstad formula.</summary>
    public double CharacteristicImpedance
    {
        get
        {
            double u = TraceWidth / SubstrateHeight;
            double ee = EffectiveDielectric;
            if (u <= 1.0)
            {
                return (60.0 / Math.Sqrt(ee)) * Math.Log(8.0 / u + 0.25 * u);
            }
            else
            {
                return (120.0 * Math.PI) / (Math.Sqrt(ee) * (u + 1.393 + 0.667 * Math.Log(u + 1.444)));
            }
        }
    }

    /// <summary>One-way propagation delay TD = Length * sqrt(E_eff) / c in seconds.</summary>
    public double Delay => (Length * Math.Sqrt(EffectiveDielectric)) / SpeedOfLight;

    public MicrostripLineComponent()
    {
        // Port 1 (Input trace & ground)
        Pins.Add(new ComponentPin("in", "IN", -20, -10));
        Pins.Add(new ComponentPin("gnd_in", "GND1", -20, 10));

        // Port 2 (Output trace & ground)
        Pins.Add(new ComponentPin("out", "OUT", 20, -10));
        Pins.Add(new ComponentPin("gnd_out", "GND2", 20, 10));

        TraceWidth = 1.5e-3;
        SubstrateHeight = 0.8e-3;
        DielectricConstant = 4.4;
        Length = 0.05;
    }

    public MicrostripLineComponent(string name, double traceWidth = 1.5e-3, double substrateHeight = 0.8e-3, double er = 4.4, double length = 0.05) : this()
    {
        Name = name;
        Reference = name;
        TraceWidth = traceWidth;
        SubstrateHeight = substrateHeight;
        DielectricConstant = er;
        Length = length;
    }
}
