using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;

namespace EESimulator.Engine.Simulation;

/// <summary>Logic-level evaluator used alongside SpiceSharp for digital components that have no analog primitive.</summary>
public static class DigitalLogicSimulator
{
    private const double LogicThreshold = 2.5;

    public static void Advance(CircuitNode circuit, double elapsedSeconds)
    {
        if (circuit == null) return;
        var context = new Context(circuit);
        if (elapsedSeconds > 0)
        {
            foreach (var clock in context.Components.OfType<ClockComponent>())
            {
                var phase = Number(clock, "_simPhase", 0) + Math.Max(0.01, clock.Frequency) * elapsedSeconds;
                var wholeCycles = (long)Math.Floor(phase);
                var duty = Math.Clamp(clock.DutyCycle, 0.01, 0.99);
                var falls = CountBoundaryCrossings(Number(clock, "_simPhase", 0), phase, duty);
                SetNumber(clock, "_simRisingEdges", Number(clock, "_simRisingEdges", 0) + wholeCycles);
                SetNumber(clock, "_simFallingEdges", Number(clock, "_simFallingEdges", 0) + falls);
                SetNumber(clock, "_simPhase", phase - wholeCycles);
                clock.State = phase - wholeCycles < duty;
            }
        }
        context.AdvanceSequential();
        context.EvaluateCombinational();
    }

    public static void EvaluateCombinational(CircuitNode circuit)
    {
        if (circuit == null) return;
        new Context(circuit).EvaluateCombinational();
    }

    public static bool IsSpiceDrivenDigital(ComponentNode component) => component is
        BufferComponent or ControlledBufferComponent or EvenParityGateComponent or OddParityGateComponent or
        PLAComponent or MultiplexerComponent or DemultiplexerComponent or DecoderComponent or
        PriorityEncoderComponent or BitSelectorComponent or DFlipFlopComponent or JKFlipFlopComponent or
        SRFlipFlopComponent or TFlipFlopComponent or RegisterComponent or CounterComponent or
        ShiftRegisterComponent or RAMComponent or ROMComponent or RandomComponent or
        AndGateComponent or OrGateComponent or NotGateComponent or NandGateComponent or NorGateComponent or
        XorGateComponent or XnorGateComponent or LogicGateComponent;

    public static IEnumerable<(string PinId, double Voltage)> GetOutputVoltages(ComponentNode component)
    {
        foreach (var pin in component.Pins)
        {
            if (!IsOutputPin(component, pin.Id)) continue;
            if (component is ControlledBufferComponent && !Bool(component, "_triStateEnabled", true)) continue;
            double high = component switch
            {
                LogicInputComponent li => li.HighVoltage,
                ClockComponent clock => clock.HighVoltage,
                PinComponent pinComponent => pinComponent.HighVoltage,
                BufferComponent buffer => buffer.HighVoltage,
                ControlledBufferComponent buffer => buffer.HighVoltage,
                DFlipFlopComponent ff => ff.HighVoltage,
                JKFlipFlopComponent ff => ff.HighVoltage,
                SRFlipFlopComponent ff => ff.HighVoltage,
                TFlipFlopComponent ff => ff.HighVoltage,
                _ => 5.0
            };
            double low = component switch
            {
                LogicInputComponent li => li.LowVoltage,
                ClockComponent clock => clock.LowVoltage,
                PinComponent pinComponent => pinComponent.LowVoltage,
                BufferComponent buffer => buffer.LowVoltage,
                ControlledBufferComponent buffer => buffer.LowVoltage,
                DFlipFlopComponent ff => ff.LowVoltage,
                JKFlipFlopComponent ff => ff.LowVoltage,
                SRFlipFlopComponent ff => ff.LowVoltage,
                TFlipFlopComponent ff => ff.LowVoltage,
                _ => 0.0
            };
            yield return (pin.Id, OutputState(component, pin.Id) ? high : low);
        }
    }

    public static bool ReadOutputState(ComponentNode component, string pinId) => OutputState(component, pinId);

