using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using ComponentRegistry = EESimulator.Core.Components.Registry.ComponentRegistry;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using Xunit;

namespace EESimulator.Tests;

public sealed class ComponentCircuitTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // REGISTRY INTEGRITY: ALL 167 REGISTERED COMPONENTS INSTANTIATE CLEANLY
    // ═══════════════════════════════════════════════════════════════════════
    public static IEnumerable<object[]> RegisteredComponents =>
        ComponentRegistry.GetAllDefinitions()
            .Select(definition => new object[] { definition.StableTypeId });

    [Theory]
    [MemberData(nameof(RegisteredComponents))]
    public void Every_registered_component_can_be_created(string stableTypeId)
    {
        var definition = ComponentRegistry.GetDefinition(stableTypeId);
        var component = ComponentRegistry.CreateInstance(stableTypeId, "T1");

        Assert.IsType(definition.NodeType, component);
        Assert.Equal("T1", component.Name);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1. ARITHMETIC SIMULATED CIRCUITS
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Arithmetic_Adder_5_Plus_3_Equals_8_No_Carry()
    {
        var circuit = new CircuitNode();
        var adder = new AdderComponent { BitWidth = 8 };
        circuit.AddChild(adder);

        SetBusInput(circuit, adder, "A", 5, 8);
        SetBusInput(circuit, adder, "B", 3, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(8, ReadBusOutput(adder, "Y", 8));
        Assert.False(DigitalLogicSimulator.ReadOutputState(adder, "cout"));
    }

    [Fact]
    public void Arithmetic_Adder_Overflow_Wraps_With_Carry()
    {
        var circuit = new CircuitNode();
        var adder = new AdderComponent { BitWidth = 8 };
        circuit.AddChild(adder);

        SetBusInput(circuit, adder, "A", 200, 8);
        SetBusInput(circuit, adder, "B", 100, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(44, ReadBusOutput(adder, "Y", 8)); // 300 & 0xFF = 44
        Assert.True(DigitalLogicSimulator.ReadOutputState(adder, "cout"));
    }

    [Fact]
    public void Arithmetic_Subtractor_10_Minus_3_Equals_7()
    {
        var circuit = new CircuitNode();
        var sub = new SubtractorComponent { BitWidth = 8 };
        circuit.AddChild(sub);

        SetBusInput(circuit, sub, "A", 10, 8);
        SetBusInput(circuit, sub, "B", 3, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(7, ReadBusOutput(sub, "Y", 8));
    }

    [Fact]
    public void Arithmetic_Multiplier_6_Times_7_Equals_42()
    {
        var circuit = new CircuitNode();
        var mul = new MultiplierComponent { BitWidth = 8 };
        circuit.AddChild(mul);

        SetBusInput(circuit, mul, "A", 6, 8);
        SetBusInput(circuit, mul, "B", 7, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(42, ReadBusOutput(mul, "Y", 8));
    }

    [Fact]
    public void Arithmetic_Divider_20_Divided_By_3_Equals_6()
    {
        var circuit = new CircuitNode();
        var div = new DividerComponent { BitWidth = 8 };
        circuit.AddChild(div);

        SetBusInput(circuit, div, "A", 20, 8);
        SetBusInput(circuit, div, "B", 3, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(6, ReadBusOutput(div, "Y", 8));
        Assert.False(DigitalLogicSimulator.ReadOutputState(div, "divide_by_zero"));
    }

    [Fact]
    public void Arithmetic_Comparator_10_Greater_Than_7()
    {
        var circuit = new CircuitNode();
        var cmp = new ComparatorComponent { BitWidth = 8 };
        circuit.AddChild(cmp);

        SetBusInput(circuit, cmp, "A", 10, 8);
        SetBusInput(circuit, cmp, "B", 7, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.True(DigitalLogicSimulator.ReadOutputState(cmp, "gt"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(cmp, "eq"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(cmp, "lt"));
    }

    [Fact]
    public void Arithmetic_Negator_Twos_Complement()
    {
        var circuit = new CircuitNode();
        var neg = new NegatorComponent { BitWidth = 8 };
        circuit.AddChild(neg);

        SetBusInput(circuit, neg, "A", 5, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(0xFB, ReadBusOutput(neg, "Y", 8)); // -5 in 8-bit two's complement
    }

    [Fact]
    public void Arithmetic_BitAdder_Popcount_Counts_Set_Bits()
    {
        var circuit = new CircuitNode();
        var pop = new BitAdderComponent { BitWidth = 8 };
        circuit.AddChild(pop);

        SetBusInput(circuit, pop, "A", 0xF0, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(4, ReadBusOutput(pop, "Y", 8));
    }

    [Fact]
    public void Arithmetic_BitFinder_Finds_First_Set_Bit()
    {
        var circuit = new CircuitNode();
        var bfnd = new BitFinderComponent { BitWidth = 8 };
        circuit.AddChild(bfnd);

        SetBusInput(circuit, bfnd, "A", 0x28, 8); // 0b0010_1000 -> bit 3
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(3, ReadBusOutput(bfnd, "Y", 8));
        Assert.True(DigitalLogicSimulator.ReadOutputState(bfnd, "found"));
    }

    [Fact]
    public void Arithmetic_Shifter_Logical_Right_Shift()
    {
        var circuit = new CircuitNode();
        var shf = new ShifterComponent { BitWidth = 8, Mode = "LogicalRight" };
        circuit.AddChild(shf);

        SetBusInput(circuit, shf, "A", 0x08, 8);
        SetBusInput(circuit, shf, "S", 2, shf.ShiftWidth);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(0x02, ReadBusOutput(shf, "Y", 8));
    }

    [Fact]
    public void Arithmetic_SquareRoot_Integer_Sqrt()
    {
        var circuit = new CircuitNode();
        var sqrt = new SquareRootComponent { BitWidth = 8 };
        circuit.AddChild(sqrt);

        SetBusInput(circuit, sqrt, "A", 49, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(7, ReadBusOutput(sqrt, "Y", 8));
    }

    [Fact]
    public void Arithmetic_MinMax_Selects_Minimum()
    {
        var circuit = new CircuitNode();
        var mm = new MinMaxComponent { BitWidth = 8, Mode = "Min" };
        circuit.AddChild(mm);

        SetBusInput(circuit, mm, "A", 3, 8);
        SetBusInput(circuit, mm, "B", 9, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(3, ReadBusOutput(mm, "Y", 8));
    }

    [Fact]
    public void Arithmetic_Exponentiator_Computes_Power()
    {
        var circuit = new CircuitNode();
        var exp = new ExponentiatorComponent { BitWidth = 8 };
        circuit.AddChild(exp);

        SetBusInput(circuit, exp, "A", 2, 8);
        SetBusInput(circuit, exp, "B", 5, 8);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.Equal(32, ReadBusOutput(exp, "Y", 8));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. LOGIC GATES SIMULATED CIRCUITS
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Gates_All_Primitive_Gates_Evaluate_Truth_Tables()
    {
        var circuit = new CircuitNode();
        var a = new LogicInputComponent("A");
        var b = new LogicInputComponent("B");
        var andGate = new AndGateComponent("AND");
        var orGate = new OrGateComponent("OR");
        var notGate = new NotGateComponent("NOT");
        var nandGate = new NandGateComponent("NAND");
        var norGate = new NorGateComponent("NOR");
        var xorGate = new XorGateComponent("XOR");
        var xnorGate = new XnorGateComponent("XNOR");

        circuit.AddChild(a); circuit.AddChild(b);
        circuit.AddChild(andGate); circuit.AddChild(orGate); circuit.AddChild(notGate);
        circuit.AddChild(nandGate); circuit.AddChild(norGate); circuit.AddChild(xorGate); circuit.AddChild(xnorGate);

        Connect(circuit, a, "out", andGate, "in1"); Connect(circuit, b, "out", andGate, "in2");
        Connect(circuit, a, "out", orGate, "in1"); Connect(circuit, b, "out", orGate, "in2");
        Connect(circuit, a, "out", notGate, "in1");
        Connect(circuit, a, "out", nandGate, "in1"); Connect(circuit, b, "out", nandGate, "in2");
        Connect(circuit, a, "out", norGate, "in1"); Connect(circuit, b, "out", norGate, "in2");
        Connect(circuit, a, "out", xorGate, "in1"); Connect(circuit, b, "out", xorGate, "in2");
        Connect(circuit, a, "out", xnorGate, "in1"); Connect(circuit, b, "out", xnorGate, "in2");

        // Case 1: A=0, B=0
        a.State = false; b.State = false;
        DigitalLogicSimulator.EvaluateCombinational(circuit);
        Assert.False(DigitalLogicSimulator.ReadOutputState(andGate, "out"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(orGate, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(notGate, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(nandGate, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(norGate, "out"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(xorGate, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(xnorGate, "out"));

        // Case 2: A=1, B=1
        a.State = true; b.State = true;
        DigitalLogicSimulator.EvaluateCombinational(circuit);
        Assert.True(DigitalLogicSimulator.ReadOutputState(andGate, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(orGate, "out"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(notGate, "out"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(nandGate, "out"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(norGate, "out"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(xorGate, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(xnorGate, "out"));
    }

    [Fact]
    public void Gates_Buffer_And_ControlledBuffer_Tristate_Operation()
    {
        var circuit = new CircuitNode();
        var buf = new BufferComponent { BitWidth = 1 };
        var tri = new ControlledBufferComponent { BitWidth = 1 };
        circuit.AddChild(buf); circuit.AddChild(tri);

        SetPinInput(circuit, buf, "in", true);
        SetPinInput(circuit, tri, "in", true);
        SetPinInput(circuit, tri, "ctrl", true);
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.True(DigitalLogicSimulator.ReadOutputState(buf, "out"));
        Assert.True(DigitalLogicSimulator.ReadOutputState(tri, "out"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. PLEXERS & SELECTORS SIMULATED CIRCUITS
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Plexers_Multiplexer_Selects_Input_Channel()
    {
        var circuit = new CircuitNode();
        var mux = new MultiplexerComponent { BitWidth = 1, SelectBits = 1 };
        circuit.AddChild(mux);

        SetPinInput(circuit, mux, "in0", true);
        SetPinInput(circuit, mux, "in1", false);
        SetPinInput(circuit, mux, "sel0", false); // Select channel 0
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.True(DigitalLogicSimulator.ReadOutputState(mux, "out"));
    }

    [Fact]
    public void Plexers_Demultiplexer_Routes_Input_To_Selected_Channel()
    {
        var circuit = new CircuitNode();
        var dmx = new DemultiplexerComponent { BitWidth = 1, SelectBits = 1 };
        circuit.AddChild(dmx);

        SetPinInput(circuit, dmx, "in", true);
        SetPinInput(circuit, dmx, "sel", true); // Select channel 1
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.True(DigitalLogicSimulator.ReadOutputState(dmx, "out1"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(dmx, "out0"));
    }

    [Fact]
    public void Plexers_Decoder_Activates_Binary_Address_Output()
    {
        var circuit = new CircuitNode();
        var dec = new DecoderComponent { SelectBits = 2, HasEnable = false };
        circuit.AddChild(dec);

        SetPinInput(circuit, dec, "sel0", false);
        SetPinInput(circuit, dec, "sel1", true); // Binary 2 (0b10)
        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.True(DigitalLogicSimulator.ReadOutputState(dec, "out2"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(dec, "out0"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(dec, "out1"));
        Assert.False(DigitalLogicSimulator.ReadOutputState(dec, "out3"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. MEMORY & SEQUENTIAL SIMULATED CIRCUITS
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Memory_DFlipFlop_Latches_Data_On_Clock_Edge()
    {
        var circuit = new CircuitNode();
        var dff = new DFlipFlopComponent();
        var clk = new ClockComponent { Frequency = 1.0, DutyCycle = 0.5 };
        circuit.AddChild(dff); circuit.AddChild(clk);

        SetPinInput(circuit, dff, "D", true);
        Connect(circuit, clk, "out", dff, "CLK");

        DigitalLogicSimulator.Advance(circuit, 0.25); // Rising edge occurs
        Assert.True(dff.StateQ);
    }

    [Fact]
    public void Memory_Register_Loads_Parallel_Word_On_Clock()
    {
        var circuit = new CircuitNode();
        var reg = new RegisterComponent { BitWidth = 8 };
        var clk = new ClockComponent { Frequency = 1.0, DutyCycle = 0.5 };
        circuit.AddChild(reg); circuit.AddChild(clk);

        SetBusInput(circuit, reg, "D", 0xA5, 8);
        SetPinInput(circuit, reg, "en", true);
        Connect(circuit, clk, "out", reg, "CLK");

        DigitalLogicSimulator.Advance(circuit, 0.25);
        Assert.Equal(0xA5, reg.Value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5. SPICESHARP ANALOG ENGINE & CONTROLLED SOURCES
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Resistor_divider_matches_expected_dc_voltage_and_current()
    {
        var circuit = new CircuitNode();
        var supply = (VoltageSourceComponent)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        supply.Voltage = 5.0;
        var upper = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "R1");
        upper.Resistance = 1000.0;
        var lower = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "R2");
        lower.Resistance = 2000.0;
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(supply); circuit.AddChild(upper); circuit.AddChild(lower); circuit.AddChild(ground);
        Connect(circuit, supply, "positive", upper, "pin1");
        Connect(circuit, upper, "pin2", lower, "pin1");
        Connect(circuit, lower, "pin2", ground, "gnd");
        Connect(circuit, supply, "negative", ground, "gnd");

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        Assert.InRange(upper.LivePinVoltages["pin1"], 4.999, 5.001);
        Assert.InRange(upper.LivePinVoltages["pin2"], 3.332, 3.335);
        Assert.InRange(Math.Abs(upper.LiveCurrent), 0.001665, 0.001668);
    }

    [Fact]
    public void Analog_Diode_Forward_Bias_Conducts_With_Junction_Drop()
    {
        var circuit = new CircuitNode();
        var supply = (VoltageSourceComponent)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        supply.Voltage = 5.0;
        var resistor = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "R1");
        resistor.Resistance = 430.0;
        var diode = (DiodeComponent)ComponentRegistry.CreateInstance("Diode", "D1");
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(supply); circuit.AddChild(resistor); circuit.AddChild(diode); circuit.AddChild(ground);
        Connect(circuit, supply, "positive", resistor, "pin1");
        Connect(circuit, resistor, "pin2", diode, "anode");
        Connect(circuit, diode, "cathode", ground, "gnd");
        Connect(circuit, supply, "negative", ground, "gnd");

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        Assert.InRange(diode.LivePinVoltages["anode"], 0.60, 0.80);
    }

    [Fact]
    public void Analog_VCVS_Amplifies_Controlling_Voltage()
    {
        var circuit = new CircuitNode();
        var vin = (VoltageSourceComponent)ComponentRegistry.CreateInstance("VoltageSource", "Vin");
        vin.Voltage = 0.5;
        var vcvs = (VCVSComponent)ComponentRegistry.CreateInstance("VCVS", "E1");
        vcvs.VoltageGain = 10.0;
        var rload = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "RL");
        rload.Resistance = 1000.0;
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(vin); circuit.AddChild(vcvs); circuit.AddChild(rload); circuit.AddChild(ground);
        Connect(circuit, vin, "positive", vcvs, "ctrl_pos");
        Connect(circuit, vin, "negative", vcvs, "ctrl_neg");
        Connect(circuit, vcvs, "out_pos", rload, "pin1");
        Connect(circuit, rload, "pin2", ground, "gnd");
        Connect(circuit, vcvs, "out_neg", ground, "gnd");
        Connect(circuit, vin, "negative", ground, "gnd");

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        // Vout = Gain * Vin = 10 * 0.5 = 5.0V
        Assert.InRange(rload.LivePinVoltages["pin1"], 4.99, 5.01);
    }

    [Fact]
    public void Analog_CurrentSource_Produces_Expected_Load_Voltage()
    {
        var circuit = new CircuitNode();
        var isrc = (CurrentSourceComponent)ComponentRegistry.CreateInstance("CurrentSource", "I1");
        isrc.Current = 0.001; // 1 mA
        var rload = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "RL");
        rload.Resistance = 1000.0; // 1k
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(isrc); circuit.AddChild(rload); circuit.AddChild(ground);
        Connect(circuit, isrc, "out", rload, "pin1");
        Connect(circuit, rload, "pin2", ground, "gnd");
        Connect(circuit, isrc, "in", ground, "gnd");

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        Assert.InRange(rload.LivePinVoltages["pin1"], 0.99, 1.01); // 1mA * 1k = 1.0V
    }

    [Fact]
    public void Bulb_state_and_brightness_follow_simulated_electrical_values()
    {
        var circuit = new CircuitNode();
        var supply = (VoltageSourceComponent)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        supply.Voltage = 5.0;
        var bulb = (BulbComponent)ComponentRegistry.CreateInstance("Bulb", "B1");
        bulb.Resistance = 50.0;
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(supply); circuit.AddChild(bulb); circuit.AddChild(ground);
        Connect(circuit, supply, "positive", bulb, "pin1");
        Connect(circuit, bulb, "pin2", ground, "gnd");
        Connect(circuit, supply, "negative", ground, "gnd");

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        Assert.True(bulb.IsOn);
        Assert.InRange(Math.Abs(bulb.LiveCurrent), 0.099, 0.101);
        Assert.InRange(bulb.Brightness, 0.82, 0.85);
    }

    [Fact]
    public void Indicator_state_properties_are_read_only_and_voltage_driven()
    {
        var bulb = new BulbComponent();
        var led = new LEDComponent();
        var output = new LogicOutputComponent();

        Assert.Null(typeof(BulbComponent).GetProperty(nameof(BulbComponent.IsOn))!.SetMethod);
        Assert.Null(typeof(LEDComponent).GetProperty(nameof(LEDComponent.IsOn))!.SetMethod);
        Assert.Null(typeof(LogicOutputComponent).GetProperty(nameof(LogicOutputComponent.IsOn))!.SetMethod);

        bulb.LiveVoltage = 0.0; led.LiveVoltage = 0.0; output.LiveVoltage = 0.0;
        Assert.False(bulb.IsOn); Assert.False(led.IsOn); Assert.False(output.IsOn);

        bulb.LiveVoltage = 2.0; led.LiveVoltage = 2.0; output.LiveVoltage = 3.0;
        Assert.True(bulb.IsOn); Assert.True(led.IsOn); Assert.True(output.IsOn);
    }

    [Fact]
    public void Rc_transient_reaches_expected_one_time_constant_voltage()
    {
        const double resistance = 1000.0;
        const double capacitance = 1e-6;
        const double timeConstant = resistance * capacitance;
        var circuit = new CircuitNode();
        var supply = (VoltageSourceComponent)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        supply.WaveformType = "Pulse";
        supply.PulseInitial = 0.0;
        supply.PulsePulsed = 5.0;
        supply.PulseRiseTime = 1e-6;
        supply.PulseFallTime = 1e-6;
        supply.PulseWidth = 0.01;
        supply.PulsePeriod = 0.02;
        var resistor = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "R1");
        resistor.Resistance = resistance;
        var capacitor = (CapacitorComponent)ComponentRegistry.CreateInstance("Capacitor", "C1");
        capacitor.Capacitance = capacitance;
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(supply); circuit.AddChild(resistor); circuit.AddChild(capacitor); circuit.AddChild(ground);
        Connect(circuit, supply, "positive", resistor, "pin1");
        Connect(circuit, resistor, "pin2", capacitor, "pin1");
        Connect(circuit, capacitor, "pin2", ground, "gnd");
        Connect(circuit, supply, "negative", ground, "gnd");

        var settings = new SimulationSettings
        {
            StopTime = 0.005,
            StepTime = 1e-5,
            CaptureAllWaveforms = true
        };
        var result = new SpiceSharpAdapter().RunSimulation(circuit, settings);

        Assert.True(result.Success, result.Message);
        var timePoints = capacitor.WaveformTime;
        var voltagePoints = capacitor.WaveformNodeVoltages["pin1"];
        int sampleIndex = Enumerable.Range(0, timePoints.Count)
            .MinBy(index => Math.Abs(timePoints[index] - timeConstant));
        double expectedVoltage = 5.0 * (1.0 - Math.Exp(-timePoints[sampleIndex] / timeConstant));

        Assert.True(
            Math.Abs(voltagePoints[sampleIndex] - expectedVoltage) <= 0.15,
            $"At t={timePoints[sampleIndex]:G6}s, expected {expectedVoltage:G6}V but got {voltagePoints[sampleIndex]:G6}V.");
    }

    [Fact]
    public void Rl_transient_reaches_expected_one_time_constant_current()
    {
        const double resistance = 100.0;
        const double inductance = 1e-3;
        const double timeConstant = inductance / resistance;
        var circuit = new CircuitNode();
        var supply = (VoltageSourceComponent)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        supply.WaveformType = "Pulse";
        supply.PulseInitial = 0.0;
        supply.PulsePulsed = 5.0;
        supply.PulseRiseTime = 1e-8;
        supply.PulseFallTime = 1e-8;
        supply.PulseWidth = 0.001;
        supply.PulsePeriod = 0.002;
        var resistor = (ResistorComponent)ComponentRegistry.CreateInstance("Resistor", "R1");
        resistor.Resistance = resistance;
        var inductor = (InductorComponent)ComponentRegistry.CreateInstance("Inductor", "L1");
        inductor.Inductance = inductance;
        var ground = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(supply); circuit.AddChild(resistor); circuit.AddChild(inductor); circuit.AddChild(ground);
        Connect(circuit, supply, "positive", resistor, "pin1");
        Connect(circuit, resistor, "pin2", inductor, "pin1");
        Connect(circuit, inductor, "pin2", ground, "gnd");
        Connect(circuit, supply, "negative", ground, "gnd");

        var settings = new SimulationSettings
        {
            StopTime = 0.00005,
            StepTime = 1e-7,
            CaptureAllWaveforms = true
        };
        var result = new SpiceSharpAdapter().RunSimulation(circuit, settings);

        Assert.True(result.Success, result.Message);
        int sampleIndex = Enumerable.Range(0, resistor.WaveformTime.Count)
            .MinBy(index => Math.Abs(resistor.WaveformTime[index] - timeConstant));
        double expectedResistorVoltage = 5.0 * (1.0 - Math.Exp(-resistor.WaveformTime[sampleIndex] / timeConstant));

        Assert.InRange(
            resistor.WaveformVoltage[sampleIndex],
            expectedResistorVoltage - 0.25,
            expectedResistorVoltage + 0.25);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER METHODS MATCHING SIMULATED CIRCUIT HARNESS
    // ═══════════════════════════════════════════════════════════════════════

    private static void Connect(
        CircuitNode circuit,
        ComponentNode from,
        string fromPin,
        ComponentNode to,
        string toPin)
    {
        circuit.AddChild(new ConnectionNode(from.Id, fromPin, to.Id, toPin));
    }

    private static void SetBusInput(CircuitNode circuit, ComponentNode uut, string pinPrefix, long value, int bitWidth)
    {
        for (int bit = 0; bit < bitWidth; bit++)
        {
            bool bitVal = ((value >> bit) & 1) != 0;
            var inputComp = new LogicInputComponent($"{uut.Name}_{pinPrefix}_{bit}") { State = bitVal };
            circuit.AddChild(inputComp);
            circuit.AddChild(new ConnectionNode(inputComp.Id, "out", uut.Id, $"{pinPrefix}{bit}"));
        }
    }

    private static long ReadBusOutput(ComponentNode uut, string pinPrefix, int bitWidth)
    {
        long value = 0;
        for (int bit = 0; bit < bitWidth; bit++)
        {
            if (DigitalLogicSimulator.ReadOutputState(uut, $"{pinPrefix}{bit}"))
            {
                value |= (1L << bit);
            }
        }
        return value;
    }

    private static void SetPinInput(CircuitNode circuit, ComponentNode uut, string pinName, bool state)
    {
        var inputComp = new LogicInputComponent($"{uut.Name}_{pinName}_in") { State = state };
        circuit.AddChild(inputComp);
        circuit.AddChild(new ConnectionNode(inputComp.Id, "out", uut.Id, pinName));
    }
}
