using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;
using EESimulator.Commands;
using EESimulator.Engine.Simulation;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using EEE.Services;
using Xunit;
using Xunit.Abstractions;

namespace EESimulator.Tests;

public sealed class ComplexLinkedCircuitTests
{
    private readonly ITestOutputHelper _output;

    public ComplexLinkedCircuitTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static T Add<T>(CircuitNode circuit, string name, Action<T>? configure = null)
        where T : ComponentNode, new()
    {
        var component = new T { Name = name, Reference = name };
        configure?.Invoke(component);
        circuit.AddChild(component);
        return component;
    }

    private static void Wire(
        CircuitNode circuit,
        ComponentNode from,
        string fromPin,
        ComponentNode to,
        string toPin)
    {
        circuit.AddChild(new ConnectionNode(from.Id, fromPin, to.Id, toPin));
    }

    private static LogicInputComponent Drive(
        CircuitNode circuit,
        ComponentNode component,
        string pin,
        bool state,
        string name)
    {
        var input = new LogicInputComponent(name) { State = state };
        circuit.AddChild(input);
        Wire(circuit, input, "out", component, pin);
        return input;
    }

    private static void DriveBus(
        CircuitNode circuit,
        ComponentNode component,
        string prefix,
        long value,
        int width,
        string name)
    {
        for (int bit = 0; bit < width; bit++)
            Drive(circuit, component, $"{prefix}{bit}", ((value >> bit) & 1) != 0, $"{name}_{bit}");
    }

    private static long ReadBus(ComponentNode component, string prefix, int width)
    {
        long value = 0;
        for (int bit = 0; bit < width; bit++)
        {
            if (DigitalLogicSimulator.ReadOutputState(component, $"{prefix}{bit}"))
                value |= 1L << bit;
        }

        return value;
    }