    private static bool OutputState(ComponentNode component, string pinId)
    {
        if (component.CustomProperties.TryGetValue($"Output:{pinId}", out var value))
        {
            if (value is bool boolean) return boolean;
            if (double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number)) return number > 0.5;
        }

        return component switch
        {
            LogicInputComponent li when pinId == "out" => li.State,
            ClockComponent clock when pinId == "out" => clock.State,
            ConstantComponent constant when pinId == "out" => constant.Value != 0,
            PowerComponent power when pinId == "vcc" => power.Voltage >= LogicThreshold,
            PinComponent pin when pinId == "pin" && pin.Direction == PinDirection.Input => pin.Value != 0,
            DFlipFlopComponent dff when pinId == "Q" => dff.StateQ,
            DFlipFlopComponent dff when pinId == "Q_bar" => !dff.StateQ,
            JKFlipFlopComponent jk when pinId == "Q" => jk.StateQ,
            JKFlipFlopComponent jk when pinId == "Q_bar" => !jk.StateQ,
            SRFlipFlopComponent sr when pinId == "Q" => sr.StateQ,
            SRFlipFlopComponent sr when pinId == "Q_bar" => !sr.StateQ,
            TFlipFlopComponent tff when pinId == "Q" => tff.StateQ,
            TFlipFlopComponent tff when pinId == "Q_bar" => !tff.StateQ,
            RegisterComponent register when pinId == "Q" => register.Value != 0,
            CounterComponent counter when pinId == "Q" => counter.Value != 0,
            CounterComponent counter when pinId == "carry" => counter.CarryOut,
            ShiftRegisterComponent shift when pinId == "sout" => (shift.Value & 1) != 0,
            RandomComponent random when pinId == "out" => random.Value != 0,
            RAMComponent ram when pinId == "dout" => ram.MemoryData.Values.FirstOrDefault() != 0,
            ROMComponent rom when pinId == "dout" => rom.MemoryData.Values.FirstOrDefault() != 0,
            _ => component.LiveVoltage >= LogicThreshold
        };
    }

    private static bool IsOutputPin(ComponentNode component, string pinId) => component switch
    {
        LogicInputComponent => pinId == "out",
        ClockComponent => pinId == "out",
        ConstantComponent => pinId == "out",
        PowerComponent => pinId == "vcc",
        PinComponent pin => pinId == "pin" && pin.Direction == PinDirection.Input,
        BufferComponent or ControlledBufferComponent or EvenParityGateComponent or OddParityGateComponent or MultiplexerComponent or BitSelectorComponent => pinId == "out" || pinId.StartsWith("out_", StringComparison.Ordinal),
        PLAComponent or DemultiplexerComponent or DecoderComponent => pinId.StartsWith("out", StringComparison.Ordinal),
        PriorityEncoderComponent => pinId.StartsWith("out", StringComparison.Ordinal) || pinId == "any_active",
        DFlipFlopComponent or JKFlipFlopComponent or SRFlipFlopComponent or TFlipFlopComponent => pinId is "Q" or "Q_bar",
        RegisterComponent => pinId.StartsWith("Q", StringComparison.Ordinal),
        CounterComponent => pinId.StartsWith("Q", StringComparison.Ordinal) || pinId == "carry",
        ShiftRegisterComponent => pinId == "sout" || pinId.StartsWith("Q", StringComparison.Ordinal),
        RAMComponent or ROMComponent => pinId.StartsWith("dout", StringComparison.Ordinal),
        RandomComponent => pinId.StartsWith("Q", StringComparison.Ordinal),
        AndGateComponent or OrGateComponent or NotGateComponent or NandGateComponent or NorGateComponent or XorGateComponent or XnorGateComponent => pinId == "out",
        _ => false
    };

    private sealed class Context
    {
        public List<ComponentNode> Components { get; }
        private readonly CircuitTopology _topology;
        private readonly Dictionary<Guid, ComponentNode> _byId;

        public Context(CircuitNode circuit)
        {
            Components = circuit.Components.ToList();
            _topology = NetlistBuilder.BuildTopology(circuit);
            _byId = Components.ToDictionary(component => component.Id);
        }

        public void AdvanceSequential()
        {
            foreach (var component in Components)
            {
                string clockPin = component switch
                {
                    DFlipFlopComponent or JKFlipFlopComponent or SRFlipFlopComponent or TFlipFlopComponent or RegisterComponent or CounterComponent or ShiftRegisterComponent or RandomComponent => "CLK",
                    RAMComponent => "clk",
                    _ => string.Empty
                };
                if (clockPin.Length == 0) continue;
                ClockTriggerType trigger = component switch
                {
                    DFlipFlopComponent dff => dff.Trigger,
                    JKFlipFlopComponent jk => jk.Trigger,
                    SRFlipFlopComponent sr => sr.Trigger,
                    TFlipFlopComponent tff => tff.Trigger,
                    _ => ClockTriggerType.RisingEdge
                };
                int edges = ClockEdges(component, clockPin, trigger);

                switch (component)
                {
                    case DFlipFlopComponent dff:
                        if (Read(dff, "reset")) dff.StateQ = false;
                        else if (Read(dff, "set")) dff.StateQ = true;
                        else for (int i = 0; i < edges; i++) dff.StateQ = Read(dff, "D");
                        Set(dff, "Q", dff.StateQ, dff.HighVoltage, dff.LowVoltage);
                        Set(dff, "Q_bar", !dff.StateQ, dff.HighVoltage, dff.LowVoltage);
                        break;
                    case JKFlipFlopComponent jk:
                        if (Read(jk, "reset")) jk.StateQ = false;
                        else for (int i = 0; i < edges; i++)
                        {
                            bool j = Read(jk, "J"), k = Read(jk, "K");
                            if (j && k) jk.StateQ = !jk.StateQ;
                            else if (j) jk.StateQ = true;
                            else if (k) jk.StateQ = false;
                        }
                        Set(jk, "Q", jk.StateQ, jk.HighVoltage, jk.LowVoltage);
                        Set(jk, "Q_bar", !jk.StateQ, jk.HighVoltage, jk.LowVoltage);
                        break;
                    case SRFlipFlopComponent sr:
                        for (int i = 0; i < edges; i++)
                        {
                            bool set = Read(sr, "S"), reset = Read(sr, "R");
                            if (set && !reset) sr.StateQ = true;
                            else if (reset && !set) sr.StateQ = false;
                        }
                        Set(sr, "Q", sr.StateQ, sr.HighVoltage, sr.LowVoltage);
                        Set(sr, "Q_bar", !sr.StateQ, sr.HighVoltage, sr.LowVoltage);
                        break;
                    case TFlipFlopComponent tff:
                        if (Read(tff, "reset")) tff.StateQ = false;
                        else for (int i = 0; i < edges; i++) if (Read(tff, "T")) tff.StateQ = !tff.StateQ;
                        Set(tff, "Q", tff.StateQ, tff.HighVoltage, tff.LowVoltage);
                        Set(tff, "Q_bar", !tff.StateQ, tff.HighVoltage, tff.LowVoltage);
                        break;
                    case RegisterComponent register:
                        if (Read(register, "clr")) register.Value = 0;
                        else if (Read(register, "en") || !HasConnected(register, "en"))
                            for (int i = 0; i < edges; i++) register.Value = ReadBus(register, "D", register.BitWidth);
                        for (int bit = 0; bit < register.BitWidth; bit++) Set(register, $"Q{bit}", ((register.Value >> bit) & 1) != 0, 5, 0);
                        break;
                    case CounterComponent counter:
                        if (Read(counter, "clr")) counter.Value = 0;
                        else if (Read(counter, "en"))
                        {
                            long max = Math.Max(1, counter.MaxValue);
                            for (int i = 0; i < edges; i++) counter.Value = Read(counter, "up_down") ? (counter.Value + 1) % (max + 1) : (counter.Value <= 0 ? max : counter.Value - 1);
                            counter.CarryOut = counter.Value == 0;
                        }
                        for (int bit = 0; bit < counter.BitWidth; bit++) Set(counter, $"Q{bit}", ((counter.Value >> bit) & 1) != 0, 5, 0);
                        Set(counter, "carry", counter.CarryOut, 5, 0);
                        break;
                    case ShiftRegisterComponent shift:
                        if (Read(shift, "clr")) shift.Value = 0;
                        else if (Read(shift, "en") || !HasConnected(shift, "en"))
                            for (int i = 0; i < edges; i++) shift.Value = ((shift.Value << 1) | (Read(shift, "sin") ? 1L : 0L)) & Mask(shift.Length);
                        for (int bit = 0; bit < shift.Length; bit++) Set(shift, $"Q{bit}", ((shift.Value >> bit) & 1) != 0, 5, 0);
                        Set(shift, "sout", (shift.Value & 1) != 0, 5, 0);
                        break;
                    case RandomComponent random:
                        if (Read(random, "rst")) random.Value = Math.Max(1, random.Seed);
                        else if (Read(random, "en") || !HasConnected(random, "en"))
                            for (int i = 0; i < edges; i++)
                            {
                                long value = random.Value == 0 ? Math.Max(1, random.Seed) : random.Value;
                                long feedback = ((value >> 7) ^ (value >> 5) ^ (value >> 4) ^ (value >> 3)) & 1;
                                random.Value = ((value << 1) | feedback) & Mask(random.BitWidth);
                            }
                        for (int bit = 0; bit < random.BitWidth; bit++) Set(random, $"Q{bit}", ((random.Value >> bit) & 1) != 0, 5, 0);
                        break;
                    case RAMComponent ram:
                        int address = (int)ReadBus(ram, "addr", ram.AddressBitWidth);
                        if (Read(ram, "we") && edges > 0) ram.MemoryData[address] = ReadBus(ram, "din", ram.DataBitWidth);
                        long word = ram.MemoryData.TryGetValue(address, out var storedWord) ? storedWord : 0;
                        for (int bit = 0; bit < ram.DataBitWidth; bit++) Set(ram, $"dout{bit}", ((word >> bit) & 1) != 0, 5, 0);
                        break;
                }
            }
        }

        public void EvaluateCombinational()
        {
            // Fixed-point iteration settles short cascades of combinational gates.
            for (int pass = 0; pass < Math.Clamp(Components.Count, 1, 24); pass++)
            {
                foreach (var component in Components)
                {
                    switch (component)
                    {
                        case BufferComponent buffer:
                            Set(buffer, "out", Read(buffer, "in"), buffer.HighVoltage, buffer.LowVoltage);
                            break;
                        case ControlledBufferComponent tri:
                            bool triEnabled = Read(tri, "ctrl") != tri.ControlActiveLow;
                            SetBool(tri, "_triStateEnabled", triEnabled);
                            if (triEnabled)
                                Set(tri, "out", Read(tri, "in"), tri.HighVoltage, tri.LowVoltage);
                            break;
                        case EvenParityGateComponent even:
                            Set(even, "out", CountInputs(even, even.NumberOfInputs) % 2 == 0, 5, 0);
                            break;
                        case OddParityGateComponent odd:
                            Set(odd, "out", CountInputs(odd, odd.NumberOfInputs) % 2 != 0, 5, 0);
                            break;
                        case PLAComponent pla:
                            EvaluatePla(pla);
                            break;
                        case MultiplexerComponent mux:
                            int muxIndex = (int)ReadBus(mux, "sel", mux.SelectBits);
                            long muxWord = ReadBus(mux, $"in{muxIndex}", mux.BitWidth);
                            bool muxEnabled = !mux.HasEnable || !HasConnected(mux, "en") || Read(mux, "en");
                            for (int bit = 0; bit < mux.BitWidth; bit++) Set(mux, mux.BitWidth == 1 ? "out" : $"out_b{bit}", muxEnabled && ((muxWord >> bit) & 1) != 0, 5, 0);
                            break;
                        case DemultiplexerComponent demux:
                            int selected = (int)ReadBus(demux, "sel", demux.SelectBits);
                            long data = ReadBus(demux, "in", demux.BitWidth);
                            bool demuxEnabled = !HasConnected(demux, "en") || Read(demux, "en");
                            for (int output = 0; output < (1 << demux.SelectBits); output++)
                                for (int bit = 0; bit < demux.BitWidth; bit++)
                                {
                                    string outputPin = demux.BitWidth == 1 ? $"out{output}" : $"out{output}_b{bit}";
                                    Set(demux, outputPin, demuxEnabled && selected == output && ((data >> bit) & 1) != 0, 5, 0);
                                }
                            break;
                        case DecoderComponent decoder:
                            int decoded = (int)ReadBus(decoder, "sel", decoder.SelectBits);
                            bool enabled = !decoder.HasEnable || !HasConnected(decoder, "en") || Read(decoder, "en");
                            for (int i = 0; i < (1 << decoder.SelectBits); i++) Set(decoder, $"out{i}", enabled && decoded == i, 5, 0);
                            break;
                        case PriorityEncoderComponent encoder:
                            int active = -1;
                            for (int i = 0; i < (1 << encoder.SelectBits); i++) if (Read(encoder, $"in{i}")) active = i;
                            SetNumber(encoder, "EncodedValue", Math.Max(active, 0));
                            for (int bit = 0; bit < encoder.SelectBits; bit++) Set(encoder, $"out{bit}", active >= 0 && ((active >> bit) & 1) != 0, 5, 0);
                            Set(encoder, "any_active", active >= 0, 5, 0);
                            break;
                        case BitSelectorComponent selector:
                            int bitIndex = (int)ReadBus(selector, "sel", selector.SelectWidth);
                            long dataWord = ReadBus(selector, "data", selector.DataBusWidth);
                            Set(selector, "out", bitIndex < selector.DataBusWidth && ((dataWord >> bitIndex) & 1) != 0, 5, 0);
                            break;
                        case LogicGateComponent logicGate:
                            EvaluateLogicGate(logicGate);
                            break;
                        case ElectronicComponent namedGate when namedGate.ComponentType is "AND" or "OR" or "NOT" or "NAND" or "NOR" or "XOR" or "XNOR":
                            EvaluateNamedGate(namedGate);
                            break;
                    }
                }
            }
        }

        private int ClockEdges(ComponentNode component, string clockPin, ClockTriggerType trigger)
        {
            if (TryGetNet(component, clockPin, out var net))
            {
                foreach (var pin in net.ConnectedPins)
                {
                    if (!_byId.TryGetValue(pin.ComponentId, out var source) || source is not ClockComponent clock) continue;
                    if (trigger == ClockTriggerType.HighLevel) return clock.State ? 1 : 0;
                    if (trigger == ClockTriggerType.LowLevel) return clock.State ? 0 : 1;
                    string key = $"_seenClockEdges:{clock.Id}:{trigger}";
                    long previous = (long)Number(component, key, 0);
                    string edgeKey = trigger == ClockTriggerType.FallingEdge ? "_simFallingEdges" : "_simRisingEdges";
                    long current = (long)Number(clock, edgeKey, 0);
                    SetNumber(component, key, current);
                    return (int)Math.Clamp(current - previous, 0, 1024);
                }
            }
            bool currentLevel = Read(component, clockPin);
            string previousKey = $"_previousClock:{clockPin}";
            bool previousLevel = Bool(component, previousKey, false);
            SetBool(component, previousKey, currentLevel);
            return trigger switch
            {
                ClockTriggerType.FallingEdge => !currentLevel && previousLevel ? 1 : 0,
                ClockTriggerType.HighLevel => currentLevel ? 1 : 0,
                ClockTriggerType.LowLevel => currentLevel ? 0 : 1,
                _ => currentLevel && !previousLevel ? 1 : 0
            };
        }

        private bool Read(ComponentNode component, string pinId)
        {
            if (!TryGetNet(component, pinId, out var net)) return false;
            foreach (var pin in net.ConnectedPins)
            {
                if (pin.ComponentId == component.Id || !_byId.TryGetValue(pin.ComponentId, out var source)) continue;
                if (IsOutputPin(source, pin.PinId) && OutputState(source, pin.PinId)) return true;
            }
            return false;
        }

        private long ReadBus(ComponentNode component, string prefix, int width)
        {
            long value = 0;
            for (int bit = 0; bit < Math.Clamp(width, 1, 62); bit++)
            {
                string pinId = component.GetPin(prefix) != null && width == 1
                    ? prefix
                    : component.GetPin($"{prefix}{bit}") != null
                        ? $"{prefix}{bit}"
                        : component.GetPin($"{prefix}_b{bit}") != null
                            ? $"{prefix}_b{bit}"
                            : (width == 1 ? prefix : $"{prefix}{bit}");
                if (Read(component, pinId)) value |= 1L << bit;
            }
            return value;
        }

        private bool HasConnected(ComponentNode component, string pinId) =>
            TryGetNet(component, pinId, out var net) && net.ConnectedPins.Any(pin => pin.ComponentId != component.Id);

        private bool TryGetNet(ComponentNode component, string pinId, out NetNode net)
        {
            var pin = component.GetPin(pinId);
            if (pin != null && _topology.PinToNetMap.TryGetValue(new PinReference(component.Id, pin.Id), out net!)) return true;
            net = null!;
            return false;
        }

        private int CountInputs(ComponentNode component, int inputCount)
        {
            int count = 0;
            for (int i = 0; i < inputCount; i++)
            {
                string pinId = component.GetPin($"in{i + 1}") != null ? $"in{i + 1}" : $"in{i}";
                if (Read(component, pinId)) count++;
            }
            return count;
        }

        private void EvaluateLogicGate(LogicGateComponent gate)
        {
            bool a = Read(gate, "in1"), b = Read(gate, "in2");
            bool result = gate.GateType switch
            {
                LogicGateType.AND => a && b,
                LogicGateType.OR => a || b,
                LogicGateType.NOT => !a,
                LogicGateType.NAND => !(a && b),
                LogicGateType.NOR => !(a || b),
                LogicGateType.XOR => a ^ b,
                _ => false
            };
            Set(gate, "out", result, 5, 0);
        }

        private void EvaluateNamedGate(ComponentNode gate)
        {
            bool a = Read(gate, "in1"), b = Read(gate, "in2");
            bool result = gate.ComponentType switch
            {
                "AND" => a && b,
                "OR" => a || b,
                "NOT" => !a,
                "NAND" => !(a && b),
                "NOR" => !(a || b),
                "XOR" => a ^ b,
                "XNOR" => !(a ^ b),
                _ => false
            };
            Set(gate, "out", result, 5, 0);
        }

        private void EvaluatePla(PLAComponent pla)
        {
            int inputWord = 0;
            for (int i = 0; i < pla.InputCount; i++) if (Read(pla, $"in{i}")) inputWord |= 1 << i;
            string[] rows = pla.TruthTable.Split(new[] { ',', ';', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            long word = inputWord < rows.Length && long.TryParse(rows[inputWord], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
            for (int i = 0; i < pla.OutputCount; i++) Set(pla, $"out{i}", (word & (1L << i)) != 0, 5, 0);
        }
    }

    private static long Mask(int width) => width >= 63 ? long.MaxValue : (1L << Math.Clamp(width, 1, 62)) - 1;
    private static bool Bool(ComponentNode component, string key, bool fallback) => component.CustomProperties.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;
    private static double Number(ComponentNode component, string key, double fallback) => component.CustomProperties.TryGetValue(key, out var value) && double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static void SetBool(ComponentNode component, string key, bool value) => component.CustomProperties[key] = value;
    private static void SetNumber(ComponentNode component, string key, double value) => component.CustomProperties[key] = value;
    private static long CountBoundaryCrossings(double phase, double total, double boundary) => Math.Max(0, (long)Math.Floor(total - boundary) - (long)Math.Floor(phase - boundary));
}
