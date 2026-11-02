using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Digital Multiplexer (MUX).
/// Selects one of N input data lines and forwards it to a single output line based on select lines.
/// </summary>
public class MultiplexerComponent : ElectronicComponent
{
    public const string StableType = "electronics.mux";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Multiplexer";
    public override string SpicePrefix => "MUX";
    public override string Description => "N-to-1 digital multiplexer data selector";

    public int SelectBits
    {
        get => ElectricalParameters.TryGetValue("SelectBits", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["SelectBits"] = Math.Clamp(value, 1, 5);
            RebuildPins();
            NotifyModified(nameof(SelectBits));
        }
    }

    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(BitWidth));
        }
    }

    public bool HasEnable
    {
        get => CustomProperties.TryGetValue("HasEnable", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["HasEnable"] = value;
            RebuildPins();
            NotifyModified(nameof(HasEnable));
        }
    }

    public MultiplexerComponent()
    {
        SelectBits = 1;
        BitWidth = 1;
        HasEnable = true;
    }

    public MultiplexerComponent(string name, int selectBits = 1) : this()
    {
        Name = name;
        Reference = name;
        SelectBits = selectBits;
    }

    private void RebuildPins()
    {
        int selectBits = SelectBits, width = BitWidth, inputCount = 1 << selectBits;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int input = 0; input < inputCount; input++)
            for (int bit = 0; bit < width; bit++)
            {
                string id = width == 1 ? $"in{input}" : $"in{input}_b{bit}";
                pins.Add(new ComponentPin(id, width == 1 ? input.ToString() : $"{input}:{bit}", -24, DigitalPinLayout.Position(input * width + bit, inputCount * width)));
            }
        if (selectBits == 1) pins.Add(new ComponentPin("sel", "S", 0, 22));
        else for (int bit = 0; bit < selectBits; bit++) pins.Add(new ComponentPin($"sel{bit}", $"S{bit}", 0, 22));
        if (HasEnable) pins.Add(new ComponentPin("en", "EN", -8, 22));
        for (int bit = 0; bit < width; bit++)
            pins.Add(new ComponentPin(width == 1 ? "out" : $"out_b{bit}", width == 1 ? "Y" : $"Y{bit}", 24, DigitalPinLayout.Position(bit, width)));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Digital Demultiplexer (DEMUX).
/// Takes a single input line and routes it to one of N output lines selected by select inputs.
/// </summary>
public class DemultiplexerComponent : ElectronicComponent
{
    public const string StableType = "electronics.demux";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Demultiplexer";
    public override string SpicePrefix => "DMX";
    public override string Description => "1-to-N digital demultiplexer data distributor";

    public int SelectBits
    {
        get => ElectricalParameters.TryGetValue("SelectBits", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["SelectBits"] = Math.Clamp(value, 1, 5);
            RebuildPins();
            NotifyModified(nameof(SelectBits));
        }
    }

    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(BitWidth));
        }
    }

    public DemultiplexerComponent()
    {
        SelectBits = 1;
        BitWidth = 1;
    }

    public DemultiplexerComponent(string name, int selectBits = 1) : this()
    {
        Name = name;
        Reference = name;
        SelectBits = selectBits;
    }

    private void RebuildPins()
    {
        int selectBits = SelectBits, width = BitWidth, outputCount = 1 << selectBits;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int bit = 0; bit < width; bit++) pins.Add(new ComponentPin(width == 1 ? "in" : $"in_b{bit}", width == 1 ? "IN" : $"I{bit}", -24, DigitalPinLayout.Position(bit, width)));
        if (selectBits == 1) pins.Add(new ComponentPin("sel", "S", 0, 22));
        else for (int bit = 0; bit < selectBits; bit++) pins.Add(new ComponentPin($"sel{bit}", $"S{bit}", 0, 22));
        pins.Add(new ComponentPin("en", "EN", 8, 22));
        for (int output = 0; output < outputCount; output++)
            for (int bit = 0; bit < width; bit++)
            {
                string id = width == 1 ? $"out{output}" : $"out{output}_b{bit}";
                pins.Add(new ComponentPin(id, width == 1 ? output.ToString() : $"{output}:{bit}", 24, DigitalPinLayout.Position(output * width + bit, outputCount * width)));
            }
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Binary Decoder (e.g. 2-to-4 or 3-to-8).
/// When enabled, activates the single output line corresponding to the binary value on select lines.
/// </summary>
public class DecoderComponent : ElectronicComponent
{
    public const string StableType = "electronics.decoder";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Decoder";
    public override string SpicePrefix => "DEC";
    public override string Description => "Binary decoder (n-to-2^n)";