    [Fact]
    public void One_linked_circuit_passes_analog_digital_memory_and_TTL_checkpoints()
    {
        var circuit = new CircuitNode();
        var checkpoints = new List<string>();

        void Check(string name, bool passed, string details)
        {
            Assert.True(passed, $"{name}: {details}");
            checkpoints.Add(name);
        }

        void Near(string name, double expected, double actual, double tolerance)
        {
            Check(name, Math.Abs(expected - actual) <= tolerance,
                $"expected {expected:G6} ± {tolerance:G3}, got {actual:G6}");
        }

        var ground = Add<GroundNode>(circuit, "GND");

        // Analog paths share a common reference but each has an independent source/load.
        var dividerSource = Add<VoltageSourceComponent>(circuit, "V_DIV", x => x.Voltage = 10);
        var upper = Add<ResistorComponent>(circuit, "R_DIV_UPPER", x => x.Resistance = 1000);
        var lower = Add<ResistorComponent>(circuit, "R_DIV_LOWER", x => x.Resistance = 1000);
        Wire(circuit, dividerSource, "positive", upper, "pin1");
        Wire(circuit, upper, "pin2", lower, "pin1");
        Wire(circuit, lower, "pin2", ground, "gnd");
        Wire(circuit, dividerSource, "negative", ground, "gnd");

        var currentSource = Add<CurrentSourceComponent>(circuit, "I_LOAD", x => x.Current = 0.001);
        var currentLoad = Add<ResistorComponent>(circuit, "R_CURRENT_LOAD", x => x.Resistance = 1000);
        Wire(circuit, currentSource, "out", currentLoad, "pin1");
        Wire(circuit, currentLoad, "pin2", ground, "gnd");
        Wire(circuit, currentSource, "in", ground, "gnd");

        var controlSource = Add<VoltageSourceComponent>(circuit, "V_CONTROL", x => x.Voltage = 0.5);
        var vcvs = Add<VCVSComponent>(circuit, "E_GAIN", x => x.VoltageGain = 10);
        var vcvsLoad = Add<ResistorComponent>(circuit, "R_VCVS_LOAD", x => x.Resistance = 1000);
        Wire(circuit, controlSource, "positive", vcvs, "ctrl_pos");
        Wire(circuit, controlSource, "negative", vcvs, "ctrl_neg");
        Wire(circuit, controlSource, "negative", ground, "gnd");
        Wire(circuit, vcvs, "out_pos", vcvsLoad, "pin1");
        Wire(circuit, vcvsLoad, "pin2", ground, "gnd");
        Wire(circuit, vcvs, "out_neg", ground, "gnd");

        var bulbSource = Add<VoltageSourceComponent>(circuit, "V_BULB", x => x.Voltage = 6);
        var bulb = Add<BulbComponent>(circuit, "BULB", x => x.Resistance = 50);
        Wire(circuit, bulbSource, "positive", bulb, "pin1");
        Wire(circuit, bulb, "pin2", ground, "gnd");
        Wire(circuit, bulbSource, "negative", ground, "gnd");

        var diodeSource = Add<VoltageSourceComponent>(circuit, "V_DIODE", x => x.Voltage = 3);
        var diodeResistor = Add<ResistorComponent>(circuit, "R_DIODE", x => x.Resistance = 1000);
        var diode = Add<DiodeComponent>(circuit, "D_FORWARD");
        Wire(circuit, diodeSource, "positive", diodeResistor, "pin1");
        Wire(circuit, diodeResistor, "pin2", diode, "anode");
        Wire(circuit, diode, "cathode", ground, "gnd");
        Wire(circuit, diodeSource, "negative", ground, "gnd");

        var ccvsSource = Add<VoltageSourceComponent>(circuit, "V_CCVS", x => x.Voltage = 1);
        var ccvsControlResistor = Add<ResistorComponent>(circuit, "R_CCVS_CONTROL", x => x.Resistance = 1000);
        var ccvs = Add<CCVSComponent>(circuit, "H_CONTROLLED", x => x.Transresistance = 1000);
        var ccvsLoad = Add<ResistorComponent>(circuit, "R_CCVS_LOAD", x => x.Resistance = 1000);
        Wire(circuit, ccvsSource, "positive", ccvsControlResistor, "pin1");
        Wire(circuit, ccvsControlResistor, "pin2", ccvs, "ctrl_pos");
        Wire(circuit, ccvs, "ctrl_neg", ground, "gnd");
        Wire(circuit, ccvsSource, "negative", ground, "gnd");
        Wire(circuit, ccvs, "out_pos", ccvsLoad, "pin1");
        Wire(circuit, ccvsLoad, "pin2", ground, "gnd");
        Wire(circuit, ccvs, "out_neg", ground, "gnd");

        var cccsSource = Add<VoltageSourceComponent>(circuit, "V_CCCS", x => x.Voltage = 1);
        var cccsControlResistor = Add<ResistorComponent>(circuit, "R_CCCS_CONTROL", x => x.Resistance = 1000);
        var cccs = Add<CCCSComponent>(circuit, "F_CONTROLLED", x => x.CurrentGain = 2);
        var cccsLoad = Add<ResistorComponent>(circuit, "R_CCCS_LOAD", x => x.Resistance = 1000);
        Wire(circuit, cccsSource, "positive", cccsControlResistor, "pin1");
        Wire(circuit, cccsControlResistor, "pin2", cccs, "ctrl_pos");
        Wire(circuit, cccs, "ctrl_neg", ground, "gnd");
        Wire(circuit, cccsSource, "negative", ground, "gnd");
        Wire(circuit, cccs, "out_pos", cccsLoad, "pin1");
        Wire(circuit, cccsLoad, "pin2", ground, "gnd");
        Wire(circuit, cccs, "out_neg", ground, "gnd");

        var switchSupply = Add<VoltageSourceComponent>(circuit, "V_SWITCH", x => x.Voltage = 5);
        var voltageSwitch = Add<VoltageSwitchComponent>(circuit, "S_CONTROLLED", x =>
        {
            x.Threshold = 1;
            x.Hysteresis = 0.1;
            x.ResistanceOn = 1;
            x.ResistanceOff = 1e6;
        });
        var switchLoad = Add<ResistorComponent>(circuit, "R_SWITCH_LOAD", x => x.Resistance = 1000);
        Wire(circuit, switchSupply, "positive", voltageSwitch, "sw_pos");
        Wire(circuit, switchSupply, "positive", voltageSwitch, "ctrl_pos");
        Wire(circuit, voltageSwitch, "ctrl_neg", ground, "gnd");
        Wire(circuit, voltageSwitch, "sw_neg", switchLoad, "pin1");
        Wire(circuit, switchLoad, "pin2", ground, "gnd");
        Wire(circuit, switchSupply, "negative", ground, "gnd");

        // A digital datapath uses linked component outputs rather than recalculating
        // the expected answers inside the test.
        var inputA = Add<LogicInputComponent>(circuit, "LOGIC_A", x => x.State = true);
        var inputB = Add<LogicInputComponent>(circuit, "LOGIC_B", x => x.State = false);
        var andGate = Add<AndGateComponent>(circuit, "G_AND");
        Wire(circuit, inputA, "out", andGate, "in1");
        Wire(circuit, inputA, "out", andGate, "in2");

        var orGate = Add<OrGateComponent>(circuit, "G_OR");
        Wire(circuit, andGate, "out", orGate, "in1");
        Wire(circuit, inputB, "out", orGate, "in2");

        var notGate = Add<NotGateComponent>(circuit, "G_NOT");
        Wire(circuit, inputB, "out", notGate, "in1");

        var nandGate = Add<NandGateComponent>(circuit, "G_NAND");
        Wire(circuit, inputA, "out", nandGate, "in1");
        Wire(circuit, inputA, "out", nandGate, "in2");

        var norGate = Add<NorGateComponent>(circuit, "G_NOR");
        Wire(circuit, inputB, "out", norGate, "in1");
        Wire(circuit, inputB, "out", norGate, "in2");

        var xorGate = Add<XorGateComponent>(circuit, "G_XOR");
        Wire(circuit, inputA, "out", xorGate, "in1");
        Wire(circuit, inputB, "out", xorGate, "in2");

        var xnorGate = Add<XnorGateComponent>(circuit, "G_XNOR");
        Wire(circuit, inputA, "out", xnorGate, "in1");
        Wire(circuit, inputB, "out", xnorGate, "in2");

        var genericGate = Add<LogicGateComponent>(circuit, "G_GENERIC", x => x.GateType = LogicGateType.AND);
        Wire(circuit, andGate, "out", genericGate, "in1");
        Wire(circuit, inputB, "out", genericGate, "in2");

        var buffer = Add<BufferComponent>(circuit, "BUF", x => x.BitWidth = 1);
        Wire(circuit, andGate, "out", buffer, "in");
        var controlledBuffer = Add<ControlledBufferComponent>(circuit, "TRI_BUF", x => x.BitWidth = 1);
        Wire(circuit, buffer, "out", controlledBuffer, "in");
        Wire(circuit, inputA, "out", controlledBuffer, "ctrl");

        var evenParity = Add<EvenParityGateComponent>(circuit, "EVEN_PARITY", x => x.NumberOfInputs = 2);
        Wire(circuit, inputA, "out", evenParity, "in1");
        Wire(circuit, inputB, "out", evenParity, "in2");
        var oddParity = Add<OddParityGateComponent>(circuit, "ODD_PARITY", x => x.NumberOfInputs = 2);
        Wire(circuit, inputA, "out", oddParity, "in1");
        Wire(circuit, inputB, "out", oddParity, "in2");

        var mux = Add<MultiplexerComponent>(circuit, "MUX", x => { x.BitWidth = 1; x.SelectBits = 1; });
        Wire(circuit, andGate, "out", mux, "in0");
        Wire(circuit, nandGate, "out", mux, "in1");
        Wire(circuit, inputB, "out", mux, "sel");
        var demux = Add<DemultiplexerComponent>(circuit, "DEMUX", x => { x.BitWidth = 1; x.SelectBits = 1; });
        Wire(circuit, mux, "out", demux, "in");
        Wire(circuit, inputA, "out", demux, "sel");
        var decoder = Add<DecoderComponent>(circuit, "DECODER", x => { x.SelectBits = 2; x.HasEnable = false; });
        Wire(circuit, inputB, "out", decoder, "sel0");
        Wire(circuit, inputA, "out", decoder, "sel1");

        var encoder = Add<PriorityEncoderComponent>(circuit, "ENCODER", x => x.SelectBits = 3);
        DriveBus(circuit, encoder, "in", 0x20, 8, "ENCODER_DATA");
        Drive(circuit, encoder, "en", true, "ENCODER_ENABLE");
        var selector = Add<BitSelectorComponent>(circuit, "BIT_SELECTOR", x =>
        {
            x.DataBusWidth = 8;
            x.SelectWidth = 3;
        });
        DriveBus(circuit, selector, "data", 0x08, 8, "SELECTOR_DATA");
        DriveBus(circuit, selector, "sel", 3, 3, "SELECTOR_INDEX");

        var adder = Add<AdderComponent>(circuit, "ADDER", x => x.BitWidth = 8);
        DriveBus(circuit, adder, "A", 15, 8, "ADDER_A");
        DriveBus(circuit, adder, "B", 10, 8, "ADDER_B");
        var subtractor = Add<SubtractorComponent>(circuit, "SUBTRACTOR", x => x.BitWidth = 8);
        for (int bit = 0; bit < 8; bit++)
            Wire(circuit, adder, $"Y{bit}", subtractor, $"A{bit}");
        DriveBus(circuit, subtractor, "B", 7, 8, "SUBTRACTOR_B");

        var multiplier = Add<MultiplierComponent>(circuit, "MULTIPLIER", x => x.BitWidth = 8);
        DriveBus(circuit, multiplier, "A", 6, 8, "MULTIPLIER_A");
        DriveBus(circuit, multiplier, "B", 7, 8, "MULTIPLIER_B");
        var divider = Add<DividerComponent>(circuit, "DIVIDER", x => x.BitWidth = 8);
        DriveBus(circuit, divider, "A", 20, 8, "DIVIDER_A");
        DriveBus(circuit, divider, "B", 3, 8, "DIVIDER_B");
        var comparator = Add<ComparatorComponent>(circuit, "COMPARATOR", x => x.BitWidth = 8);
        for (int bit = 0; bit < 8; bit++)
        {
            Wire(circuit, subtractor, $"Y{bit}", comparator, $"A{bit}");
            Wire(circuit, divider, $"Y{bit}", comparator, $"B{bit}");
        }

        var negator = Add<NegatorComponent>(circuit, "NEGATOR", x => x.BitWidth = 8);
        DriveBus(circuit, negator, "A", 5, 8, "NEGATOR_A");
        var absolute = Add<AbsoluteValueComponent>(circuit, "ABSOLUTE", x => x.BitWidth = 8);
        for (int bit = 0; bit < 8; bit++)
            Wire(circuit, negator, $"Y{bit}", absolute, $"A{bit}");

        var shifter = Add<ShifterComponent>(circuit, "SHIFTER", x =>
        {
            x.BitWidth = 8;
            x.Mode = "LogicalRight";
        });
        DriveBus(circuit, shifter, "A", 0x08, 8, "SHIFTER_A");
        DriveBus(circuit, shifter, "S", 2, shifter.ShiftWidth, "SHIFTER_AMOUNT");
        var bitAdder = Add<BitAdderComponent>(circuit, "BIT_ADDER", x => x.BitWidth = 8);
        DriveBus(circuit, bitAdder, "A", 0xF0, 8, "BIT_ADDER_A");
        var bitFinder = Add<BitFinderComponent>(circuit, "BIT_FINDER", x => x.BitWidth = 8);
        DriveBus(circuit, bitFinder, "A", 0x28, 8, "BIT_FINDER_A");
        var squareRoot = Add<SquareRootComponent>(circuit, "SQUARE_ROOT", x => x.BitWidth = 8);
        DriveBus(circuit, squareRoot, "A", 49, 8, "SQUARE_ROOT_A");
        var minMax = Add<MinMaxComponent>(circuit, "MIN_MAX", x =>
        {
            x.BitWidth = 8;
            x.Mode = "Min";
        });
        DriveBus(circuit, minMax, "A", 3, 8, "MIN_MAX_A");
        DriveBus(circuit, minMax, "B", 9, 8, "MIN_MAX_B");
        var exponentiator = Add<ExponentiatorComponent>(circuit, "EXPONENTIATOR", x => x.BitWidth = 8);
        DriveBus(circuit, exponentiator, "A", 2, 8, "EXPONENTIATOR_A");
        DriveBus(circuit, exponentiator, "B", 5, 8, "EXPONENTIATOR_B");

        var clock = Add<ClockComponent>(circuit, "CLOCK", x =>
        {
            x.Frequency = 1;
            x.DutyCycle = 0.5;
        });
        var dff = Add<DFlipFlopComponent>(circuit, "DFF");
        Wire(circuit, andGate, "out", dff, "D");
        Wire(circuit, clock, "out", dff, "CLK");
        var jkff = Add<JKFlipFlopComponent>(circuit, "JKFF");
        Wire(circuit, andGate, "out", jkff, "J");
        Wire(circuit, nandGate, "out", jkff, "K");
        Wire(circuit, clock, "out", jkff, "CLK");
        var srff = Add<SRFlipFlopComponent>(circuit, "SRFF");
        Wire(circuit, andGate, "out", srff, "S");
        Wire(circuit, nandGate, "out", srff, "R");
        Wire(circuit, clock, "out", srff, "CLK");
        var tff = Add<TFlipFlopComponent>(circuit, "TFF");
        Wire(circuit, andGate, "out", tff, "T");
        Wire(circuit, clock, "out", tff, "CLK");

        var register = Add<RegisterComponent>(circuit, "REGISTER", x => x.BitWidth = 8);
        for (int bit = 0; bit < 8; bit++)
            Wire(circuit, exponentiator, $"Y{bit}", register, $"D{bit}");
        Drive(circuit, register, "en", true, "REGISTER_ENABLE");
        Wire(circuit, clock, "out", register, "CLK");

        var counter = Add<CounterComponent>(circuit, "COUNTER", x =>
        {
            x.BitWidth = 8;
            x.MaxValue = 255;
        });
        Drive(circuit, counter, "en", true, "COUNTER_ENABLE");
        Drive(circuit, counter, "up_down", true, "COUNTER_DIRECTION");
        Wire(circuit, clock, "out", counter, "CLK");

        var shiftRegister = Add<ShiftRegisterComponent>(circuit, "SHIFT_REGISTER", x => x.Length = 8);
        Drive(circuit, shiftRegister, "sin", true, "SHIFT_REGISTER_INPUT");
        Drive(circuit, shiftRegister, "en", true, "SHIFT_REGISTER_ENABLE");
        Wire(circuit, clock, "out", shiftRegister, "CLK");

        var ram = Add<RAMComponent>(circuit, "RAM", x =>
        {
            x.AddressBitWidth = 5;
            x.DataBitWidth = 8;
        });
        DriveBus(circuit, ram, "addr", 0x10, ram.AddressBitWidth, "RAM_ADDRESS");
        DriveBus(circuit, ram, "din", 0x5A, ram.DataBitWidth, "RAM_DATA");
        Drive(circuit, ram, "we", true, "RAM_WRITE_ENABLE");
        Drive(circuit, ram, "oe", true, "RAM_OUTPUT_ENABLE");
        Wire(circuit, clock, "out", ram, "clk");

        var rom = Add<ROMComponent>(circuit, "ROM", x =>
        {
            x.AddressBitWidth = 2;
            x.DataBitWidth = 8;
            x.ContentsHex = "A5F0";
        });
        DriveBus(circuit, rom, "addr", 0, rom.AddressBitWidth, "ROM_ADDRESS");

        var ledBar = Add<LedBarComponent>(circuit, "LED_BAR", x => x.LedCount = 8);
        var sevenSegment = Add<SevenSegmentComponent>(circuit, "SEVEN_SEGMENT");
        for (int bit = 0; bit < 8; bit++)
        {
            Wire(circuit, register, $"Q{bit}", ledBar, $"D{bit}");
            Wire(circuit, register, $"Q{bit}", sevenSegment, $"seg{bit}");
        }

        var ttlNand = new TtlPackageComponent("7400") { Name = "TTL_7400", Reference = "TTL_7400" };
        circuit.AddChild(ttlNand);
        Drive(circuit, ttlNand, "pin1", true, "TTL_A");
        Drive(circuit, ttlNand, "pin2", true, "TTL_B");

        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());
        Assert.True(simulation.Success, $"Combined-circuit SPICE solve failed: {simulation.Message}");

