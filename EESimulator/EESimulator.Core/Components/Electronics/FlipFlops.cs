using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum ClockTriggerType
{
    RisingEdge,
    FallingEdge,
    HighLevel,
    LowLevel
}

/// <summary>
/// Edge-triggered D-Type Flip-Flop with asynchronous Set (PRE) and Reset (CLR).
/// On active clock edge, Q latches the logic level of D.
/// </summary>
public class DFlipFlopComponent : ElectronicComponent
{
    public const string StableType = "electronics.dff";

    public override string StableTypeId => StableType;
    public override string ComponentType => "DFlipFlop";
    public override string SpicePrefix => "DFF";
    public override string Description => "Edge-triggered D-Type Flip-Flop";

    public bool StateQ
    {
        get => CustomProperties.TryGetValue("StateQ", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["StateQ"] = value;
            NotifyModified(nameof(StateQ));
        }
    }

    public ClockTriggerType Trigger
    {
        get => CustomProperties.TryGetValue("Trigger", out var val) && val is ClockTriggerType t ? t : ClockTriggerType.RisingEdge;
        set
        {
            CustomProperties["Trigger"] = value;
            NotifyModified(nameof(Trigger));
        }
    }

    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public DFlipFlopComponent()
    {
        Pins.Add(new ComponentPin("D", "D", -20, -10));
        Pins.Add(new ComponentPin("CLK", "CLK", -20, 10));
        Pins.Add(new ComponentPin("Q", "Q", 20, -10));
        Pins.Add(new ComponentPin("Q_bar", "Q̄", 20, 10));
        Pins.Add(new ComponentPin("reset", "CLR", 0, 20));
        Pins.Add(new ComponentPin("set", "PRE", 0, -20));

        StateQ = false;
        Trigger = ClockTriggerType.RisingEdge;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
    }

    public DFlipFlopComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}

/// <summary>
/// Edge-triggered JK-Type Flip-Flop.
/// When J=1, K=1, the output toggles on every active clock edge.
/// </summary>
public class JKFlipFlopComponent : ElectronicComponent
{
    public const string StableType = "electronics.jkff";

    public override string StableTypeId => StableType;
    public override string ComponentType => "JKFlipFlop";
    public override string SpicePrefix => "JKFF";
    public override string Description => "Edge-triggered JK-Type Flip-Flop";

    public bool StateQ
    {
        get => CustomProperties.TryGetValue("StateQ", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["StateQ"] = value;
            NotifyModified(nameof(StateQ));
        }
    }

    public ClockTriggerType Trigger
    {
        get => CustomProperties.TryGetValue("Trigger", out var val) && val is ClockTriggerType t ? t : ClockTriggerType.RisingEdge;
        set
        {
            CustomProperties["Trigger"] = value;
            NotifyModified(nameof(Trigger));
        }
    }

    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public JKFlipFlopComponent()
    {
        Pins.Add(new ComponentPin("J", "J", -20, -14));
        Pins.Add(new ComponentPin("CLK", "CLK", -20, 0));
        Pins.Add(new ComponentPin("K", "K", -20, 14));
        Pins.Add(new ComponentPin("Q", "Q", 20, -10));
        Pins.Add(new ComponentPin("Q_bar", "Q̄", 20, 10));
        Pins.Add(new ComponentPin("reset", "CLR", 0, 20));

        StateQ = false;
        Trigger = ClockTriggerType.RisingEdge;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
    }

    public JKFlipFlopComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}

/// <summary>
/// Clocked Set-Reset (SR) Flip-Flop / Latch.
/// S=1 sets Q to 1, R=1 resets Q to 0. S=1 and R=1 is an invalid/indeterminate state.
/// </summary>
public class SRFlipFlopComponent : ElectronicComponent
{
    public const string StableType = "electronics.srff";

    public override string StableTypeId => StableType;
    public override string ComponentType => "SRFlipFlop";
    public override string SpicePrefix => "SRFF";
    public override string Description => "Clocked SR-Type Flip-Flop";

    public bool StateQ
    {
        get => CustomProperties.TryGetValue("StateQ", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["StateQ"] = value;
            NotifyModified(nameof(StateQ));
        }
    }

    public ClockTriggerType Trigger
    {
        get => CustomProperties.TryGetValue("Trigger", out var val) && val is ClockTriggerType t ? t : ClockTriggerType.RisingEdge;
        set
        {
            CustomProperties["Trigger"] = value;
            NotifyModified(nameof(Trigger));
        }
    }

    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public SRFlipFlopComponent()
    {
        Pins.Add(new ComponentPin("S", "S", -20, -14));
        Pins.Add(new ComponentPin("CLK", "CLK", -20, 0));
        Pins.Add(new ComponentPin("R", "R", -20, 14));
        Pins.Add(new ComponentPin("Q", "Q", 20, -10));
        Pins.Add(new ComponentPin("Q_bar", "Q̄", 20, 10));

        StateQ = false;
        Trigger = ClockTriggerType.RisingEdge;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
    }

    public SRFlipFlopComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}

/// <summary>
/// Edge-triggered Toggle (T-Type) Flip-Flop.
/// When T=1, state Q toggles on every active clock edge. Ideal for frequency division and counters.
/// </summary>
public class TFlipFlopComponent : ElectronicComponent
{
    public const string StableType = "electronics.tff";

    public override string StableTypeId => StableType;
    public override string ComponentType => "TFlipFlop";
    public override string SpicePrefix => "TFF";
    public override string Description => "Edge-triggered Toggle (T-Type) Flip-Flop";

    public bool StateQ
    {
        get => CustomProperties.TryGetValue("StateQ", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["StateQ"] = value;
            NotifyModified(nameof(StateQ));
        }
    }

    public ClockTriggerType Trigger
    {
        get => CustomProperties.TryGetValue("Trigger", out var val) && val is ClockTriggerType t ? t : ClockTriggerType.RisingEdge;
        set
        {
            CustomProperties["Trigger"] = value;
            NotifyModified(nameof(Trigger));
        }
    }

    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public TFlipFlopComponent()
    {
        Pins.Add(new ComponentPin("T", "T", -20, -10));
        Pins.Add(new ComponentPin("CLK", "CLK", -20, 10));
        Pins.Add(new ComponentPin("Q", "Q", 20, -10));
        Pins.Add(new ComponentPin("Q_bar", "Q̄", 20, 10));
        Pins.Add(new ComponentPin("reset", "CLR", 0, 20));

        StateQ = false;
        Trigger = ClockTriggerType.RisingEdge;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
    }

    public TFlipFlopComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