    public int SelectBits
    {
        get => ElectricalParameters.TryGetValue("SelectBits", out var val) && val >= 1 ? (int)val : 2;
        set
        {
            ElectricalParameters["SelectBits"] = Math.Clamp(value, 1, 4);
            RebuildPins();
            NotifyModified(nameof(SelectBits));
        }
    }

    public bool HasEnable
    {
        get => CustomProperties.TryGetValue("HasEnable", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["HasEnable"] = value;
            RebuildPins();
            NotifyModified(nameof(HasEnable));
        }
    }

    public DecoderComponent()
    {
        SelectBits = 2;
        HasEnable = true;
    }

    public DecoderComponent(string name, int selectBits = 2) : this()
    {
        Name = name;
        Reference = name;
        SelectBits = selectBits;
    }

    private void RebuildPins()
    {
        int bits = SelectBits, outputs = 1 << bits;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        if (bits == 1) pins.Add(new ComponentPin("sel", "SEL", -24, 0));
        else for (int bit = 0; bit < bits; bit++) pins.Add(new ComponentPin($"sel{bit}", $"S{bit}", -24, DigitalPinLayout.Position(bit, bits)));
        if (HasEnable) pins.Add(new ComponentPin("en", "EN", 0, 24));
        for (int i = 0; i < outputs; i++) pins.Add(new ComponentPin($"out{i}", i.ToString(), 24, DigitalPinLayout.Position(i, outputs)));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Priority Encoder.
/// Encodes the index of the highest-priority active input into binary code.
/// Provides a group select (GS / AnyActive) output flag.
/// </summary>
public class PriorityEncoderComponent : ElectronicComponent
{
    public const string StableType = "electronics.priorityencoder";

    public override string StableTypeId => StableType;
    public override string ComponentType => "PriorityEncoder";
    public override string SpicePrefix => "ENC";
    public override string Description => "Priority encoder (highest active input to binary)";

    public int SelectBits
    {
        get => ElectricalParameters.TryGetValue("SelectBits", out var val) && val >= 1 ? (int)val : 2;
        set
        {
            ElectricalParameters["SelectBits"] = Math.Clamp(value, 1, 4);
            RebuildPins();
            NotifyModified(nameof(SelectBits));
        }
    }

    public PriorityEncoderComponent()
    {
        SelectBits = 2;
    }

    public PriorityEncoderComponent(string name, int selectBits = 2) : this()
    {
        Name = name;
        Reference = name;
        SelectBits = selectBits;
    }

    private void RebuildPins()
    {
        int bits = SelectBits, inputs = 1 << bits;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int i = 0; i < inputs; i++) pins.Add(new ComponentPin($"in{i}", i.ToString(), -24, DigitalPinLayout.Position(i, inputs)));
        pins.Add(new ComponentPin("en", "EN", 0, 24));
        for (int i = 0; i < bits; i++) pins.Add(new ComponentPin($"out{i}", $"O{i}", 24, DigitalPinLayout.Position(i, bits)));
        pins.Add(new ComponentPin("any_active", "GS", 24, 20));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// Bit Selector Component.
/// Extracts a specified bit or sub-bus from an input bus based on a select input.
/// </summary>
public class BitSelectorComponent : ElectronicComponent
{
    public const string StableType = "electronics.bitselector";

    public override string StableTypeId => StableType;
    public override string ComponentType => "BitSelector";
    public override string SpicePrefix => "BSEL";
    public override string Description => "Bit Selector (selects specific bit from a bus)";

    public int DataBusWidth
    {
        get => ElectricalParameters.TryGetValue("DataBusWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["DataBusWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(DataBusWidth));
        }
    }

    public int SelectWidth
    {
        get => ElectricalParameters.TryGetValue("SelectWidth", out var val) && val >= 1 ? (int)val : 3;
        set
        {
            ElectricalParameters["SelectWidth"] = Math.Clamp(value, 1, 5);
            RebuildPins();
            NotifyModified(nameof(SelectWidth));
        }
    }

    public BitSelectorComponent()
    {
        DataBusWidth = 8;
        SelectWidth = 3;
    }

    public BitSelectorComponent(string name, int dataWidth = 8, int selWidth = 3) : this()
    {
        Name = name;
        Reference = name;
        DataBusWidth = dataWidth;
        SelectWidth = selWidth;
    }

    private void RebuildPins()
    {
        int dataWidth = DataBusWidth, selectWidth = SelectWidth;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int i = 0; i < dataWidth; i++) pins.Add(new ComponentPin($"data{i}", $"D{i}", -24, DigitalPinLayout.Position(i, dataWidth)));
        for (int i = 0; i < selectWidth; i++) pins.Add(new ComponentPin($"sel{i}", $"S{i}", -10, DigitalPinLayout.Position(i, selectWidth)));
        pins.Add(new ComponentPin("out", "OUT", 24, 0));
        DigitalPinLayout.Replace(this, pins);
    }
}