        Near("analog divider 10V/2", 5, lower.LivePinVoltages["pin1"], 0.02);
        Near("1mA current source through 1kΩ", 1, currentLoad.LivePinVoltages["pin1"], 0.02);
        Near("VCVS 0.5V × 10", 5, vcvsLoad.LivePinVoltages["pin1"], 0.02);
        Near("6V / 50Ω bulb current", 0.12, Math.Abs(bulb.LiveCurrent), 0.002);
        Check("forward diode conducts", Math.Abs(diodeResistor.LiveCurrent) > 0.001,
            $"expected measurable current, got {diodeResistor.LiveCurrent:G6} A");
        Check("forward diode drop", diode.LivePinVoltages.TryGetValue("anode", out var anode) &&
              diode.LivePinVoltages.TryGetValue("cathode", out var cathode) &&
              anode - cathode is >= 0.5 and <= 1.0,
            $"expected 0.5–1.0V forward drop, got {diode.LiveVoltage:G6} V");
        Near("CCVS transresistance output", 1, ccvsLoad.LivePinVoltages["pin1"], 0.03);
        Near("CCCS gain output", -2, cccsLoad.LivePinVoltages["pin1"], 0.03);
        Near("voltage-controlled switch ON current", 5.0 / 1001, Math.Abs(switchLoad.LiveCurrent), 0.0001);

