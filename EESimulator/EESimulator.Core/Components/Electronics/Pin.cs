using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum PinDirection
{
    Input,
    Output
}

public enum PinBehavior
{
    Tristate,
    NonTristate,
    PullUp,
    PullDown
}

public enum DisplayRadix
{
    Binary = 2,
    Octal = 8,
    SignedDecimal = 10,
    UnsignedDecimal = 11,
    Hexadecimal = 16
}

/// <summary>
/// Named I/O Port Terminal (Pin).
/// Acts as an external circuit boundary terminal. Can be configured as Input (injects a logic/voltage value)
/// or Output (probes and monitors an internal node), supporting single-bit or multi-bit buses.
/// </summary>
public class PinComponent : ElectronicComponent
{
    public const string StableType = "electronics.pin";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Pin";
    public override string SpicePrefix => "PIN";
    public override string Description => "Named I/O Port Terminal (Input / Output)";

    public PinDirection Direction
    {
        get
        {
            if (CustomProperties.TryGetValue("Direction", out var val) && val is PinDirection dir)
                return dir;
            if (CustomProperties.TryGetValue("Direction", out var strVal) && Enum.TryParse<PinDirection>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return PinDirection.Input;
        }
        set
        {
            CustomProperties["Direction"] = value;
            NotifyModified(nameof(Direction));
        }
    }

    /// <summary>Logisim Pin Behavior: Tristate, NonTristate, PullUp, PullDown (ATTR_BEHAVIOR).</summary>
    public PinBehavior Behavior
    {
        get
        {
            if (CustomProperties.TryGetValue("Behavior", out var val) && val is PinBehavior b)
                return b;
            if (CustomProperties.TryGetValue("Behavior", out var strVal) && Enum.TryParse<PinBehavior>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return PinBehavior.Tristate;
        }
        set
        {
            CustomProperties["Behavior"] = value;
            NotifyModified(nameof(Behavior));
        }
    }

    /// <summary>Logisim Initial / Reset Value (ATTR_INITIAL).</summary>
    public long InitialValue
    {
        get => CustomProperties.TryGetValue("InitialValue", out var val) && long.TryParse(val?.ToString(), out var lVal) ? lVal : 0;
        set
        {
            CustomProperties["InitialValue"] = value;
            NotifyModified(nameof(InitialValue));
        }
    }

    /// <summary>Logisim Display Radix (RadixOption.ATTRIBUTE).</summary>
    public DisplayRadix Radix
    {
        get
        {
            if (CustomProperties.TryGetValue("Radix", out var val) && val is DisplayRadix r)
                return r;
            if (CustomProperties.TryGetValue("Radix", out var strVal) && Enum.TryParse<DisplayRadix>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return DisplayRadix.Binary;
        }
        set
        {
            CustomProperties["Radix"] = value;
            NotifyModified(nameof(Radix));
        }
    }

    /// <summary>Bit width of this pin / bus (1 to 32 bits, default 1).</summary>
    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            NotifyModified(nameof(BitWidth));
        }
    }

    /// <summary>Logic state: 0 or 1 for 1-bit pins, or integer value for multi-bit buses.</summary>
    public long Value
    {
        get
        {
            if (CustomProperties.TryGetValue("Value", out var val))
            {
                if (val is long lVal) return lVal;
                if (val is int iVal) return iVal;
                if (val is bool bVal) return bVal ? 1 : 0;
            }
            return ElectricalParameters.TryGetValue("Value", out var pVal) ? (long)pVal : 0;
        }
        set
        {
            CustomProperties["Value"] = value;
            ElectricalParameters["Value"] = value;
            LiveVoltage = value > 0 ? HighVoltage : LowVoltage;
            NotifyModified(nameof(Value));
        }
    }

    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            if (Value > 0) LiveVoltage = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            if (Value == 0) LiveVoltage = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public PinComponent()
    {
        Pins.Add(new ComponentPin("pin", "IO", 20, 0));
        Direction = PinDirection.Input;
        BitWidth = 1;
        Value = 0;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
        ShowLiveProbe = true;
    }

    public PinComponent(string name, PinDirection direction = PinDirection.Input, int bitWidth = 1) : this()
    {
        Name = name;
        Reference = name;
        Direction = direction;
        BitWidth = bitWidth;
    }

    public void Toggle()
    {
        if (Direction == PinDirection.Input)
        {
            Value = Value == 0 ? 1 : 0;
        }
    }
}
