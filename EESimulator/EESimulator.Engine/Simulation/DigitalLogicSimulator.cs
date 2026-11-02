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
        ArithmeticComponentBase or ButtonComponent or DipSwitchComponent or JoystickComponent or
        KeyboardComponent or RealTimeClockComponent or TtlPackageComponent or
        AndGateComponent or OrGateComponent or NotGateComponent or NandGateComponent or NorGateComponent or
        XorGateComponent or XnorGateComponent or LogicGateComponent;

    public static IEnumerable<(string PinId, double Voltage)> GetOutputVoltages(ComponentNode component)
    {
        foreach (var pin in component.Pins)
        {
            if (!IsOutputPin(component, pin.Id)) continue;
            if (Bool(component, $"HighZ:{pin.Id}", false)) continue;
            if (component.CustomProperties.TryGetValue($"OutputVoltage:{pin.Id}", out var outputVoltage) &&
                double.TryParse(outputVoltage?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var storedVoltage))
            {
                yield return (pin.Id, storedVoltage);
                continue;
            }
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
            ShiftRegisterComponent shift when pinId == "sout" => ((shift.Value >> (shift.Length - 1)) & 1) != 0,
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
        ButtonComponent => pinId == "out",
        DipSwitchComponent => pinId.StartsWith("Q", StringComparison.Ordinal),
        JoystickComponent => pinId.StartsWith("x", StringComparison.Ordinal) || pinId.StartsWith("y", StringComparison.Ordinal) || pinId == "button",
        KeyboardComponent => pinId.StartsWith("D", StringComparison.Ordinal) || pinId == "valid",
        RealTimeClockComponent => pinId.StartsWith("seconds", StringComparison.Ordinal) || pinId.StartsWith("minutes", StringComparison.Ordinal) || pinId.StartsWith("hours", StringComparison.Ordinal),
        ArithmeticComponentBase arithmetic => IsArithmeticOutput(arithmetic, pinId),
        TtlPackageComponent ttl => TtlPinCatalog.GetOutputs(ttl.PartNumber).Contains(ParseTtlPin(pinId)),
        BufferComponent or ControlledBufferComponent or EvenParityGateComponent or OddParityGateComponent or MultiplexerComponent or BitSelectorComponent => pinId == "out" || pinId.StartsWith("out", StringComparison.Ordinal),
        PLAComponent or DemultiplexerComponent or DecoderComponent => pinId.StartsWith("out", StringComparison.Ordinal),
        PriorityEncoderComponent => pinId.StartsWith("out", StringComparison.Ordinal) || pinId is "any_active" or "en_out",
        DFlipFlopComponent or JKFlipFlopComponent or SRFlipFlopComponent or TFlipFlopComponent => pinId is "Q" or "Q_bar",
        RegisterComponent => pinId.StartsWith("Q", StringComparison.Ordinal),
        CounterComponent => pinId.StartsWith("Q", StringComparison.Ordinal) || pinId == "carry",
        ShiftRegisterComponent => pinId == "sout" || pinId.StartsWith("Q", StringComparison.Ordinal),
        RAMComponent or ROMComponent => pinId.StartsWith("dout", StringComparison.Ordinal),
        RandomComponent => pinId.StartsWith("Q", StringComparison.Ordinal),
        AndGateComponent or OrGateComponent or NotGateComponent or NandGateComponent or NorGateComponent or XorGateComponent or XnorGateComponent => pinId == "out",
        _ => false
    };

    private static bool IsArithmeticOutput(ArithmeticComponentBase component, string pinId)
    {
        if (component is ComparatorComponent) return pinId is "gt" or "eq" or "lt";
        if (component is BitFinderComponent) return pinId.StartsWith("Y", StringComparison.Ordinal) || pinId == "found";
        if (component is AdderComponent or SubtractorComponent) return pinId.StartsWith("Y", StringComparison.Ordinal) || pinId == "cout";
        if (component is DividerComponent) return pinId.StartsWith("Y", StringComparison.Ordinal) || pinId == "divide_by_zero";
        return pinId.StartsWith("Y", StringComparison.Ordinal);
    }

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
                if (component is TtlPackageComponent ttl)
                {
                    AdvanceTtlSequential(ttl);
                    continue;
                }
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
                    RegisterComponent register => register.Trigger,
                    CounterComponent counter => counter.Trigger,
                    ShiftRegisterComponent shift => shift.Trigger,
                    RandomComponent random => random.Trigger,
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
                        else if (Read(jk, "set")) jk.StateQ = true;
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
                        if (Read(sr, "reset")) { sr.StateQ = false; SetBool(sr, "_invalidState", false); }
                        else if (Read(sr, "set")) { sr.StateQ = true; SetBool(sr, "_invalidState", false); }
                        else for (int i = 0; i < edges; i++)
                        {
                            bool set = Read(sr, "S"), reset = Read(sr, "R");
                            if (set && reset) SetBool(sr, "_invalidState", true);
                            else if (set) { sr.StateQ = true; SetBool(sr, "_invalidState", false); }
                            else if (reset) { sr.StateQ = false; SetBool(sr, "_invalidState", false); }
                        }
                        if (Bool(sr, "_invalidState", false))
                        {
                            SetHighZ(sr, "Q");
                            SetHighZ(sr, "Q_bar");
                        }
                        else
                        {
                            Set(sr, "Q", sr.StateQ, sr.HighVoltage, sr.LowVoltage);
                            Set(sr, "Q_bar", !sr.StateQ, sr.HighVoltage, sr.LowVoltage);
                        }
                        break;
                    case TFlipFlopComponent tff:
                        if (Read(tff, "reset")) tff.StateQ = false;
                        else if (Read(tff, "set")) tff.StateQ = true;
                        else for (int i = 0; i < edges; i++) if (Read(tff, "T")) tff.StateQ = !tff.StateQ;
                        Set(tff, "Q", tff.StateQ, tff.HighVoltage, tff.LowVoltage);
                        Set(tff, "Q_bar", !tff.StateQ, tff.HighVoltage, tff.LowVoltage);
                        break;
                    case RegisterComponent register:
                        if (Read(register, "clr")) register.Value = 0;
                        else if (edges > 0 && (Read(register, "en") || !HasConnected(register, "en")))
                            for (int i = 0; i < edges; i++) register.Value = ReadBus(register, "D", register.BitWidth);
                        for (int bit = 0; bit < register.BitWidth; bit++) Set(register, $"Q{bit}", ((register.Value >> bit) & 1) != 0, 5, 0);
                        break;
                    case CounterComponent counter:
                        if (Read(counter, "clr")) { counter.Value = 0; counter.CarryOut = false; }
                        else if (edges > 0 && Read(counter, "ld")) counter.Value = ReadBus(counter, "D", counter.BitWidth) & Mask(counter.BitWidth);
                        else if (edges > 0 && Read(counter, "en"))
                        {
                            long max = Math.Min(Mask(counter.BitWidth), Math.Max(1, counter.MaxValue));
                            for (int i = 0; i < edges; i++)
                            {
                                bool countUp = Read(counter, "up_down");
                                bool atGoal = countUp ? counter.Value >= max : counter.Value <= 0;
                                if (!atGoal)
                                    counter.Value = countUp ? counter.Value + 1 : counter.Value - 1;
                                else
                                {
                                    counter.Value = counter.GoalAction switch
                                    {
                                        "Stay" => counter.Value,
                                        "Continue" => countUp ? (counter.Value + 1) & Mask(counter.BitWidth) : (counter.Value - 1) & Mask(counter.BitWidth),
                                        "Load" => ReadBus(counter, "D", counter.BitWidth) & Mask(counter.BitWidth),
                                        _ => countUp ? 0 : max
                                    };
                                }
                                counter.CarryOut = atGoal;
                            }
                        }
                        for (int bit = 0; bit < counter.BitWidth; bit++) Set(counter, $"Q{bit}", ((counter.Value >> bit) & 1) != 0, 5, 0);
                        Set(counter, "carry", counter.CarryOut, 5, 0);
                        break;
                    case ShiftRegisterComponent shift:
                        if (Read(shift, "clr")) shift.Value = 0;
                        else if (edges > 0 && shift.HasParallelLoad && Read(shift, "ld")) shift.Value = ReadBus(shift, "D", shift.Length) & Mask(shift.Length);
                        else if (edges > 0 && (Read(shift, "en") || !HasConnected(shift, "en")))
                            for (int i = 0; i < edges; i++) shift.Value = ((shift.Value << 1) | (Read(shift, "sin") ? 1L : 0L)) & Mask(shift.Length);
                        for (int bit = 0; bit < shift.Length; bit++) Set(shift, $"Q{bit}", ((shift.Value >> bit) & 1) != 0, 5, 0);
                        Set(shift, "sout", ((shift.Value >> (shift.Length - 1)) & 1) != 0, 5, 0);
                        break;
                    case RandomComponent random:
                        if (Read(random, "rst"))
                        {
                            const long seedMask = (1L << 48) - 1;
                            SetNumber(random, "_randomSeed48", Math.Max(1, random.Seed) & seedMask);
                            random.Value = random.Seed;
                        }
                        else if (edges > 0 && (Read(random, "en") || !HasConnected(random, "en")))
                            for (int i = 0; i < edges; i++)
                            {
                                const long addend = 0xB;
                                const long seedMask = (1L << 48) - 1;
                                const long multiplier = 0x5DEECE66D;
                                long seed = (long)Number(random, "_randomSeed48", Math.Max(1, random.Seed) & seedMask);
                                seed = (seed * multiplier + addend) & seedMask;
                                SetNumber(random, "_randomSeed48", seed);
                                random.Value = (seed >> 12) & Mask(random.BitWidth);
                            }
                        for (int bit = 0; bit < random.BitWidth; bit++) Set(random, $"Q{bit}", ((random.Value >> bit) & 1) != 0, 5, 0);
                        break;
                    case RAMComponent ram:
                        int address = (int)ReadBus(ram, "addr", ram.AddressBitWidth);
                        if (edges > 0)
                        {
                            ram.ReadAddress = address;
                            if (Read(ram, "we")) ram.MemoryData[address] = ReadBus(ram, "din", ram.DataBitWidth) & Mask(ram.DataBitWidth);
                        }
                        break;
                }
            }
        }

        public void EvaluateCombinational()
        {
            PublishStoredOutputs();
            // Fixed-point iteration settles short cascades of combinational gates.
            for (int pass = 0; pass < Math.Clamp(Components.Count, 1, 24); pass++)
            {
                foreach (var component in Components)
                {
                    switch (component)
                    {
                        case BufferComponent buffer:
                            for (int bit = 0; bit < buffer.BitWidth; bit++)
                            {
                                string input = BusPin(buffer, "in", bit, buffer.BitWidth);
                                string output = BusPin(buffer, "out", bit, buffer.BitWidth);
                                bool inputState = Read(buffer, input);
                                string mode = buffer.OutputMode;
                                if ((mode == "0Z" && inputState) || (mode == "Z1" && !inputState)) SetHighZ(buffer, output);
                                else Set(buffer, output, inputState, buffer.HighVoltage, buffer.LowVoltage);
                            }
                            break;
                        case ControlledBufferComponent tri:
                            bool triEnabled = Read(tri, "ctrl") != tri.ControlActiveLow;
                            SetBool(tri, "_triStateEnabled", triEnabled);
                            for (int bit = 0; bit < tri.BitWidth; bit++)
                            {
                                string input = BusPin(tri, "in", bit, tri.BitWidth);
                                string output = BusPin(tri, "out", bit, tri.BitWidth);
                                if (triEnabled) Set(tri, output, Read(tri, input), tri.HighVoltage, tri.LowVoltage);
                                else SetHighZ(tri, output);
                            }
                            break;
                        case EvenParityGateComponent even:
                            Set(even, "out", CountInputs(even, even.NumberOfInputs, true) % 2 == 0, 5, 0);
                            break;
                        case OddParityGateComponent odd:
                            Set(odd, "out", CountInputs(odd, odd.NumberOfInputs, true) % 2 != 0, 5, 0);
                            break;
                        case PLAComponent pla:
                            EvaluatePla(pla);
                            break;
                        case MultiplexerComponent mux:
                            int muxIndex = (int)ReadBus(mux, "sel", mux.SelectBits);
                            long muxWord = ReadBus(mux, $"in{muxIndex}", mux.BitWidth);
                            bool muxEnabled = !mux.HasEnable || Read(mux, "en");
                            for (int bit = 0; bit < mux.BitWidth; bit++)
                            {
                                string output = BusPin(mux, "out", bit, mux.BitWidth, "_b");
                                if (!muxEnabled && mux.ThreeStateOutputs) SetHighZ(mux, output);
                                else Set(mux, output, muxEnabled && ((muxWord >> bit) & 1) != 0, 5, 0);
                            }
                            break;
                        case DemultiplexerComponent demux:
                            int selected = (int)ReadBus(demux, "sel", demux.SelectBits);
                            long data = ReadBus(demux, "in", demux.BitWidth);
                            bool demuxEnabled = !demux.HasEnable || Read(demux, "en");
                            for (int output = 0; output < (1 << demux.SelectBits); output++)
                                for (int bit = 0; bit < demux.BitWidth; bit++)
                                {
                                    string outputPin = demux.BitWidth == 1 ? $"out{output}" : $"out{output}_b{bit}";
                                    if ((!demuxEnabled || selected != output) && demux.ThreeStateOutputs) SetHighZ(demux, outputPin);
                                    else Set(demux, outputPin, demuxEnabled && selected == output && ((data >> bit) & 1) != 0, 5, 0);
                                }
                            break;
                        case DecoderComponent decoder:
                            int decoded = (int)ReadBus(decoder, "sel", decoder.SelectBits);
                            bool enabled = !decoder.HasEnable || Read(decoder, "en");
                            for (int i = 0; i < (1 << decoder.SelectBits); i++)
                            {
                                if (!enabled && decoder.ThreeStateOutputs) SetHighZ(decoder, $"out{i}");
                                else Set(decoder, $"out{i}", enabled && decoded == i, 5, 0);
                            }
                            break;
                        case PriorityEncoderComponent encoder:
                            int active = -1;
                            for (int i = 0; i < (1 << encoder.SelectBits); i++) if (Read(encoder, $"in{i}")) active = i;
                            bool encoderEnabled = Read(encoder, "en");
                            SetNumber(encoder, "EncodedValue", encoderEnabled ? Math.Max(active, 0) : 0);
                            for (int bit = 0; bit < encoder.SelectBits; bit++)
                            {
                                if ((encoderEnabled && active < 0) || (!encoderEnabled && encoder.ThreeStateOutputs)) SetHighZ(encoder, $"out{bit}");
                                else Set(encoder, $"out{bit}", encoderEnabled && active >= 0 && ((active >> bit) & 1) != 0, 5, 0);
                            }
                            Set(encoder, "any_active", encoderEnabled && active >= 0, 5, 0);
                            Set(encoder, "en_out", encoderEnabled && active < 0, 5, 0);
                            break;
                        case BitSelectorComponent selector:
                            int bitIndex = (int)ReadBus(selector, "sel", selector.SelectWidth);
                            long dataWord = ReadBus(selector, "data", selector.DataBusWidth);
                            long selectedGroup = bitIndex * (long)selector.GroupWidth;
                            for (int bit = 0; bit < selector.GroupWidth; bit++)
                            {
                                string output = selector.GroupWidth == 1 ? "out" : $"out_b{bit}";
                                long sourceBit = selectedGroup + bit;
                                Set(selector, output, sourceBit < selector.DataBusWidth && ((dataWord >> (int)sourceBit) & 1) != 0, 5, 0);
                            }
                            break;
                        case RAMComponent ram:
                            EvaluateRamRead(ram);
                            break;
                        case ROMComponent rom:
                            EvaluateRomRead(rom);
                            break;
                        case ArithmeticComponentBase arithmetic:
                            EvaluateArithmetic(arithmetic);
                            break;
                        case TtlPackageComponent ttl:
                            EvaluateTtlCombinational(ttl);
                            break;
                        case ButtonComponent button:
                            Set(button, "out", button.IsPressed, 5, 0);
                            break;
                        case DipSwitchComponent dip:
                            for (int bit = 0; bit < dip.PoleCount; bit++) Set(dip, $"Q{bit}", ((dip.Value >> bit) & 1) != 0, 5, 0);
                            break;
                        case JoystickComponent joystick:
                            for (int bit = 0; bit < 8; bit++)
                            {
                                Set(joystick, $"x{bit}", ((joystick.XValue >> bit) & 1) != 0, 5, 0);
                                Set(joystick, $"y{bit}", ((joystick.YValue >> bit) & 1) != 0, 5, 0);
                            }
                            Set(joystick, "button", joystick.Pressed, 5, 0);
                            break;
                        case KeyboardComponent keyboard:
                            for (int bit = 0; bit < 8; bit++) Set(keyboard, $"D{bit}", ((keyboard.Data >> bit) & 1) != 0, 5, 0);
                            Set(keyboard, "valid", keyboard.DataReady, 5, 0);
                            if (Read(keyboard, "ack")) keyboard.DataReady = false;
                            break;
                        case RealTimeClockComponent rtc:
                            var now = DateTime.Now;
                            rtc.CustomProperties["RenderedSeconds"] = now.Second;
                            rtc.CustomProperties["RenderedMinutes"] = now.Minute;
                            rtc.CustomProperties["RenderedHours"] = now.Hour;
                            WriteBusOutputs(rtc, "seconds", (long)now.Second, 32);
                            WriteBusOutputs(rtc, "minutes", (long)now.Minute, 6);
                            WriteBusOutputs(rtc, "hours", (long)now.Hour, 5);
                            break;
                        case SevenSegmentComponent sevenSegment:
                            int segments = 0;
                            for (int bit = 0; bit < 8; bit++) if (Read(sevenSegment, $"seg{bit}") != sevenSegment.ActiveLow) segments |= 1 << bit;
                            sevenSegment.CustomProperties["RenderedSegments"] = segments;
                            break;
                        case HexDigitComponent hexDigit:
                            hexDigit.CustomProperties["RenderedValue"] = (int)ReadBus(hexDigit, "D", 4);
                            break;
                        case LedBarComponent ledBar:
                            ledBar.CustomProperties["RenderedValue"] = ReadBus(ledBar, "D", ledBar.LedCount);
                            break;
                        case RgbLedComponent rgb:
                            rgb.CustomProperties["RenderedRgb"] = (Read(rgb, "R") ? 1 : 0) | (Read(rgb, "G") ? 2 : 0) | (Read(rgb, "B") ? 4 : 0);
                            break;
                        case DotMatrixComponent matrix:
                            long rows = ReadBus(matrix, "row", matrix.Rows);
                            long columns = ReadBus(matrix, "col", matrix.Columns);
                            matrix.CustomProperties["RenderedRows"] = rows;
                            matrix.CustomProperties["RenderedColumns"] = columns;
                            break;
                        case TtyComponent tty:
                            if (Read(tty, "clear")) tty.Text = string.Empty;
                            else if (Read(tty, "strobe") && !Bool(tty, "_previousStrobe", false))
                            {
                                tty.Text += (char)ReadBus(tty, "D", 8);
                                if (tty.Text.Length > 4096) tty.Text = tty.Text[^4096..];
                            }
                            SetBool(tty, "_previousStrobe", Read(tty, "strobe"));
                            break;
                        case VideoComponent video:
                            video.CustomProperties["RenderedRgb"] = (long)ReadBus(video, "R", 8) << 16 | (long)ReadBus(video, "G", 8) << 8 | ReadBus(video, "B", 8);
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

        private void PublishStoredOutputs()
        {
            foreach (var component in Components)
            {
                switch (component)
                {
                    case DFlipFlopComponent dff:
                        Set(dff, "Q", dff.StateQ, dff.HighVoltage, dff.LowVoltage);
                        Set(dff, "Q_bar", !dff.StateQ, dff.HighVoltage, dff.LowVoltage);
                        break;
                    case JKFlipFlopComponent jk:
                        Set(jk, "Q", jk.StateQ, jk.HighVoltage, jk.LowVoltage);
                        Set(jk, "Q_bar", !jk.StateQ, jk.HighVoltage, jk.LowVoltage);
                        break;
                    case SRFlipFlopComponent sr:
                        if (Bool(sr, "_invalidState", false))
                        {
                            SetHighZ(sr, "Q");
                            SetHighZ(sr, "Q_bar");
                        }
                        else
                        {
                            Set(sr, "Q", sr.StateQ, sr.HighVoltage, sr.LowVoltage);
                            Set(sr, "Q_bar", !sr.StateQ, sr.HighVoltage, sr.LowVoltage);
                        }
                        break;
                    case TFlipFlopComponent tff:
                        Set(tff, "Q", tff.StateQ, tff.HighVoltage, tff.LowVoltage);
                        Set(tff, "Q_bar", !tff.StateQ, tff.HighVoltage, tff.LowVoltage);
                        break;
                    case RegisterComponent register:
                        for (int bit = 0; bit < register.BitWidth; bit++) Set(register, $"Q{bit}", ((register.Value >> bit) & 1) != 0, 5, 0);
                        break;
                    case CounterComponent counter:
                        for (int bit = 0; bit < counter.BitWidth; bit++) Set(counter, $"Q{bit}", ((counter.Value >> bit) & 1) != 0, 5, 0);
                        Set(counter, "carry", counter.CarryOut, 5, 0);
                        break;
                    case ShiftRegisterComponent shift:
                        for (int bit = 0; bit < shift.Length; bit++) Set(shift, $"Q{bit}", ((shift.Value >> bit) & 1) != 0, 5, 0);
                        Set(shift, "sout", ((shift.Value >> (shift.Length - 1)) & 1) != 0, 5, 0);
                        break;
                    case RandomComponent random:
                        for (int bit = 0; bit < random.BitWidth; bit++) Set(random, $"Q{bit}", ((random.Value >> bit) & 1) != 0, 5, 0);
                        break;
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
                if (Bool(source, $"HighZ:{pin.PinId}", false)) continue;
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
            => CountInputs(component, inputCount, false);

        private int CountInputs(ComponentNode component, int inputCount, bool applyNegation)
        {
            int count = 0;
            for (int i = 0; i < inputCount; i++)
            {
                string pinId = component.GetPin($"in{i + 1}") != null ? $"in{i + 1}" : $"in{i}";
                bool value = Read(component, pinId);
                if (applyNegation && Bool(component, $"Negate:{pinId}", false)) value = !value;
                if (value) count++;
            }
            return count;
        }

        private void EvaluateRamRead(RAMComponent ram)
        {
            int address = ram.AsynchronousRead ? (int)ReadBus(ram, "addr", ram.AddressBitWidth) : ram.ReadAddress;
            bool outputEnabled = !ram.HasOutputEnable || Read(ram, "oe");
            long word = ram.MemoryData.TryGetValue(address, out var storedWord) ? storedWord : 0;
            for (int bit = 0; bit < ram.DataBitWidth; bit++)
            {
                string pinId = $"dout{bit}";
                if (!outputEnabled) SetHighZ(ram, pinId);
                else Set(ram, pinId, ((word >> bit) & 1) != 0, 5, 0);
            }
        }

        private void EvaluateRomRead(ROMComponent rom)
        {
            bool selected = !rom.HasChipSelect || Read(rom, "sel");
            int address = (int)ReadBus(rom, "addr", rom.AddressBitWidth);
            long word = ReadRomWord(rom, address);
            for (int bit = 0; bit < rom.DataBitWidth; bit++)
            {
                string pinId = $"dout{bit}";
                if (!selected) SetHighZ(rom, pinId);
                else Set(rom, pinId, ((word >> bit) & 1) != 0, 5, 0);
            }
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

        private void EvaluateArithmetic(ArithmeticComponentBase component)
        {
            int width = component.BitWidth;
            long mask = Mask(width);
            long a = ReadBus(component, "A", width) & mask;
            long b = component is ArithmeticComponentBase { ComponentType: "Shifter" }
                ? 0
                : ReadBus(component, "B", width) & mask;
            long result = 0;
            bool carry = false;
            bool found = false;
            bool divideByZero = false;
            bool signed = Bool(component, "Signed", false);
            long signedA = ToSigned(a, width);
            long signedB = ToSigned(b, width);

            if (component is AdderComponent)
            {
                long sum = a + b + (Read(component, "cin") ? 1L : 0L);
                result = sum & mask;
                carry = (sum & ~mask) != 0;
            }
            else if (component is SubtractorComponent)
            {
                long diff = a - b - (Read(component, "cin") ? 1L : 0L);
                result = diff & mask;
                carry = a >= b + (Read(component, "cin") ? 1L : 0L);
            }
            else if (component is MultiplierComponent) result = (a * b) & mask;
            else if (component is DividerComponent)
            {
                divideByZero = b == 0;
                result = divideByZero ? 0 : (signed ? signedA / signedB : a / b) & mask;
            }
            else if (component is ComparatorComponent)
            {
                long compareA = signed ? signedA : a;
                long compareB = signed ? signedB : b;
                Set(component, "gt", compareA > compareB, 5, 0);
                Set(component, "eq", compareA == compareB, 5, 0);
                Set(component, "lt", compareA < compareB, 5, 0);
                return;
            }
            else if (component is NegatorComponent) result = (-signedA) & mask;
            else if (component is ShifterComponent shifter)
            {
                int shift = (int)ReadBus(component, "S", component.ShiftWidth);
                bool arith = shifter.Mode.Equals("ArithmeticRight", StringComparison.OrdinalIgnoreCase) || shifter.Mode.Equals("ASR", StringComparison.OrdinalIgnoreCase);
                bool right = shifter.Mode.Contains("Right", StringComparison.OrdinalIgnoreCase) || shifter.Mode.Equals("LSR", StringComparison.OrdinalIgnoreCase);
                bool rotate = shifter.Mode.StartsWith("Rotate", StringComparison.OrdinalIgnoreCase) || shifter.Mode.StartsWith("ROL", StringComparison.OrdinalIgnoreCase) || shifter.Mode.StartsWith("ROR", StringComparison.OrdinalIgnoreCase);
                if (rotate) shift %= width;
                if (width > 0 && shift >= width) result = right && arith && ((a >> (width - 1)) & 1) != 0 ? mask : 0;
                else if (rotate && right) result = ((a >> shift) | (a << (width - shift))) & mask;
                else if (rotate) result = ((a << shift) | (a >> (width - shift))) & mask;
                else if (right && arith) result = (signedA >> shift) & mask;
                else if (right) result = a >> shift;
                else result = (a << shift) & mask;
            }
            else if (component is BitAdderComponent)
            {
                for (int bit = 0; bit < width; bit++) if (((a >> bit) & 1) != 0) result++;
            }
            else if (component is BitFinderComponent finder)
            {
                bool findLast = finder.Mode.Equals("Last", StringComparison.OrdinalIgnoreCase) || finder.Mode.Equals("MSB", StringComparison.OrdinalIgnoreCase);
                result = findLast ? width - 1 : 0;
                found = false;
                for (int step = 0; step < width; step++)
                {
                    int bit = findLast ? width - 1 - step : step;
                    if (((a >> bit) & 1) == 0) continue;
                    result = bit;
                    found = true;
                    break;
                }
                Set(component, "found", found, 5, 0);
            }
            else if (component is SquareRootComponent) result = (long)Math.Sqrt(a);
            else if (component is AbsoluteValueComponent) result = Math.Abs(signedA) & mask;
            else if (component is MinMaxComponent)
            {
                bool maximum = component.Mode.Equals("Max", StringComparison.OrdinalIgnoreCase) || component.Mode.Equals("Maximum", StringComparison.OrdinalIgnoreCase);
                long av = signed ? signedA : a, bv = signed ? signedB : b;
                result = (maximum ? Math.Max(av, bv) : Math.Min(av, bv)) & mask;
            }
            else if (component is ExponentiatorComponent)
            {
                long exponent = Math.Min(b, 1024);
                result = 1;
                for (long i = 0; i < exponent; i++) result = (result * a) & mask;
            }

            WriteBusOutputs(component, "Y", result & mask, width);
            if (component is AdderComponent or SubtractorComponent) Set(component, "cout", carry, 5, 0);
            if (component is DividerComponent) Set(component, "divide_by_zero", divideByZero, 5, 0);
        }

        private static long ToSigned(long value, int width)
        {
            if (width >= 64) return value;
            long sign = 1L << (width - 1);
            return (value & sign) == 0 ? value : value - (1L << width);
        }

        private void WriteBusOutputs(ComponentNode component, string prefix, long value, int width)
        {
            for (int bit = 0; bit < width; bit++) Set(component, $"{prefix}{bit}", ((value >> bit) & 1) != 0, 5, 0);
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

        private bool TtlRead(TtlPackageComponent ttl, int pin) => Read(ttl, $"pin{pin}");

        private void EvaluateTtlCombinational(TtlPackageComponent ttl)
        {
            foreach (var gate in TtlPinCatalog.GetGates(ttl.PartNumber))
            {
                bool[] inputs = gate.Inputs.Select(pin => TtlRead(ttl, pin)).ToArray();
                bool value = gate.Operation switch
                {
                    "AND" => inputs.All(input => input),
                    "OR" => inputs.Any(input => input),
                    "NAND" => !inputs.All(input => input),
                    "NOR" => !inputs.Any(input => input),
                    "NOT" => !inputs[0],
                    "BUF" => inputs[0],
                    "XOR" => inputs.Aggregate(false, (current, input) => current ^ input),
                    "XNOR" => !inputs.Aggregate(false, (current, input) => current ^ input),
                    "AO22" => (inputs[0] && inputs[1]) || (inputs[2] && inputs[3]),
                    "AOI22" => !((inputs[0] && inputs[1]) || (inputs[2] && inputs[3])),
                    "AO32" => (inputs[0] && inputs[1] && inputs[2]) || (inputs[3] && inputs[4] && inputs[5]),
                    _ => false
                };
                if ((ttl.PartNumber is "74266" or "747266") && value) SetHighZ(ttl, $"pin{gate.Output}");
                else Set(ttl, $"pin{gate.Output}", value, 5, 0);
            }

            switch (ttl.PartNumber)
            {
                case "74138":
                    int selected = (TtlRead(ttl, 1) ? 1 : 0) | (TtlRead(ttl, 2) ? 2 : 0) | (TtlRead(ttl, 3) ? 4 : 0);
                    bool enabled = TtlRead(ttl, 6) && !TtlRead(ttl, 4) && !TtlRead(ttl, 5);
                    int[] outputs = { 15, 14, 13, 12, 11, 10, 9, 7 };
                    for (int i = 0; i < outputs.Length; i++) SetTtl(ttl, outputs[i], !enabled || selected != i);
                    break;
                case "74139":
                    EvaluateTtlDecoder2(ttl, 1, 2, 3, new[] { 4, 5, 6, 7 });
                    EvaluateTtlDecoder2(ttl, 15, 14, 13, new[] { 12, 11, 10, 9 });
                    break;
                case "74151":
                    int mux = (TtlRead(ttl, 11) ? 1 : 0) | (TtlRead(ttl, 10) ? 2 : 0) | (TtlRead(ttl, 9) ? 4 : 0);
                    int[] muxInputs = { 4, 3, 2, 1, 15, 14, 13, 12 };
                    bool data = TtlRead(ttl, muxInputs[mux]);
                    SetTtl(ttl, 5, !TtlRead(ttl, 7) && data);
                    SetTtl(ttl, 6, TtlRead(ttl, 7) || !data);
                    break;
                case "74153":
                    EvaluateTtlMux4(ttl, 1, 14, 2, new[] { 6, 5, 4, 3 }, 7);
                    EvaluateTtlMux4(ttl, 15, 14, 2, new[] { 10, 11, 12, 13 }, 9);
                    break;
                case "74157":
                    int[] inA = { 2, 5, 10, 13 }, inB = { 3, 6, 11, 14 }, outPins = { 4, 7, 9, 12 };
                    bool selectB = TtlRead(ttl, 1), disabled = TtlRead(ttl, 15);
                    for (int i = 0; i < 4; i++) SetTtl(ttl, outPins[i], !disabled && TtlRead(ttl, selectB ? inB[i] : inA[i]));
                    break;
                case "7442":
                case "7443":
                case "7444":
                    int encoded = (TtlRead(ttl, 15) ? 1 : 0) | (TtlRead(ttl, 14) ? 2 : 0) | (TtlRead(ttl, 13) ? 4 : 0) | (TtlRead(ttl, 12) ? 8 : 0);
                    int decoded = ttl.PartNumber switch
                    {
                        "7442" => encoded < 10 ? encoded : -1,
                        // 7443 decodes the excess-3 code (decimal 0 is encoded as 3).
                        "7443" => encoded is >= 3 and <= 12 ? encoded - 3 : -1,
                        // 7444 decodes the 4-bit Gray code into decimal digits.
                        "7444" => encoded switch
                        {
                            2 => 0, 6 => 1, 7 => 2, 5 => 3, 4 => 4,
                            12 => 5, 13 => 6, 15 => 7, 14 => 8, 10 => 9,
                            _ => -1
                        },
                        _ => -1
                    };
                    int[] bcdOutputs = { 1, 2, 3, 4, 5, 6, 7, 9, 10, 11 };
                    for (int i = 0; i < bcdOutputs.Length; i++) SetTtl(ttl, bcdOutputs[i], decoded != i);
                    break;
                case "7447":
                    EvaluateTtl7447(ttl);
                    break;
                case "7485":
                    EvaluateTtl7485(ttl);
                    break;
                case "74283":
                    EvaluateTtl74283(ttl);
                    break;
                case "74240":
                case "74241":
                case "74244":
                case "74541":
                    EvaluateTtlOctalBuffer(ttl);
                    break;
                case "74245":
                    EvaluateTtlTransceiver245(ttl);
                    break;
                case "7454":
                    SetTtl(ttl, 8, !((TtlRead(ttl, 1) && TtlRead(ttl, 13)) || (TtlRead(ttl, 2) && TtlRead(ttl, 3)) || (TtlRead(ttl, 4) && TtlRead(ttl, 5)) || (TtlRead(ttl, 9) && TtlRead(ttl, 10))));
                    break;
                case "7464":
                    SetTtl(ttl, 8, !((TtlRead(ttl, 2) && TtlRead(ttl, 3)) || (TtlRead(ttl, 4) && TtlRead(ttl, 5) && TtlRead(ttl, 6)) || (TtlRead(ttl, 9) && TtlRead(ttl, 10)) || (TtlRead(ttl, 11) && TtlRead(ttl, 12) && TtlRead(ttl, 13) && TtlRead(ttl, 1))));
                    break;
                case "7487":
                    EvaluateTtl7487(ttl);
                    break;
                case "74381":
                    EvaluateTtl74381(ttl);
                    break;
            }
        }

        private void EvaluateTtl7487(TtlPackageComponent ttl)
        {
            bool controlB = TtlRead(ttl, 8);
            bool controlC = TtlRead(ttl, 1);
            int[] inputs = { 2, 5, 10, 13 };
            int[] outputs = { 3, 6, 9, 12 };
            for (int bit = 0; bit < inputs.Length; bit++)
            {
                bool value = controlB ? !controlC : TtlRead(ttl, inputs[bit]) ^ !controlC;
                SetTtl(ttl, outputs[bit], value);
            }
        }

        private void EvaluateTtl74381(TtlPackageComponent ttl)
        {
            bool[] a = { TtlRead(ttl, 3), TtlRead(ttl, 1), TtlRead(ttl, 19), TtlRead(ttl, 17) };
            bool[] b = { TtlRead(ttl, 4), TtlRead(ttl, 2), TtlRead(ttl, 18), TtlRead(ttl, 16) };
            bool[] select = { TtlRead(ttl, 5), TtlRead(ttl, 6), TtlRead(ttl, 7) };
            bool u = select[0] || select[1];
            bool v = select[1] || select[2];
            bool w = select[0] || !select[1];
            bool x = !(select[0] && select[1]) || select[2];
            bool y = (select[0] && select[1]) || !select[2];
            bool z = !select[2] && u;
            bool ci = TtlRead(ttl, 15);
            bool[] level1 = a.Concat(a.Select(value => !value))
                .Concat(b).Concat(b.Select(value => !value))
                .Concat(new[] { y, x, w, v, u }).ToArray();
            bool[] j =
            {
                Pal(level1, 0x000c1010, 0x00161001, 0x000a0110),
                Pal(level1, 0x000c2020, 0x00162002, 0x000a0220),
                Pal(level1, 0x000c4040, 0x00164004, 0x000a0440),
                Pal(level1, 0x000c8080, 0x00168008, 0x000a0880)
            };
            bool[] k =
            {
                Pal(level1, 0x00131010, 0x000c1001, 0x000c0110, 0x00120101),
                Pal(level1, 0x00132020, 0x000c2002, 0x000c0220, 0x00120202),
                Pal(level1, 0x00134040, 0x000c4004, 0x000c0440, 0x00120404),
                Pal(level1, 0x00138080, 0x000c8008, 0x000c0880, 0x00120808)
            };
            bool[] level2 = j.Concat(k).Concat(new[] { z, ci }).ToArray();
            bool[] l =
            {
                Pal(level2, 0x00000300),
                Pal(level2, 0x00000301, 0x00000111),
                Pal(level2, 0x00000303, 0x00000113, 0x00000122),
                Pal(level2, 0x00000307, 0x00000117, 0x00000126, 0x00000144)
            };
            bool propagate = Pal(level2, 0x0000000f);
            bool generate = Pal(level2, 0x00000088, 0x0000004c, 0x0000002e, 0x0000001f);
            SetTtl(ttl, 8, l[0] ^ k[0]);
            SetTtl(ttl, 9, l[1] ^ k[1]);
            SetTtl(ttl, 11, l[2] ^ k[2]);
            SetTtl(ttl, 12, l[3] ^ k[3]);
            SetTtl(ttl, 14, propagate);
            SetTtl(ttl, 13, generate);
        }

        private static bool Pal(IReadOnlyList<bool> inputs, params int[] products)
        {
            foreach (int product in products)
            {
                bool term = true;
                for (int bit = 0; bit < inputs.Count && term; bit++)
                    if ((product & (1 << bit)) != 0) term = inputs[bit];
                if (term) return false;
            }
            return true;
        }

        private void EvaluateTtl7447(TtlPackageComponent ttl)
        {
            int digit = (TtlRead(ttl, 7) ? 1 : 0) | (TtlRead(ttl, 1) ? 2 : 0) | (TtlRead(ttl, 2) ? 4 : 0) | (TtlRead(ttl, 6) ? 8 : 0);
            // a,b,c,d,e,f,g; outputs are active LOW.
            int[] segments = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
            bool lampTest = !TtlRead(ttl, 3);
            bool blank = !TtlRead(ttl, 4);
            bool blankRipple = !TtlRead(ttl, 5);
            int pattern = lampTest ? 0x7F : blank || (blankRipple && digit == 0) ? 0 : digit < 10 ? segments[digit] : 0;
            int[] segmentPins = { 13, 12, 11, 10, 9, 15, 14 };
            for (int bit = 0; bit < segmentPins.Length; bit++)
            {
                if (((pattern >> bit) & 1) != 0) SetTtl(ttl, segmentPins[bit], false);
                else SetHighZ(ttl, $"pin{segmentPins[bit]}");
            }
        }

        private void EvaluateTtl7485(TtlPackageComponent ttl)
        {
            int a = (TtlRead(ttl, 10) ? 1 : 0) | (TtlRead(ttl, 12) ? 2 : 0) | (TtlRead(ttl, 15) ? 4 : 0) | (TtlRead(ttl, 1) ? 8 : 0);
            int b = (TtlRead(ttl, 9) ? 1 : 0) | (TtlRead(ttl, 11) ? 2 : 0) | (TtlRead(ttl, 14) ? 4 : 0) | (TtlRead(ttl, 13) ? 8 : 0);
            bool gt = a > b, eq = a == b, lt = a < b;
            if (eq) { gt = TtlRead(ttl, 4); lt = TtlRead(ttl, 2); eq = TtlRead(ttl, 3); }
            SetTtl(ttl, 7, gt); SetTtl(ttl, 6, eq); SetTtl(ttl, 5, lt);
        }

        private void EvaluateTtl74283(TtlPackageComponent ttl)
        {
            int a = (TtlRead(ttl, 5) ? 1 : 0) | (TtlRead(ttl, 3) ? 2 : 0) | (TtlRead(ttl, 14) ? 4 : 0) | (TtlRead(ttl, 12) ? 8 : 0);
            int b = (TtlRead(ttl, 6) ? 1 : 0) | (TtlRead(ttl, 2) ? 2 : 0) | (TtlRead(ttl, 15) ? 4 : 0) | (TtlRead(ttl, 11) ? 8 : 0);
            int sum = a + b + (TtlRead(ttl, 7) ? 1 : 0);
            SetTtl(ttl, 4, (sum & 1) != 0); SetTtl(ttl, 1, (sum & 2) != 0);
            SetTtl(ttl, 13, (sum & 4) != 0); SetTtl(ttl, 10, (sum & 8) != 0); SetTtl(ttl, 9, (sum & 16) != 0);
        }

        private void EvaluateTtlOctalBuffer(TtlPackageComponent ttl)
        {
            int[] inputs = { 2, 4, 6, 8, 11, 13, 15, 17 };
            int[] outputs = { 18, 16, 14, 12, 9, 7, 5, 3 };
            for (int bit = 0; bit < inputs.Length; bit++)
            {
                int enablePin = bit < 4 ? 1 : 19;
                bool enabled = ttl.PartNumber == "74241" && bit >= 4 ? TtlRead(ttl, enablePin) : !TtlRead(ttl, enablePin);
                if (!enabled) SetHighZ(ttl, $"pin{outputs[bit]}");
                else SetTtl(ttl, outputs[bit], TtlRead(ttl, inputs[bit]) ^ (ttl.PartNumber == "74240"));
            }
        }

        private void EvaluateTtlTransceiver245(TtlPackageComponent ttl)
        {
            bool enabled = !TtlRead(ttl, 19);
            bool aToB = TtlRead(ttl, 1);
            int[] aPins = { 2, 3, 4, 5, 6, 7, 8, 9 };
            int[] bPins = { 18, 17, 16, 15, 14, 13, 12, 11 };
            for (int bit = 0; bit < 8; bit++)
            {
                if (!enabled)
                {
                    SetHighZ(ttl, $"pin{aPins[bit]}");
                    SetHighZ(ttl, $"pin{bPins[bit]}");
                }
                else if (aToB)
                {
                    SetHighZ(ttl, $"pin{aPins[bit]}");
                    SetTtl(ttl, bPins[bit], TtlRead(ttl, aPins[bit]));
                }
                else
                {
                    SetTtl(ttl, aPins[bit], TtlRead(ttl, bPins[bit]));
                    SetHighZ(ttl, $"pin{bPins[bit]}");
                }
            }
        }

        private void AdvanceTtl191(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlCounter", 0) & 0xF;
            bool enabled = !TtlRead(ttl, 14);
            if (!TtlRead(ttl, 11)) value = (TtlRead(ttl, 15) ? 1 : 0) | (TtlRead(ttl, 1) ? 2 : 0) | (TtlRead(ttl, 10) ? 4 : 0) | (TtlRead(ttl, 9) ? 8 : 0);
            else if (enabled && ClockEdges(ttl, "pin5", ClockTriggerType.RisingEdge) > 0) value = (value + 1) & 0xF;
            else if (enabled && ClockEdges(ttl, "pin4", ClockTriggerType.RisingEdge) > 0) value = (value - 1) & 0xF;
            SetNumber(ttl, "_ttlCounter", value);
            SetTtl(ttl, 3, (value & 1) != 0); SetTtl(ttl, 2, (value & 2) != 0);
            SetTtl(ttl, 6, (value & 4) != 0); SetTtl(ttl, 7, (value & 8) != 0);
            SetTtl(ttl, 13, value == 15); SetTtl(ttl, 12, value == 0);
        }

        private void AdvanceTtl194(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlShift", 0) & 0xF;
            if (!TtlRead(ttl, 1)) value = 0;
            else if (ClockEdges(ttl, "pin11", ClockTriggerType.RisingEdge) > 0)
            {
                int mode = (TtlRead(ttl, 10) ? 1 : 0) | (TtlRead(ttl, 9) ? 2 : 0);
                if (mode == 3) value = (TtlRead(ttl, 3) ? 1 : 0) | (TtlRead(ttl, 4) ? 2 : 0) | (TtlRead(ttl, 5) ? 4 : 0) | (TtlRead(ttl, 6) ? 8 : 0);
                else if (mode == 1) value = ((value >> 1) | (TtlRead(ttl, 7) ? 8L : 0L)) & 0xF;
                else if (mode == 2) value = ((value << 1) | (TtlRead(ttl, 2) ? 1L : 0L)) & 0xF;
            }
            SetNumber(ttl, "_ttlShift", value);
            SetTtl(ttl, 15, (value & 1) != 0); SetTtl(ttl, 14, (value & 2) != 0);
            SetTtl(ttl, 13, (value & 4) != 0); SetTtl(ttl, 12, (value & 8) != 0);
        }

        private void AdvanceTtl299(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlShift", 0) & 0xFF;
            if (!TtlRead(ttl, 9)) value = 0;
            else if (ClockEdges(ttl, "pin12", ClockTriggerType.RisingEdge) > 0)
            {
                int mode = (TtlRead(ttl, 19) ? 2 : 0) | (TtlRead(ttl, 1) ? 1 : 0);
                int[] ioPins = { 16, 4, 15, 5, 14, 6, 13, 7 };
                if (mode == 3)
                {
                    value = 0;
                    for (int bit = 0; bit < 8; bit++) if (TtlRead(ttl, ioPins[bit])) value |= 1L << bit;
                }
                else if (mode == 1) value = ((value >> 1) | (TtlRead(ttl, 11) ? 0x80L : 0L)) & 0xFF;
                else if (mode == 2) value = ((value << 1) | (TtlRead(ttl, 18) ? 1L : 0L)) & 0xFF;
            }
            SetNumber(ttl, "_ttlShift", value);
            SetTtl(ttl, 8, (value & 0x80) != 0);
            SetTtl(ttl, 17, (value & 1) != 0);
            bool outputEnabled = !TtlRead(ttl, 2) && !TtlRead(ttl, 3) && (!TtlRead(ttl, 1) || !TtlRead(ttl, 19));
            int[] outputs = { 16, 4, 15, 5, 14, 6, 13, 7 };
            for (int bit = 0; bit < outputs.Length; bit++)
            {
                if (outputEnabled) SetTtl(ttl, outputs[bit], ((value >> bit) & 1) != 0);
                else SetHighZ(ttl, $"pin{outputs[bit]}");
            }
        }

        private void AdvanceTtl74670(TtlPackageComponent ttl)
        {
            if (!ttl.CustomProperties.TryGetValue("_ttl74670", out var raw) || raw is not Dictionary<int, long>)
                ttl.CustomProperties["_ttl74670"] = new Dictionary<int, long>();
            var memory = (Dictionary<int, long>)ttl.CustomProperties["_ttl74670"];
            int readAddress = (TtlRead(ttl, 4) ? 2 : 0) | (TtlRead(ttl, 5) ? 1 : 0);
            int writeAddress = (TtlRead(ttl, 13) ? 2 : 0) | (TtlRead(ttl, 14) ? 1 : 0);
            if (ClockEdges(ttl, "pin12", ClockTriggerType.FallingEdge) > 0 && !TtlRead(ttl, 12))
            {
                long write = (TtlRead(ttl, 15) ? 1L : 0L) | (TtlRead(ttl, 1) ? 2L : 0L) | (TtlRead(ttl, 2) ? 4L : 0L) | (TtlRead(ttl, 3) ? 8L : 0L);
                memory[writeAddress] = write;
            }
            bool outputEnabled = !TtlRead(ttl, 11);
            long value = memory.TryGetValue(readAddress, out var stored) ? stored : 0;
            int[] outputs = { 10, 9, 7, 6 };
            for (int bit = 0; bit < outputs.Length; bit++)
            {
                if (outputEnabled) SetTtl(ttl, outputs[bit], ((value >> bit) & 1) != 0);
                else SetHighZ(ttl, $"pin{outputs[bit]}");
            }
        }

        private void EvaluateTtlDecoder2(TtlPackageComponent ttl, int enablePin, int aPin, int bPin, int[] outputPins)
        {
            int selected = (TtlRead(ttl, aPin) ? 1 : 0) | (TtlRead(ttl, bPin) ? 2 : 0);
            bool enabled = !TtlRead(ttl, enablePin);
            for (int i = 0; i < outputPins.Length; i++) SetTtl(ttl, outputPins[i], !enabled || selected != i);
        }

        private void EvaluateTtlMux4(TtlPackageComponent ttl, int enablePin, int selectA, int selectB, int[] dataPins, int outputPin)
        {
            int index = (TtlRead(ttl, selectA) ? 1 : 0) | (TtlRead(ttl, selectB) ? 2 : 0);
            SetTtl(ttl, outputPin, !TtlRead(ttl, enablePin) && TtlRead(ttl, dataPins[index]));
        }

        private void AdvanceTtlSequential(TtlPackageComponent ttl)
        {
            if (ttl.PartNumber == "7474")
            {
                AdvanceTtlDff(ttl, 1, 2, 3, 4, 5, 6, ClockTriggerType.RisingEdge);
                AdvanceTtlDff(ttl, 13, 12, 11, 10, 9, 8, ClockTriggerType.RisingEdge);
            }
            else if (ttl.PartNumber == "7476")
            {
                AdvanceTtlJk(ttl, 3, 4, 16, 1, 2, 15, 14);
                AdvanceTtlJk(ttl, 8, 10, 13, 6, 7, 12, 11);
            }
            else if (ttl.PartNumber == "74175")
            {
                AdvanceTtlRegister4(ttl, 15, 14, new[] { 3, 4, 7, 11 }, new[] { 1, 6, 9, 13 }, new[] { 2, 5, 10, 12 });
            }
            else if (ttl.PartNumber == "74273")
            {
                AdvanceTtlRegister8(ttl, 1, 11, new[] { 3, 4, 7, 8, 13, 14, 17, 18 }, new[] { 2, 5, 6, 9, 12, 15, 16, 19 });
            }
            else if (ttl.PartNumber == "74377")
            {
                AdvanceTtlRegister8Enabled(ttl, 1, 11, new[] { 3, 4, 7, 8, 13, 14, 17, 18 }, new[] { 2, 5, 6, 9, 12, 15, 16, 19 });
            }
            else if (ttl.PartNumber == "74165")
            {
                AdvanceTtlShiftRegister(ttl, 2, 1, 15, 10, new[] { 11, 12, 13, 14, 3, 4, 5, 6 }, 9, 7, parallelLoadActiveLow: true, clearPin: 0, asynchronousLoad: true);
            }
            else if (ttl.PartNumber == "74166")
            {
                AdvanceTtlShiftRegister(ttl, 7, 15, 6, 1, new[] { 2, 3, 4, 5, 10, 11, 12, 14 }, 13, -1, parallelLoadActiveLow: true, clearPin: 9);
            }
            else if (ttl.PartNumber == "74191") AdvanceTtl191(ttl);
            else if (ttl.PartNumber == "74194") AdvanceTtl194(ttl);
            else if (ttl.PartNumber == "74299") AdvanceTtl299(ttl);
            else if (ttl.PartNumber == "74670") AdvanceTtl74670(ttl);
            else if (ttl.PartNumber is "74161" or "74163")
            {
                AdvanceTtlCounter4(ttl);
            }
        }

        private void AdvanceTtlJk(TtlPackageComponent ttl, int clearPin, int jPin, int kPin, int clockPin, int presetPin, int qPin, int qBarPin)
        {
            string key = $"_ttlQ:{qPin}";
            bool q = Bool(ttl, key, false);
            if (!TtlRead(ttl, clearPin) && !TtlRead(ttl, presetPin))
            {
                SetHighZ(ttl, $"pin{qPin}");
                SetHighZ(ttl, $"pin{qBarPin}");
                return;
            }
            if (!TtlRead(ttl, clearPin)) q = false;
            else if (!TtlRead(ttl, presetPin)) q = true;
            else if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.FallingEdge) > 0)
            {
                bool j = TtlRead(ttl, jPin), k = TtlRead(ttl, kPin);
                if (j && k) q = !q;
                else if (j) q = true;
                else if (k) q = false;
            }
            SetBool(ttl, key, q);
            SetTtl(ttl, qPin, q);
            SetTtl(ttl, qBarPin, !q);
        }

        private void AdvanceTtlRegister4(TtlPackageComponent ttl, int clearPin, int clockPin, int[] dataPins, int[] qPins, int[] qBarPins)
        {
            long value = (long)Number(ttl, "_ttlRegister", 0) & 0xF;
            if (!TtlRead(ttl, clearPin)) value = 0;
            else if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge) > 0)
            {
                value = 0;
                for (int bit = 0; bit < 4; bit++) if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            }
            SetNumber(ttl, "_ttlRegister", value);
            for (int bit = 0; bit < 4; bit++) { SetTtl(ttl, qPins[bit], ((value >> bit) & 1) != 0); SetTtl(ttl, qBarPins[bit], ((value >> bit) & 1) == 0); }
        }

        private void AdvanceTtlRegister8(TtlPackageComponent ttl, int clearPin, int clockPin, int[] dataPins, int[] qPins)
        {
            long value = (long)Number(ttl, "_ttlRegister", 0) & 0xFF;
            if (!TtlRead(ttl, clearPin)) value = 0;
            else if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge) > 0)
            {
                value = 0;
                for (int bit = 0; bit < 8; bit++) if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            }
            SetNumber(ttl, "_ttlRegister", value);
            for (int bit = 0; bit < 8; bit++) SetTtl(ttl, qPins[bit], ((value >> bit) & 1) != 0);
        }

        private void AdvanceTtlRegister8Enabled(TtlPackageComponent ttl, int enablePin, int clockPin, int[] dataPins, int[] qPins)
        {
            long value = (long)Number(ttl, "_ttlRegister", 0) & 0xFF;
            if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge) > 0 && !TtlRead(ttl, enablePin))
            {
                value = 0;
                for (int bit = 0; bit < 8; bit++) if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            }
            SetNumber(ttl, "_ttlRegister", value);
            for (int bit = 0; bit < 8; bit++) SetTtl(ttl, qPins[bit], ((value >> bit) & 1) != 0);
        }

        private void AdvanceTtlShiftRegister(TtlPackageComponent ttl, int clockPin, int loadPin, int inhibitPin, int serialPin, int[] dataPins, int outputPin, int invertedOutputPin, bool parallelLoadActiveLow, int clearPin, bool asynchronousLoad = false)
        {
            long value = (long)Number(ttl, "_ttlShift", 0) & 0xFF;
            bool clear = clearPin > 0 && !TtlRead(ttl, clearPin);
            int clockEdges = ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge);
            bool load = TtlRead(ttl, loadPin) == !parallelLoadActiveLow;
            if (clear) value = 0;
            else if (asynchronousLoad && load) value = ReadTtlParallelData(ttl, dataPins);
            else if (clockEdges > 0 && (inhibitPin <= 0 || !TtlRead(ttl, inhibitPin)))
            {
                if (load) value = ReadTtlParallelData(ttl, dataPins);
                else value = ((value << 1) | (TtlRead(ttl, serialPin) ? 1L : 0L)) & 0xFF;
            }
            SetNumber(ttl, "_ttlShift", value);
            SetTtl(ttl, outputPin, ((value >> 7) & 1) != 0);
            if (invertedOutputPin > 0) SetTtl(ttl, invertedOutputPin, ((value >> 7) & 1) == 0);
        }

        private long ReadTtlParallelData(TtlPackageComponent ttl, int[] dataPins)
        {
            long value = 0;
            for (int bit = 0; bit < dataPins.Length; bit++)
                if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            return value;
        }

        private void AdvanceTtlDff(TtlPackageComponent ttl, int clearPin, int dataPin, int clockPin, int presetPin, int qPin, int qBarPin, ClockTriggerType trigger)
        {
            string key = $"_ttlQ:{qPin}";
            bool q = Bool(ttl, key, false);
            if (!TtlRead(ttl, clearPin) && !TtlRead(ttl, presetPin))
            {
                SetHighZ(ttl, $"pin{qPin}"); SetHighZ(ttl, $"pin{qBarPin}");
                return;
            }
            if (!TtlRead(ttl, clearPin)) q = false;
            else if (!TtlRead(ttl, presetPin)) q = true;
            else if (ClockEdges(ttl, $"pin{clockPin}", trigger) > 0) q = TtlRead(ttl, dataPin);
            SetBool(ttl, key, q);
            SetTtl(ttl, qPin, q);
            SetTtl(ttl, qBarPin, !q);
        }

        private void AdvanceTtlCounter4(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlCounter", 0) & 0xF;
            bool clear = !TtlRead(ttl, 1);
            int clockPin = 2;
            int edges = ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge);
            if (ttl.PartNumber == "74161" && clear) value = 0;
            else if (edges > 0)
            {
                if (ttl.PartNumber == "74163" && clear) value = 0;
                else if (!TtlRead(ttl, 9)) value = (TtlRead(ttl, 3) ? 1 : 0) | (TtlRead(ttl, 4) ? 2 : 0) | (TtlRead(ttl, 5) ? 4 : 0) | (TtlRead(ttl, 6) ? 8 : 0);
                else if (TtlRead(ttl, 7) && TtlRead(ttl, 9)) value = (value + 1) & 0xF;
            }
            SetNumber(ttl, "_ttlCounter", value);
            SetTtl(ttl, 14, (value & 1) != 0);
            SetTtl(ttl, 13, (value & 2) != 0);
            SetTtl(ttl, 12, (value & 4) != 0);
            SetTtl(ttl, 11, (value & 8) != 0);
            SetTtl(ttl, 15, value == 15 && TtlRead(ttl, 10));
        }

        private void SetTtl(TtlPackageComponent ttl, int pin, bool value) => Set(ttl, $"pin{pin}", value, 5, 0);

        private void EvaluatePla(PLAComponent pla)
        {
            int inputWord = 0;
            for (int i = 0; i < pla.InputCount; i++) if (Read(pla, $"in{i}")) inputWord |= 1 << i;
            long word = 0;
            bool matched = false;
            foreach (var rawRow in pla.TruthTable.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries).Take(pla.AndTerms))
            {
                string[] sides = rawRow.Split(new[] { "->", "=", ":" }, 2, StringSplitOptions.TrimEntries);
                if (sides.Length != 2)
                {
                    sides = rawRow.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
                if (sides.Length != 2) continue;
                string pattern = new string(sides[0].Where(ch => !char.IsWhiteSpace(ch) && ch != ',').ToArray());
                if (pattern.Length != pla.InputCount || !MatchesPlaPattern(pattern, inputWord)) continue;
                word = ParsePlaOutput(sides[1]);
                matched = true;
                break;
            }

            // Backward-compatible truth-vector format: one decimal/hex output word per input index.
            if (!matched)
            {
                string[] entries = pla.TruthTable.Split(new[] { ',', ';', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (inputWord < entries.Length) word = ParseWord(entries[inputWord]);
            }
            for (int i = 0; i < pla.OutputCount; i++) Set(pla, $"out{i}", (word & (1L << i)) != 0, 5, 0);
        }

        private static bool MatchesPlaPattern(string pattern, int inputWord)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                char expected = char.ToLowerInvariant(pattern[i]);
                if (expected is 'x' or '-') continue;
                int bit = pattern.Length - 1 - i;
                bool actual = ((inputWord >> bit) & 1) != 0;
                if ((expected == '1') != actual) return false;
                if (expected is not ('0' or '1')) return false;
            }
            return true;
        }

        private static long ParseWord(string text)
        {
            string value = text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : 0;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexValue) ? hexValue : 0;
        }

        private static long ParsePlaOutput(string text)
        {
            string value = new string(text.Trim().Where(ch => !char.IsWhiteSpace(ch) && ch != ',').ToArray());
            if (value.Length > 0 && value.All(ch => ch is '0' or '1') && value.Length <= 62)
                return Convert.ToInt64(value, 2);
            return ParseWord(value);
        }

        private static long ReadRomWord(ROMComponent rom, int address)
        {
            if (rom.MemoryData.TryGetValue(address, out var value)) return value;
            string contents = rom.ContentsHex.Trim();
            if (contents.Length == 0) return 0;
            string[] tokens = contents.Split(new[] { ',', ';', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 1 && tokens[0].Length > (rom.DataBitWidth + 3) / 4)
            {
                string compact = tokens[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? tokens[0][2..] : tokens[0];
                int digits = Math.Max(1, (rom.DataBitWidth + 3) / 4);
                tokens = Enumerable.Range(0, (compact.Length + digits - 1) / digits)
                    .Select(index => compact.Substring(index * digits, Math.Min(digits, compact.Length - index * digits)))
                    .ToArray();
            }
            return address >= 0 && address < tokens.Length ? ParseHexWord(tokens[address]) & Mask(rom.DataBitWidth) : 0;
        }

        private static long ParseHexWord(string text)
        {
            string value = text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
            return long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var word) ? word : 0;
        }

        private static string BusPin(ComponentNode component, string prefix, int bit, int width, string suffix = "")
        {
            if (width == 1 && component.GetPin(prefix) != null) return prefix;
            string indexed = suffix.Length == 0 ? $"{prefix}{bit}" : $"{prefix}{suffix}{bit}";
            return component.GetPin(indexed) != null ? indexed : $"{prefix}{bit}";
        }
    }

    private static long Mask(int width) => width >= 63 ? long.MaxValue : (1L << Math.Clamp(width, 1, 62)) - 1;
    private static int ParseTtlPin(string pinId) => pinId.StartsWith("pin", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(pinId.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pinNumber) ? pinNumber : -1;
    private static void Set(ComponentNode component, string pinId, bool value, double highVoltage, double lowVoltage)
    {
        component.CustomProperties[$"HighZ:{pinId}"] = false;
        component.CustomProperties[$"Output:{pinId}"] = value;
        component.CustomProperties[$"OutputVoltage:{pinId}"] = value ? highVoltage : lowVoltage;
    }
    private static void SetHighZ(ComponentNode component, string pinId) => component.CustomProperties[$"HighZ:{pinId}"] = true;
    private static bool Bool(ComponentNode component, string key, bool fallback) => component.CustomProperties.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;
    private static double Number(ComponentNode component, string key, double fallback) => component.CustomProperties.TryGetValue(key, out var value) && double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static void SetBool(ComponentNode component, string key, bool value) => component.CustomProperties[key] = value;
    private static void SetNumber(ComponentNode component, string key, double value) => component.CustomProperties[key] = value;
    private static long CountBoundaryCrossings(double phase, double total, double boundary) => Math.Max(0, (long)Math.Floor(total - boundary) - (long)Math.Floor(phase - boundary));
}