        DigitalLogicSimulator.EvaluateCombinational(circuit);
        Check("AND(1,1)", DigitalLogicSimulator.ReadOutputState(andGate, "out"), "output should be high");
        Check("OR(1,0)", DigitalLogicSimulator.ReadOutputState(orGate, "out"), "output should be high");
        Check("NOT(0)", DigitalLogicSimulator.ReadOutputState(notGate, "out"), "output should be high");
        Check("NAND(1,1)", !DigitalLogicSimulator.ReadOutputState(nandGate, "out"), "output should be low");
        Check("NOR(0,0)", DigitalLogicSimulator.ReadOutputState(norGate, "out"), "output should be high");
        Check("XOR(1,0)", DigitalLogicSimulator.ReadOutputState(xorGate, "out"), "output should be high");
        Check("XNOR(1,0)", !DigitalLogicSimulator.ReadOutputState(xnorGate, "out"), "output should be low");
        Check("generic AND(1,0)", !DigitalLogicSimulator.ReadOutputState(genericGate, "out"), "output should be low");
        Check("buffer pass-through", DigitalLogicSimulator.ReadOutputState(buffer, "out"), "output should be high");
        Check("controlled buffer enabled", DigitalLogicSimulator.ReadOutputState(controlledBuffer, "out"), "output should be high");
        Check("even parity of 1,0", !DigitalLogicSimulator.ReadOutputState(evenParity, "out"), "output should be low");
        Check("odd parity of 1,0", DigitalLogicSimulator.ReadOutputState(oddParity, "out"), "output should be high");
        Check("multiplexer selects input 0", DigitalLogicSimulator.ReadOutputState(mux, "out"), "selected input should be high");
        Check("demultiplexer routes to output 1", DigitalLogicSimulator.ReadOutputState(demux, "out1"), "output 1 should be high");
        Check("decoder selects address 2", DigitalLogicSimulator.ReadOutputState(decoder, "out2"), "output 2 should be high");
        Check("priority encoder detects an active input", DigitalLogicSimulator.ReadOutputState(encoder, "any_active"), "any_active should be high");
        Check("bit selector selects bit 3", DigitalLogicSimulator.ReadOutputState(selector, "out"), "selected bit should be high");
        Check("adder output", ReadBus(adder, "Y", 8) == 25, $"expected 25, got {ReadBus(adder, "Y", 8)}");
        Check("linked subtractor output", ReadBus(subtractor, "Y", 8) == 18, $"expected 18, got {ReadBus(subtractor, "Y", 8)}");
        Check("multiplier output", ReadBus(multiplier, "Y", 8) == 42, $"expected 42, got {ReadBus(multiplier, "Y", 8)}");
        Check("divider output", ReadBus(divider, "Y", 8) == 6, $"expected 6, got {ReadBus(divider, "Y", 8)}");
        Check("linked comparator", DigitalLogicSimulator.ReadOutputState(comparator, "gt"), "18 should be greater than 6");
        Check("two's-complement negator", ReadBus(negator, "Y", 8) == 0xFB, $"expected 0xFB, got 0x{ReadBus(negator, "Y", 8):X}");
        Check("linked absolute value", ReadBus(absolute, "Y", 8) == 5, $"expected 5, got {ReadBus(absolute, "Y", 8)}");
        Check("logical right shifter", ReadBus(shifter, "Y", 8) == 2, $"expected 2, got {ReadBus(shifter, "Y", 8)}");
        Check("population count", ReadBus(bitAdder, "Y", 8) == 4, $"expected 4, got {ReadBus(bitAdder, "Y", 8)}");
        Check("least-significant set bit", ReadBus(bitFinder, "Y", 8) == 3, $"expected 3, got {ReadBus(bitFinder, "Y", 8)}");
        Check("integer square root", ReadBus(squareRoot, "Y", 8) == 7, $"expected 7, got {ReadBus(squareRoot, "Y", 8)}");
        Check("minimum selector", ReadBus(minMax, "Y", 8) == 3, $"expected 3, got {ReadBus(minMax, "Y", 8)}");
        Check("exponentiator output", ReadBus(exponentiator, "Y", 8) == 32, $"expected 32, got {ReadBus(exponentiator, "Y", 8)}");

