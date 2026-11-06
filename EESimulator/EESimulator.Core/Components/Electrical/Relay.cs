using System;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>Electromagnetic relay with a resistive coil and normally-open voltage-controlled contact.</summary>
public sealed class RelayComponent : PowerSystemComponent
{
    public override string ComponentType => "Relay";
    public override string StableTypeId => "electrical.relay";
    public override string SpicePrefix => "KRL";
    public override string Description => "Electromagnetic relay with coil and normally-open contact";

    public double CoilResistance
    {
        get => ElectricalParameters.TryGetValue(nameof(CoilResistance), out var value) ? value : 400;
        set { ElectricalParameters[nameof(CoilResistance)] = Math.Max(value, 1e-3); NotifyModified(nameof(CoilResistance)); }
    }

    public double PullInVoltage
    {
        get => ElectricalParameters.TryGetValue(nameof(PullInVoltage), out var value) ? value : 5;
        set { ElectricalParameters[nameof(PullInVoltage)] = Math.Max(value, 0); NotifyModified(nameof(PullInVoltage)); }
    }

    public double DropoutVoltage
    {
        get => ElectricalParameters.TryGetValue(nameof(DropoutVoltage), out var value) ? value : 2;
        set { ElectricalParameters[nameof(DropoutVoltage)] = Math.Max(value, 0); NotifyModified(nameof(DropoutVoltage)); }
    }

    public double ContactResistance
    {
        get => ElectricalParameters.TryGetValue(nameof(ContactResistance), out var value) ? value : 0.05;
        set { ElectricalParameters[nameof(ContactResistance)] = Math.Max(value, 1e-6); NotifyModified(nameof(ContactResistance)); }
    }

    public double OpenResistance
    {
        get => ElectricalParameters.TryGetValue(nameof(OpenResistance), out var value) ? value : 1e9;
        set { ElectricalParameters[nameof(OpenResistance)] = Math.Max(value, 1); NotifyModified(nameof(OpenResistance)); }
    }

    public RelayComponent()
    {
        Pins.Add(new ComponentPin("coil_positive", "Coil +", -22, -14));
        Pins.Add(new ComponentPin("coil_negative", "Coil -", -22, 14));
        Pins.Add(new ComponentPin("common", "COM", 22, -14));
        Pins.Add(new ComponentPin("normally_open", "NO", 22, 14));
        CoilResistance = 400;
        PullInVoltage = 5;
        DropoutVoltage = 2;
        ContactResistance = 0.05;
        OpenResistance = 1e9;
    }

    public override void ValidateConfiguration()
    {
        if (DropoutVoltage >= PullInVoltage)
            throw new InvalidOperationException("Relay dropout voltage must be lower than pull-in voltage.");
    }
}
