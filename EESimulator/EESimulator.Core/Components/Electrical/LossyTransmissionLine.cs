using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Distributed Lossy Transmission Line (RLGC model).
/// Models a transmission line with distributed resistance R (Ω/m), inductance L (H/m),
/// conductance G (S/m), capacitance C (F/m), and physical length (m).
/// </summary>
public class LossyTransmissionLineComponent : ElectricalComponent
{
    public const string StableType = "electrical.lossytransmissionline";

    public override string StableTypeId => StableType;
    public override string ComponentType => "LossyTransmissionLine";
    public override string SpicePrefix => "TLOSS";
    public override string Description => "Lossy Transmission Line (RLGC Distributed Model)";

    /// <summary>Distributed series resistance in Ohms/meter (Ω/m). Default 0.2 Ω/m.</summary>
    public double ResistancePerMeter
    {
        get => ElectricalParameters.TryGetValue("R", out var val) ? val : 0.2;
        set => ElectricalParameters["R"] = Math.Max(value, 0.0);
    }

    /// <summary>Distributed series inductance in Henries/meter (H/m). Default 250 nH/m.</summary>
    public double InductancePerMeter
    {
        get => ElectricalParameters.TryGetValue("L", out var val) ? val : 250e-9;
        set => ElectricalParameters["L"] = Math.Max(value, 1e-12);
    }

    /// <summary>Distributed shunt conductance in Siemens/meter (S/m). Default 10 μS/m.</summary>
    public double ConductancePerMeter
    {
        get => ElectricalParameters.TryGetValue("G", out var val) ? val : 10e-6;
        set => ElectricalParameters["G"] = Math.Max(value, 0.0);
    }

    /// <summary>Distributed shunt capacitance in Farads/meter (F/m). Default 100 pF/m.</summary>
    public double CapacitancePerMeter
    {
        get => ElectricalParameters.TryGetValue("C", out var val) ? val : 100e-12;
        set => ElectricalParameters["C"] = Math.Max(value, 1e-15);
    }

    /// <summary>Physical length in meters (m). Default 1.0 m.</summary>
    public double Length
    {
        get => ElectricalParameters.TryGetValue("Length", out var val) ? val : 1.0;
        set => ElectricalParameters["Length"] = Math.Max(value, 1e-3);
    }

    /// <summary>Characteristic impedance Z0 = sqrt(L/C) in Ohms.</summary>
    public double CharacteristicImpedance =>
        CapacitancePerMeter > 0 ? Math.Sqrt(InductancePerMeter / CapacitancePerMeter) : 50.0;

    /// <summary>Total one-way propagation delay TD = Length * sqrt(L*C) in seconds.</summary>
    public double TotalDelay => Length * Math.Sqrt(InductancePerMeter * CapacitancePerMeter);

    public LossyTransmissionLineComponent()
    {
        // Port 1 (Left terminals)
        Pins.Add(new ComponentPin("pos1", "P1+", -20, -10));
        Pins.Add(new ComponentPin("neg1", "P1−", -20, 10));

        // Port 2 (Right terminals)
        Pins.Add(new ComponentPin("pos2", "P2+", 20, -10));
        Pins.Add(new ComponentPin("neg2", "P2−", 20, 10));

        ResistancePerMeter = 0.2;
        InductancePerMeter = 250e-9;
        ConductancePerMeter = 10e-6;
        CapacitancePerMeter = 100e-12;
        Length = 1.0;
    }

    public LossyTransmissionLineComponent(string name, double length = 1.0) : this()
    {
        Name = name;
        Reference = name;
        Length = length;
    }
}