        clock.State = false;
        DigitalLogicSimulator.Advance(circuit, 0.25);
        Check("D flip-flop captures data", dff.StateQ,
            $"Q should capture 1 on the initial rising edge (D source={DigitalLogicSimulator.ReadOutputState(andGate, "out")}, " +
            $"clock={clock.State}, rising edges={clock.CustomProperties.GetValueOrDefault("_simRisingEdges", 0)})");
        Check("JK flip-flop set transition", jkff.StateQ, "Q should set for J=1, K=0");
        Check("SR flip-flop set transition", srff.StateQ, "Q should set for S=1, R=0");
        Check("T flip-flop toggle transition", tff.StateQ, "Q should toggle on the initial rising edge");
        Check("register captures linked exponentiator result", register.Value == 32, $"expected 32, got {register.Value}");
        Check("counter advances", counter.Value == 1, $"expected 1 on the first rising edge, got {counter.Value}");
        Check("shift register shifts serial input", shiftRegister.Value == 1, $"expected 1 after one shift, got {shiftRegister.Value}");
        Check("RAM writes and reads address 0x10",
            ram.MemoryData.TryGetValue(0x10, out var ramWord) && ramWord == 0x5A &&
            ReadBus(ram, "dout", ram.DataBitWidth) == 0x5A,
            $"expected stored/output word 0x5A, stored {ram.MemoryData.GetValueOrDefault(0x10):X}, " +
            $"output 0x{ReadBus(ram, "dout", ram.DataBitWidth):X}");
        Check("ROM reads address zero", ReadBus(rom, "dout", rom.DataBitWidth) == 0xA5,
            $"expected 0xA5, got 0x{ReadBus(rom, "dout", rom.DataBitWidth):X}");
        Check("registered word reaches LED bar",
            Convert.ToInt64(ledBar.CustomProperties["RenderedValue"]) == 32,
            $"expected 32, got {ledBar.CustomProperties["RenderedValue"]}");
        Check("registered word reaches seven-segment pins",
            Convert.ToInt32(sevenSegment.CustomProperties["RenderedSegments"]) == 0x20,
            $"expected 0x20, got 0x{sevenSegment.CustomProperties["RenderedSegments"]:X}");
        Check("TTL 7400 NAND output", !DigitalLogicSimulator.ReadOutputState(ttlNand, "pin3"),
            "pin3 should be low for inputs 1 and 1");

