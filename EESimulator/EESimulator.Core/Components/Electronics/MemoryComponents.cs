using System;
using System.Collections.Generic;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// N-bit Parallel Load Register.
/// Stores N bits of digital state. On active clock edge when enable (EN) is HIGH, Q loads D.
/// </summary>
public class RegisterComponent : ElectronicComponent
{
    public const string StableType = "electronics.register";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Register";
    public override string SpicePrefix => "REG";
    public override string Description => "N-bit parallel-load register";

    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 64);
            RebuildPins();
            NotifyModified(nameof(BitWidth));
        }
    }

    public long Value
    {
        get => CustomProperties.TryGetValue("Value", out var val) && val is long l ? l : (CustomProperties.TryGetValue("Value", out var o) && long.TryParse(o?.ToString(), out var parsed) ? parsed : 0);
        set
        {
            CustomProperties["Value"] = value;
            NotifyModified(nameof(Value));
        }
    }

    public RegisterComponent()
    {
        BitWidth = 8;
        Value = 0;
    }

    public RegisterComponent(string name, int bitWidth = 8) : this()
    {
        Name = name;
        Reference = name;
        BitWidth = bitWidth;
    }

    private void RebuildPins()
    {
        int width = BitWidth;
        var pins = new List<ComponentPin>();
        for (int bit = 0; bit < width; bit++) pins.Add(new ComponentPin($"D{bit}", $"D{bit}", -24, DigitalPinLayout.Position(bit, width)));
        pins.Add(new ComponentPin("CLK", "CLK", -8, 24));
        pins.Add(new ComponentPin("en", "EN", 8, 24));
        pins.Add(new ComponentPin("clr", "CLR", 0, 24));
        for (int bit = 0; bit < width; bit++) pins.Add(new ComponentPin($"Q{bit}", $"Q{bit}", 24, DigitalPinLayout.Position(bit, width)));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Up/Down Binary Counter with Enable, Clear, and Carry Out.
/// Increments or decrements its stored value on clock edges.
/// </summary>
public class CounterComponent : ElectronicComponent
{
    public const string StableType = "electronics.counter";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Counter";
    public override string SpicePrefix => "CTR";
    public override string Description => "Up/Down binary counter with load and carry";

    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(BitWidth));
        }
    }

    public long MaxValue
    {
        get => ElectricalParameters.TryGetValue("MaxValue", out var val) && val > 0 ? (long)val : ((1L << BitWidth) - 1);
        set
        {
            ElectricalParameters["MaxValue"] = value;
            NotifyModified(nameof(MaxValue));
        }
    }

    public long Value
    {
        get => CustomProperties.TryGetValue("Value", out var val) && val is long l ? l : 0;
        set
        {
            CustomProperties["Value"] = value;
            NotifyModified(nameof(Value));
        }
    }

    public bool CarryOut
    {
        get => CustomProperties.TryGetValue("CarryOut", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["CarryOut"] = value;
            NotifyModified(nameof(CarryOut));
        }
    }

    public CounterComponent()
    {
        BitWidth = 8;
        Value = 0;
    }

    public CounterComponent(string name, int bitWidth = 8) : this()
    {
        Name = name;
        Reference = name;
        BitWidth = bitWidth;
    }

    private void RebuildPins()
    {
        int width = BitWidth;
        var pins = new List<ComponentPin>
        {
            new("CLK", "CLK", -24, -18), new("en", "EN", -24, -6),
            new("up_down", "DIR", -24, 6), new("clr", "CLR", -24, 18)
        };
        for (int bit = 0; bit < width; bit++) pins.Add(new ComponentPin($"Q{bit}", $"Q{bit}", 24, DigitalPinLayout.Position(bit, width)));
        pins.Add(new ComponentPin("carry", "CY", 12, 24));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Shift Register (Serial-In, Serial-Out / Parallel-Out).
/// On clock edges, shifts stored bits and accepts new bit at serial input (SI).
/// </summary>
public class ShiftRegisterComponent : ElectronicComponent
{
    public const string StableType = "electronics.shiftregister";

    public override string StableTypeId => StableType;
    public override string ComponentType => "ShiftRegister";
    public override string SpicePrefix => "SHR";
    public override string Description => "Serial-in / parallel-out shift register";

    public int Length
    {
        get => ElectricalParameters.TryGetValue("Length", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["Length"] = Math.Clamp(value, 1, 64);
            RebuildPins();
            NotifyModified(nameof(Length));
        }
    }

    public long Value
    {
        get => CustomProperties.TryGetValue("Value", out var val) && val is long l ? l : 0;
        set
        {
            CustomProperties["Value"] = value;
            NotifyModified(nameof(Value));
        }
    }

    public ShiftRegisterComponent()
    {
        Length = 8;
        Value = 0;
    }

    public ShiftRegisterComponent(string name, int length = 8) : this()
    {
        Name = name;
        Reference = name;
        Length = length;
    }

    private void RebuildPins()
    {
        int width = Length;
        var pins = new List<ComponentPin>
        {
            new("sin", "SI", -24, -18), new("CLK", "CLK", -24, -6),
            new("en", "EN", -24, 6), new("clr", "CLR", -24, 18)
        };
        for (int bit = 0; bit < width; bit++) pins.Add(new ComponentPin($"Q{bit}", $"Q{bit}", 24, DigitalPinLayout.Position(bit, width)));
        pins.Add(new ComponentPin("sout", "SO", 8, 24));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Random-Access Read/Write Memory (RAM).
/// Stores an addressable array of data words with clock-synchronized write enable.
/// </summary>
public class RAMComponent : ElectronicComponent
{
    public const string StableType = "electronics.ram";

    public override string StableTypeId => StableType;
    public override string ComponentType => "RAM";
    public override string SpicePrefix => "RAM";
    public override string Description => "Random-access read/write memory";

    public int AddressBitWidth
    {
        get => ElectricalParameters.TryGetValue("AddressBitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["AddressBitWidth"] = Math.Clamp(value, 1, 16);
            RebuildPins();
            NotifyModified(nameof(AddressBitWidth));
        }
    }

    public int DataBitWidth
    {
        get => ElectricalParameters.TryGetValue("DataBitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["DataBitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(DataBitWidth));
        }
    }

    public Dictionary<int, long> MemoryData { get; } = new();

    public RAMComponent()
    {
        AddressBitWidth = 8;
        DataBitWidth = 8;
    }

    public RAMComponent(string name, int addrWidth = 8, int dataWidth = 8) : this()
    {
        Name = name;
        Reference = name;
        AddressBitWidth = addrWidth;
        DataBitWidth = dataWidth;
    }

    private void RebuildPins()
    {
        var pins = new List<ComponentPin>();
        for (int bit = 0; bit < AddressBitWidth; bit++) pins.Add(new ComponentPin($"addr{bit}", $"A{bit}", -24, DigitalPinLayout.Position(bit, AddressBitWidth)));
        for (int bit = 0; bit < DataBitWidth; bit++) pins.Add(new ComponentPin($"din{bit}", $"D{bit}", -8, DigitalPinLayout.Position(bit, DataBitWidth)));
        pins.Add(new ComponentPin("clk", "CLK", 0, 24));
        pins.Add(new ComponentPin("we", "WE", -12, 24));
        for (int bit = 0; bit < DataBitWidth; bit++) pins.Add(new ComponentPin($"dout{bit}", $"Q{bit}", 24, DigitalPinLayout.Position(bit, DataBitWidth)));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Read-Only Memory (ROM) with preloaded contents.
/// Outputs stored memory contents at the specified address bus input when selected.
/// </summary>
public class ROMComponent : ElectronicComponent
{
    public const string StableType = "electronics.rom";

    public override string StableTypeId => StableType;
    public override string ComponentType => "ROM";
    public override string SpicePrefix => "ROM";
    public override string Description => "Read-only memory with preloaded contents";

    public int AddressBitWidth
    {
        get => ElectricalParameters.TryGetValue("AddressBitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["AddressBitWidth"] = Math.Clamp(value, 1, 16);
            RebuildPins();
            NotifyModified(nameof(AddressBitWidth));
        }
    }

    public int DataBitWidth
    {
        get => ElectricalParameters.TryGetValue("DataBitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["DataBitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(DataBitWidth));
        }
    }

    public string ContentsHex
    {
        get => CustomProperties.TryGetValue("ContentsHex", out var val) ? val?.ToString() ?? string.Empty : string.Empty;
        set
        {
            CustomProperties["ContentsHex"] = value;
            NotifyModified(nameof(ContentsHex));
        }
    }

    public Dictionary<int, long> MemoryData { get; } = new();

    public ROMComponent()
    {
        AddressBitWidth = 8;
        DataBitWidth = 8;
    }

    public ROMComponent(string name, int addrWidth = 8, int dataWidth = 8, string hexData = "") : this()
    {
        Name = name;
        Reference = name;
        AddressBitWidth = addrWidth;
        DataBitWidth = dataWidth;
        ContentsHex = hexData;
    }

    private void RebuildPins()
    {
        var pins = new List<ComponentPin>();
        for (int bit = 0; bit < AddressBitWidth; bit++) pins.Add(new ComponentPin($"addr{bit}", $"A{bit}", -24, DigitalPinLayout.Position(bit, AddressBitWidth)));
        pins.Add(new ComponentPin("sel", "CS", 0, 24));
        for (int bit = 0; bit < DataBitWidth; bit++) pins.Add(new ComponentPin($"dout{bit}", $"Q{bit}", 24, DigitalPinLayout.Position(bit, DataBitWidth)));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Pseudo-random number generator (PRNG / LFSR).
/// Generates next pseudo-random digital sequence on active clock edges.
/// </summary>
public class RandomComponent : ElectronicComponent
{
    public const string StableType = "electronics.random";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Random";
    public override string SpicePrefix => "RND";
    public override string Description => "Pseudo-random number generator (PRNG)";

    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(BitWidth));
        }
    }

    public int Seed
    {
        get => ElectricalParameters.TryGetValue("Seed", out var val) ? (int)val : 1;
        set
        {
            ElectricalParameters["Seed"] = value;
            NotifyModified(nameof(Seed));
        }
    }

    public long Value
    {
        get => CustomProperties.TryGetValue("Value", out var val) && val is long l ? l : 0;
        set
        {
            CustomProperties["Value"] = value;
            NotifyModified(nameof(Value));
        }
    }

    public RandomComponent()
    {
        BitWidth = 8;
        Seed = 1;
        Value = 0;
    }

    public RandomComponent(string name, int bitWidth = 8, int seed = 1) : this()
    {
        Name = name;
        Reference = name;
        BitWidth = bitWidth;
        Seed = seed;
    }

    private void RebuildPins()
    {
        var pins = new List<ComponentPin>
        {
            new("CLK", "CLK", -24, -12), new("rst", "RST", -24, 0), new("en", "EN", -24, 12)
        };
        for (int bit = 0; bit < BitWidth; bit++) pins.Add(new ComponentPin($"Q{bit}", $"Q{bit}", 24, DigitalPinLayout.Position(bit, BitWidth)));
        DigitalPinLayout.Replace(this, pins);
    }
}