        _output.WriteLine($"Passed {checkpoints.Count} checkpoints in one linked circuit:");
        foreach (var checkpoint in checkpoints)
            _output.WriteLine($"  {checkpoint}");
        Assert.True(checkpoints.Count >= 40, $"Expected at least 40 checkpoints, got {checkpoints.Count}");
    }

    [Fact]
    public void Python_script_can_configure_and_clock_RAM_with_required_enable_states()
    {
        var circuit = new CircuitNode();
        var executor = new PythonCommandExecutor(new PythonApi(new CommandManager(), circuit));
        var script = new List<string>
        {
            "eesim.add_component(\"electronics.ram\", name=\"RAM1\", row=2, col=3)",
            "eesim.set_component_property(\"RAM1\", \"AddressBitWidth\", 1)",
            "eesim.set_component_property(\"RAM1\", \"DataBitWidth\", 8)",
            "eesim.add_component(\"electronics.clock\", name=\"CLK\", row=1, col=1)",
            "eesim.set_component_property(\"CLK\", \"Frequency\", 1)",
            "eesim.set_component_property(\"CLK\", \"DutyCycle\", 0.5)",
            "eesim.set_component_property(\"CLK\", \"State\", false)",
            "eesim.add_component(\"electronics.logicinput\", name=\"WE\", row=1, col=2)",
            "eesim.set_logic(\"WE\", True)",
            "eesim.add_component(\"electronics.logicinput\", name=\"OE\", row=1, col=3)",
            "eesim.set_logic(\"OE\", True)",
            "circuit.connect(WE.out, RAM1.we)",
            "circuit.connect(OE.out, RAM1.oe)",
            "circuit.connect(CLK.out, RAM1.clk)"
        };

        const long expectedWord = 0xA5;
        for (int bit = 0; bit < 8; bit++)
        {
            script.Add($"eesim.add_component(\"electronics.logicinput\", name=\"D{bit}\", row=1, col=4)");
            script.Add($"eesim.set_logic(\"D{bit}\", {(((expectedWord >> bit) & 1) != 0 ? "True" : "False")})");
            script.Add($"circuit.connect(D{bit}.out, RAM1.din{bit})");
        }

        script.Add("eesim.evaluate_combinational()");
        script.Add("eesim.advance_simulation(0.25)");
        script.Add("eesim.evaluate_combinational()");
        script.Add("eesim.add_component(\"electrical.voltagesource\", name=\"V1\", row=3, col=1)");
        script.Add("eesim.set_component_property(\"V1\", \"Voltage\", 5)");
        script.Add("eesim.add_component(\"electrical.resistor\", name=\"R1\", row=3, col=2)");
        script.Add("eesim.set_component_property(\"R1\", \"Resistance\", 1000)");
        script.Add("eesim.add_component(\"electrical.ground\", name=\"GND\", row=4, col=1)");
        script.Add("circuit.connect(V1.positive, R1.pin1)");
        script.Add("circuit.connect(R1.pin2, GND.gnd)");
        script.Add("circuit.connect(V1.negative, GND.gnd)");
        script.Add("eesim.simulate(\"OperatingPoint\")");

        Assert.True(executor.ExecuteScript(string.Join(Environment.NewLine, script), out string message), message);

        var ram = Assert.IsType<RAMComponent>(circuit.FindChild<ComponentNode>("RAM1"));
        Assert.True(ram.MemoryData.TryGetValue(0, out long storedWord));
        Assert.Equal(expectedWord, storedWord);
        Assert.Equal(expectedWord, ReadBus(ram, "dout", ram.DataBitWidth));
        var resistor = Assert.IsAssignableFrom<ResistorComponent>(circuit.FindChild<ComponentNode>("R1"));
        Assert.InRange(resistor.LivePinVoltages["pin1"], 4.99, 5.01);
        Assert.True(executor.ExecuteScript(
            "dout_msb = eesim.read_digital_output_state(\"RAM1\", \"dout7\")",
            out string digitalReadMessage), digitalReadMessage);
        Assert.Equal(true, executor.LastResult);
        Assert.True(executor.ExecuteScript(
            "vout = eesim.read_pin_voltage(\"R1\", \"pin1\")",
            out string voltageReadMessage), voltageReadMessage);
        Assert.InRange(Assert.IsType<double>(executor.LastResult), 4.99, 5.01);

        var arithmeticCircuit = new CircuitNode();
        var arithmeticExecutor = new PythonCommandExecutor(
            new PythonApi(new CommandManager(), arithmeticCircuit));
        var arithmeticScript = new List<string>
        {
            "eesim.add_component(\"arith.adder\", name=\"ADD\", row=3, col=4)",
            "eesim.set_component_property(\"ADD\", \"BitWidth\", 4)"
        };
        const long operandA = 5;
        const long operandB = 8;
        for (int bit = 0; bit < 4; bit++)
        {
            bool a = ((operandA >> bit) & 1) != 0;
            bool b = ((operandB >> bit) & 1) != 0;
            arithmeticScript.Add($"eesim.add_component(\"electronics.logicinput\", name=\"A{bit}\", row=1, col={bit + 1})");
            arithmeticScript.Add($"eesim.set_logic(\"A{bit}\", {a.ToString().ToLowerInvariant()})");
            arithmeticScript.Add($"eesim.add_component(\"electronics.logicinput\", name=\"B{bit}\", row=2, col={bit + 1})");
            arithmeticScript.Add($"eesim.set_logic(\"B{bit}\", {b.ToString().ToLowerInvariant()})");
            arithmeticScript.Add($"circuit.connect(A{bit}.out, ADD.A{bit})");
            arithmeticScript.Add($"circuit.connect(B{bit}.out, ADD.B{bit})");
        }

        arithmeticScript.Add("eesim.evaluate_combinational()");
        Assert.True(
            arithmeticExecutor.ExecuteScript(string.Join(Environment.NewLine, arithmeticScript), out string arithmeticMessage),
            arithmeticMessage);

        long sum = 0;
        for (int bit = 0; bit < 4; bit++)
        {
            Assert.True(arithmeticExecutor.ExecuteScript(
                $"sum_bit = eesim.read_digital_output_state(\"ADD\", \"Y{bit}\")",
                out string outputMessage), outputMessage);
            if (Assert.IsType<bool>(arithmeticExecutor.LastResult))
                sum |= 1L << bit;
        }
        Assert.Equal(operandA + operandB, sum);

        var guide = Assert.IsType<ComponentGuide>(ComponentGuideService.Create(ram));
        Assert.Contains("we HIGH", guide.OperatingNotesText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oe HIGH", guide.OperatingNotesText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eesim.set_memory_word", guide.PythonCallsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_script_recreates_component_state_and_escaped_connections()
    {
        var source = new CircuitNode { Title = "Generated round trip" };
        var resistor = Add<ResistorComponent>(source, "R \"quoted\"", component =>
        {
            component.Resistance = 2200;
            component.LogicalRow = 2;
            component.LogicalColumn = 3;
            component.Rotation = 90;
            component.CustomProperties["HarnessTag"] = "value with \"quotes\"";
        });
        var ground = Add<GroundNode>(source, "GND", component =>
        {
            component.LogicalRow = 2;
            component.LogicalColumn = 4;
        });
        source.AddChild(new ConnectionNode(resistor.Id, "pin2", ground.Id, "gnd")
        {
            Resistance = 0.25
        });

        string generatedScript = PythonCodeGenerator.GenerateFullScript(source);
        var restored = new CircuitNode();
        var executor = new PythonCommandExecutor(new PythonApi(new CommandManager(), restored));

        Assert.True(executor.ExecuteScript(generatedScript, out string error), error);

        var restoredResistor = Assert.IsAssignableFrom<ResistorComponent>(restored.FindChild<ComponentNode>("R \"quoted\""));
        Assert.Equal(2200, restoredResistor.Resistance);
        Assert.Equal(2, restoredResistor.LogicalRow);
        Assert.Equal(3, restoredResistor.LogicalColumn);
        Assert.Equal(90, restoredResistor.Rotation);
        Assert.Equal("value with \"quotes\"", restoredResistor.CustomProperties["HarnessTag"]);
        Assert.NotNull(restored.FindChild<ComponentNode>("GND"));
        Assert.Single(restored.Connections);
        var restoredConnection = Assert.Single(restored.Connections);
        Assert.Equal("R \"quoted\"", restored.FindChildren<ComponentNode>()
            .Single(component => component.Id == restoredConnection.FromComponentId).Name);
        Assert.Equal("pin2", restoredConnection.FromPin);
        Assert.Equal("gnd", restoredConnection.ToPin);
        Assert.Equal(0.25, restoredConnection.Resistance);
    }
}
