using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Registry;

/// <summary>
/// Central dynamic registry and factory for all electrical and electronic components.
/// Supports stable type identifiers (e.g. 'electrical.resistor'), categorized browsing,
/// and dynamic instantiation for UI palettes and Python scripting.
/// </summary>
public static class ComponentRegistry
{
    private static readonly ConcurrentDictionary<string, ComponentDefinition> _registry = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ComponentDefinition> _allDefinitions = new();
    private static readonly object _syncLock = new();

    static ComponentRegistry()
    {
        RegisterDefaultComponents();
    }

    public static void Register<T>(string stableTypeId, string typeName, string category, string referencePrefix, Action<ComponentDefinition>? configure = null) where T : ComponentNode, new()
    {
        var def = new ComponentDefinition
        {
            StableTypeId = stableTypeId,
            TypeName = typeName,
            DisplayName = typeName,
            Category = category,
            ReferencePrefix = referencePrefix,
            SymbolKey = typeName,
            NodeType = typeof(T),
            FactoryMethod = () => new T()
        };

        configure?.Invoke(def);

        lock (_syncLock)
        {
            _registry[stableTypeId] = def;
            _registry[typeName] = def;
            _registry[typeof(T).Name] = def;

            if (!_allDefinitions.Any(d => d.StableTypeId == stableTypeId))
            {
                _allDefinitions.Add(def);
            }
        }
    }

    public static void RegisterAlias(string alias, string targetTypeId)
    {
        lock (_syncLock)
        {
            if (_registry.TryGetValue(targetTypeId, out var def))
            {
                _registry[alias] = def;
            }
        }
    }

    public static bool TryGetDefinition(string typeIdOrName, out ComponentDefinition definition)
    {
        return _registry.TryGetValue(typeIdOrName, out definition!);
    }

    public static ComponentDefinition GetDefinition(string typeIdOrName)
    {
        if (_registry.TryGetValue(typeIdOrName, out var def))
            return def;

        throw new KeyNotFoundException($"No component registered for type or ID '{typeIdOrName}'.");
    }

    public static IEnumerable<ComponentDefinition> GetAllDefinitions()
    {
        lock (_syncLock)
        {
            return _allDefinitions.ToList();
        }
    }

    public static IEnumerable<ComponentDefinition> GetByCategory(string category)
    {
        lock (_syncLock)
        {
            return _allDefinitions.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public static IEnumerable<string> GetCategories()
    {
        lock (_syncLock)
        {
            return _allDefinitions.Select(d => d.Category).Distinct().ToList();
        }
    }

    public static ComponentNode CreateInstance(string typeIdOrName, string name = "", double x = 0, double y = 0)
    {
        var def = GetDefinition(typeIdOrName);
        var instance = (ComponentNode)def.FactoryMethod();
        instance.Name = string.IsNullOrEmpty(name) ? $"{def.ReferencePrefix}_{Guid.NewGuid().ToString("N")[..4]}" : name;
        instance.Reference = instance.Name;
        instance.X = x;
        instance.Y = y;
        return instance;
    }

    private static void RegisterDefaultComponents()
    {
        // ── Electrical (Passive & Sources) ───────────────────────────────────
        Register<ResistorNode>("electrical.resistor", "Resistor", "Electrical", "R", d =>
        {
            d.Description = "Linear two-terminal electrical resistor";
            d.DefaultParameters["Resistance"] = 1000.0;
        });

        Register<CapacitorNode>("electrical.capacitor", "Capacitor", "Electrical", "C", d =>
        {
            d.Description = "Linear two-terminal electrical capacitor";
            d.DefaultParameters["Capacitance"] = 1e-6;
        });

        Register<InductorNode>("electrical.inductor", "Inductor", "Electrical", "L", d =>
        {
            d.Description = "Linear two-terminal electrical inductor";
            d.DefaultParameters["Inductance"] = 1e-3;
        });

        Register<VoltageSourceNode>("electrical.voltagesource", "VoltageSource", "Electrical", "V", d =>
        {
            d.Description = "Independent DC/AC voltage source";
            d.DefaultParameters["Voltage"] = 5.0;
        });

        Register<CurrentSourceNode>("electrical.currentsource", "CurrentSource", "Electrical", "I", d =>
        {
            d.Description = "Independent DC/AC current source";
            d.DefaultParameters["Current"] = 0.001;
        });

        Register<GroundNode>("electrical.ground", "Ground", "Electrical", "GND", d =>
        {
            d.Description = "Circuit ground reference (Node 0)";
            d.Icon = "ground.svg";
        });

        Register<DiodeComponent>("electrical.diode", "Diode", "Electrical", "D", d =>
        {
            d.Description = "Semiconductor P-N junction diode";
            d.DefaultParameters["Is"] = 1e-14;
        });

        Register<MutualInductanceNode>("electrical.mutualinductance", "MutualInductance", "Electrical", "K", d =>
        {
            d.Description = "Magnetic mutual coupling between two inductors (K)";
            d.DefaultParameters["Coupling"] = 0.99;
        });

        Register<BulbComponent>("electrical.bulb", "Bulb", "Electrical", "LAMP", d =>
        {
            d.Description = "Incandescent Light Bulb";
            d.Icon = "bulb.svg";
            d.DefaultParameters["Resistance"] = 50.0;
            d.DefaultParameters["ThresholdVoltage"] = 1.0;
        });

        // Controlled / Dependent Sources
        Register<VCVSComponent>("electrical.vcvs", "VCVS", "Electrical", "E", d =>
        {
            d.Description = "Voltage-Controlled Voltage Source (E)";
            d.Icon = "vcvs.svg";
            d.DefaultParameters["Gain"] = 1.0;
        });

        Register<VCCSComponent>("electrical.vccs", "VCCS", "Electrical", "G", d =>
        {
            d.Description = "Voltage-Controlled Current Source (G)";
            d.Icon = "vccs.svg";
            d.DefaultParameters["Transconductance"] = 0.001;
        });

        Register<CCVSComponent>("electrical.ccvs", "CCVS", "Electrical", "H", d =>
        {
            d.Description = "Current-Controlled Voltage Source (H)";
            d.Icon = "ccvs.svg";
            d.DefaultParameters["Transresistance"] = 1000.0;
        });

        Register<CCCSComponent>("electrical.cccs", "CCCS", "Electrical", "F", d =>
        {
            d.Description = "Current-Controlled Current Source (F)";
            d.Icon = "cccs.svg";
            d.DefaultParameters["CurrentGain"] = 1.0;
        });

        // Switches
        Register<VoltageSwitchComponent>("electrical.voltageswitch", "VoltageSwitch", "Electrical", "S", d =>
        {
            d.Description = "Voltage-Controlled Switch (S)";
            d.Icon = "voltageswitch.svg";
            d.DefaultParameters["Ron"] = 1.0;
            d.DefaultParameters["Roff"] = 1e6;
            d.DefaultParameters["Threshold"] = 1.0;
            d.DefaultParameters["Hysteresis"] = 0.1;
        });

        Register<CurrentSwitchComponent>("electrical.currentswitch", "CurrentSwitch", "Electrical", "W", d =>
        {
            d.Description = "Current-Controlled Switch (W)";
            d.Icon = "currentswitch.svg";
            d.DefaultParameters["Ron"] = 1.0;
            d.DefaultParameters["Roff"] = 1e6;
            d.DefaultParameters["Threshold"] = 0.001;
            d.DefaultParameters["Hysteresis"] = 0.0;
        });

        // ── Distributed & Transmission Line Elements ─────────────────────────
        Register<TransmissionLineComponent>("electrical.transmissionline", "TransmissionLine", "Electrical", "T", d =>
        {
            d.Description = "Lossless Transmission Line (T)";
            d.Icon = "transmissionline.svg";
            d.DefaultParameters["Z0"] = 50.0;
            d.DefaultParameters["TD"] = 1e-9;
            d.DefaultParameters["F"] = 1e9;
            d.DefaultParameters["NL"] = 0.25;
        });

        Register<VoltageDelayComponent>("electrical.voltagedelay", "VoltageDelay", "Electrical", "VD", d =>
        {
            d.Description = "Voltage Delay Element (Delay)";
            d.Icon = "voltagedelay.svg";
            d.DefaultParameters["Delay"] = 1e-6;
        });

        // ── Electronics (Active & Semiconductors) ────────────────────────────
        Register<LEDComponent>("electronics.led", "LED", "Electronics", "LED", d =>
        {
            d.Description = "Light Emitting Diode";
            d.Icon = "led.svg";
            d.DefaultParameters["Vf"] = 2.0;
        });

        Register<BJTComponent>("electronics.bjt.npn", "NPN", "Electronics", "Q", d =>
        {
            d.Description = "BJT NPN Bipolar Junction Transistor";
            d.Icon = "bjt_npn.svg";
            d.DefaultParameters["Bf"] = 100.0;
            d.FactoryMethod = () => new BJTComponent { Polarity = BjtPolarity.NPN };
        });

        Register<BJTComponent>("electronics.bjt.pnp", "PNP", "Electronics", "Q", d =>
        {
            d.Description = "BJT PNP Bipolar Junction Transistor";
            d.Icon = "bjt_pnp.svg";
            d.DefaultParameters["Bf"] = 100.0;
            d.FactoryMethod = () => new BJTComponent { Polarity = BjtPolarity.PNP };
        });

        Register<BJTComponent>("electronics.bjt", "BJT", "Electronics", "Q", d =>
        {
            d.Description = "Bipolar Junction Transistor (NPN/PNP)";
            d.Icon = "bjt_npn.svg";
            d.DefaultParameters["Bf"] = 100.0;
        });

        Register<JFETComponent>("electronics.jfet", "JFET", "Electronics", "J", d =>
        {
            d.Description = "Junction Field-Effect Transistor (JFET N-Ch/P-Ch)";
            d.Icon = "jfet.svg";
            d.DefaultParameters["Vto"] = -2.0;
            d.DefaultParameters["Beta"] = 1e-4;
        });

        Register<MOSFETComponent>("electronics.mosfet.nmos", "NMOS", "Electronics", "M", d =>
        {
            d.Description = "N-Channel Enhancement MOSFET";
            d.Icon = "nmos.svg";
            d.DefaultParameters["Vto"] = 2.0;
            d.FactoryMethod = () => new MOSFETComponent { Channel = MosfetChannel.NChannel };
        });

        Register<MOSFETComponent>("electronics.mosfet.pmos", "PMOS", "Electronics", "M", d =>
        {
            d.Description = "P-Channel Enhancement MOSFET";
            d.Icon = "pmos.svg";
            d.DefaultParameters["Vto"] = -2.0;
            d.FactoryMethod = () => new MOSFETComponent { Channel = MosfetChannel.PChannel };
        });

        Register<MOSFETComponent>("electronics.mosfet", "MOSFET", "Electronics", "M", d =>
        {
            d.Description = "MOSFET Field-Effect Transistor (N-Ch/P-Ch)";
            d.Icon = "nmos.svg";
            d.DefaultParameters["Vto"] = 2.0;
        });

        Register<OpAmpComponent>("electronics.opamp", "OpAmp", "Electronics", "U", d =>
        {
            d.Description = "Operational Amplifier";
            d.DefaultParameters["Aol"] = 100000.0;
        });

        Register<LogicGateComponent>("electronics.logicgate", "LogicGate", "Electronics", "U", d =>
        {
            d.Description = "Generic Digital Logic Gate";
        });

        Register<AndGateComponent>("electronics.and", "AND", "Electronics", "U", d =>
        {
            d.Description = "2-Input AND Gate";
            d.Icon = "and.svg";
        });

        Register<OrGateComponent>("electronics.or", "OR", "Electronics", "U", d =>
        {
            d.Description = "2-Input OR Gate";
            d.Icon = "or.svg";
        });

        Register<NotGateComponent>("electronics.not", "NOT", "Electronics", "U", d =>
        {
            d.Description = "Inverter / NOT Gate";
            d.Icon = "not.svg";
        });

        Register<NandGateComponent>("electronics.nand", "NAND", "Electronics", "U", d =>
        {
            d.Description = "2-Input NAND Gate";
            d.Icon = "nand.svg";
        });

        Register<NorGateComponent>("electronics.nor", "NOR", "Electronics", "U", d =>
        {
            d.Description = "2-Input NOR Gate";
            d.Icon = "nor.svg";
        });

        Register<XorGateComponent>("electronics.xor", "XOR", "Electronics", "U", d =>
        {
            d.Description = "2-Input XOR Gate";
            d.Icon = "xor.svg";
        });

        Register<XnorGateComponent>("electronics.xnor", "XNOR", "Electronics", "U", d =>
        {
            d.Description = "2-Input XNOR Gate";
            d.Icon = "xnor.svg";
        });

        Register<ICComponent>("electronics.ic", "IC", "Electronics", "U", d =>
        {
            d.Description = "Integrated Circuit DIP package";
        });

        Register<LogicInputComponent>("electronics.logicinput", "LogicInput", "Electronics", "IN", d =>
        {
            d.Description = "Digital Logic Toggle Input (0/1)";
            d.Icon = "logicinput.svg";
            d.DefaultParameters["HighVoltage"] = 5.0;
            d.DefaultParameters["LowVoltage"] = 0.0;
        });

        Register<LogicOutputComponent>("electronics.logicoutput", "LogicOutput", "Electronics", "OUT", d =>
        {
            d.Description = "Digital Logic Output Probe (0/1)";
            d.Icon = "logicoutput.svg";
            d.DefaultParameters["Threshold"] = 2.5;
        });

        // ── Digital & Core Wiring (Logisim compatible) ─────────────────────
        Register<ClockComponent>("electronics.clock", "Clock", "Electronics", "CLK", d =>
        {
            d.Description = "Digital clock pulse generator";
            d.Icon = "clock.svg";
            d.DefaultParameters["Frequency"] = 1000.0;
            d.DefaultParameters["DutyCycle"] = 0.50;
            d.DefaultParameters["HighVoltage"] = 5.0;
            d.DefaultParameters["LowVoltage"] = 0.0;
        });

        Register<PinComponent>("electronics.pin", "Pin", "Electronics", "PIN", d =>
        {
            d.Description = "Named I/O port terminal (Input / Output)";
            d.Icon = "pin.svg";
            d.DefaultParameters["BitWidth"] = 1.0;
            d.DefaultParameters["Value"] = 0.0;
            d.DefaultParameters["HighVoltage"] = 5.0;
            d.DefaultParameters["LowVoltage"] = 0.0;
        });

        Register<PowerComponent>("electrical.power", "Power", "Electrical", "VCC", d =>
        {
            d.Description = "Positive supply rail (+VCC node)";
            d.Icon = "power.svg";
            d.DefaultParameters["Voltage"] = 5.0;
        });

        Register<PullResistorComponent>("electrical.pullresistor", "PullResistor", "Electrical", "RPULL", d =>
        {
            d.Description = "Configurable pull-up / pull-down resistor";
            d.Icon = "pullresistor.svg";
            d.DefaultParameters["Resistance"] = 10000.0;
            d.DefaultParameters["RailVoltage"] = 5.0;
        });

        Register<TunnelComponent>("general.tunnel", "Tunnel", "General", "TUN", d =>
        {
            d.Description = "Named net tunnel / label junction";
            d.Icon = "tunnel.svg";
            d.IsSimulatable = true;
        });

        Register<ProbeComponent>("general.probe", "Probe", "General", "PRB", d =>
        {
            d.Description = "Voltage/current probe measurement point";
            d.Icon = "probe.svg";
            d.IsSimulatable = true;
        });

        Register<DigitalTransistorComponent>("electronics.transistor", "Transistor", "Electronics", "SWT", d =>
        {
            d.Description = "Digital-mode switch transistor (N-Type / P-Type)";
            d.Icon = "transistor_digital.svg";
            d.DefaultParameters["Ron"] = 0.1;
            d.DefaultParameters["Roff"] = 10e6;
            d.DefaultParameters["Threshold"] = 2.5;
        });

        Register<TransmissionGateComponent>("electronics.transmissiongate", "TransmissionGate", "Electronics", "TG", d =>
        {
            d.Description = "CMOS bilateral switch (N+P MOSFET transmission gate)";
            d.Icon = "transmissiongate.svg";
            d.DefaultParameters["Ron"] = 0.2;
            d.DefaultParameters["Roff"] = 10e6;
        });

        Register<BitExtenderComponent>("electronics.bitextender", "BitExtender", "Electronics", "EXT", d =>
        {
            d.Description = "Bus bit extender (sign/zero-extend bus signal)";
            d.Icon = "bitextender.svg";
            d.DefaultParameters["InputBitWidth"] = 1.0;
            d.DefaultParameters["OutputBitWidth"] = 8.0;
        });

        Register<ConstantComponent>("electronics.constant", "Constant", "Electronics", "CONST", d =>
        {
            d.Description = "Wired logic HIGH/LOW constant value source";
            d.Icon = "constant.svg";
            d.DefaultParameters["Value"] = 1.0;
            d.DefaultParameters["VoltageHigh"] = 5.0;
            d.DefaultParameters["VoltageLow"] = 0.0;
        });

        Register<DoNotConnectComponent>("general.dnc", "DoNotConnect", "General", "DNC", d =>
        {
            d.Description = "DNC marker for unused pins";
            d.Icon = "dnc.svg";
            d.IsSimulatable = true;
        });

        // ── Additional Transmission Line Components ──────────────────────────
        Register<LossyTransmissionLineComponent>("electrical.lossytransmissionline", "LossyTransmissionLine", "Electrical", "TLOSS", d =>
        {
            d.Description = "Lossy Transmission Line (RLGC Distributed Model)";
            d.Icon = "lossy_transmissionline.svg";
            d.DefaultParameters["R"] = 0.2;
            d.DefaultParameters["L"] = 250e-9;
            d.DefaultParameters["G"] = 10e-6;
            d.DefaultParameters["C"] = 100e-12;
            d.DefaultParameters["Length"] = 1.0;
        });

        Register<CoaxialCableComponent>("electrical.coaxialcable", "CoaxialCable", "Electrical", "COAX", d =>
        {
            d.Description = "Coaxial Cable Transmission Line";
            d.Icon = "coaxial.svg";
            d.DefaultParameters["Z0"] = 50.0;
            d.DefaultParameters["VF"] = 0.66;
            d.DefaultParameters["Length"] = 1.0;
            d.DefaultParameters["Attenuation"] = 0.05;
        });

        Register<MicrostripLineComponent>("electrical.microstripline", "MicrostripLine", "Electrical", "USTRIP", d =>
        {
            d.Description = "PCB Microstrip Planar Transmission Line";
            d.Icon = "microstrip.svg";
            d.DefaultParameters["W"] = 1.5e-3;
            d.DefaultParameters["H"] = 0.8e-3;
            d.DefaultParameters["Er"] = 4.4;
            d.DefaultParameters["Length"] = 0.05;
        });

        // ── General (Structural & Schematic) ─────────────────────────────────
        Register<WireComponent>("general.wire", "Wire", "General", "W", d =>
        {
            d.Description = "Schematic wire segment";
            d.IsSimulatable = false;
        });

        Register<JunctionComponent>("general.junction", "Junction", "General", "J", d =>
        {
            d.Description = "Electrical wire junction";
            d.IsSimulatable = false;
        });

        Register<LabelComponent>("general.label", "Label", "General", "LBL", d =>
        {
            d.Description = "Net connectivity label";
            d.IsSimulatable = false;
        });

        Register<TextAnnotationComponent>("general.textannotation", "TextAnnotation", "General", "TXT", d =>
        {
            d.Description = "Free-floating text annotation";
            d.IsSimulatable = false;
        });

        Register<ConnectorComponent>("general.connector", "Connector", "General", "J", d =>
        {
            d.Description = "Multi-pin external header connector";
            d.IsSimulatable = false;
        });

        // ── Logisim Gates Library ─────────────────────────────────────────────
        Register<BufferComponent>("electronics.buffer", "Buffer", "Gates", "BUF", d =>
        {
            d.Description = "Unity-gain digital buffer (signal driver)";
            d.Icon = "buffer.svg";
            d.DefaultParameters["BitWidth"] = 1.0;
        });

        Register<ControlledBufferComponent>("electronics.controlledbuffer", "ControlledBuffer", "Gates", "TRI", d =>
        {
            d.Description = "Tri-state controlled buffer (enable input)";
            d.Icon = "controlledbuffer.svg";
            d.DefaultParameters["BitWidth"] = 1.0;
        });

        Register<EvenParityGateComponent>("electronics.evenparity", "EvenParity", "Gates", "EPAR", d =>
        {
            d.Description = "N-input even-parity check gate";
            d.Icon = "evenparity.svg";
            d.DefaultParameters["NumberOfInputs"] = 2.0;
        });

        Register<OddParityGateComponent>("electronics.oddparity", "OddParity", "Gates", "OPAR", d =>
        {
            d.Description = "N-input odd-parity check gate";
            d.Icon = "oddparity.svg";
            d.DefaultParameters["NumberOfInputs"] = 2.0;
        });

        Register<PLAComponent>("electronics.pla", "PLA", "Gates", "PLA", d =>
        {
            d.Description = "Programmable Logic Array";
            d.Icon = "pla.svg";
            d.DefaultParameters["InputCount"] = 2.0;
            d.DefaultParameters["OutputCount"] = 2.0;
            d.DefaultParameters["AndTerms"] = 4.0;
        });

        // ── Logisim Plexers Library ───────────────────────────────────────────
        Register<MultiplexerComponent>("electronics.mux", "Multiplexer", "Plexers", "MUX", d =>
        {
            d.Description = "N-to-1 digital data selector (Multiplexer)";
            d.Icon = "mux.svg";
            d.DefaultParameters["SelectBits"] = 1.0;
            d.DefaultParameters["BitWidth"] = 1.0;
        });

        Register<DemultiplexerComponent>("electronics.demux", "Demultiplexer", "Plexers", "DMX", d =>
        {
            d.Description = "1-to-N digital data distributor (Demultiplexer)";
            d.Icon = "demux.svg";
            d.DefaultParameters["SelectBits"] = 1.0;
            d.DefaultParameters["BitWidth"] = 1.0;
        });

        Register<DecoderComponent>("electronics.decoder", "Decoder", "Plexers", "DEC", d =>
        {
            d.Description = "Binary decoder (n-to-2^n)";
            d.Icon = "decoder.svg";
            d.DefaultParameters["SelectBits"] = 1.0;
        });

        Register<PriorityEncoderComponent>("electronics.priorityencoder", "PriorityEncoder", "Plexers", "ENC", d =>
        {
            d.Description = "Priority encoder (highest active input to binary)";
            d.Icon = "priorityencoder.svg";
            d.DefaultParameters["SelectBits"] = 3.0;
        });

        Register<BitSelectorComponent>("electronics.bitselector", "BitSelector", "Plexers", "BSEL", d =>
        {
            d.Description = "Select specific bit from a digital bus";
            d.Icon = "bitselector.svg";
            d.DefaultParameters["DataBusWidth"] = 8.0;
            d.DefaultParameters["SelectWidth"] = 3.0;
        });

        // ── Logisim Memory Library ────────────────────────────────────────────
        Register<DFlipFlopComponent>("electronics.dff", "DFlipFlop", "Memory", "DFF", d =>
        {
            d.Description = "Edge-triggered D-Type Flip-Flop";
            d.Icon = "dff.svg";
        });

        Register<JKFlipFlopComponent>("electronics.jkff", "JKFlipFlop", "Memory", "JKFF", d =>
        {
            d.Description = "Edge-triggered JK-Type Flip-Flop";
            d.Icon = "jkff.svg";
        });

        Register<SRFlipFlopComponent>("electronics.srff", "SRFlipFlop", "Memory", "SRFF", d =>
        {
            d.Description = "Clocked Set-Reset (SR) Flip-Flop";
            d.Icon = "srff.svg";
        });

        Register<TFlipFlopComponent>("electronics.tff", "TFlipFlop", "Memory", "TFF", d =>
        {
            d.Description = "Edge-triggered Toggle (T-Type) Flip-Flop";
            d.Icon = "tff.svg";
        });

        Register<RegisterComponent>("electronics.register", "Register", "Memory", "REG", d =>
        {
            d.Description = "N-bit parallel-load register";
            d.Icon = "register.svg";
            d.DefaultParameters["BitWidth"] = 8.0;
        });

        Register<CounterComponent>("electronics.counter", "Counter", "Memory", "CTR", d =>
        {
            d.Description = "Up/Down binary counter with load and carry";
            d.Icon = "counter.svg";
            d.DefaultParameters["BitWidth"] = 8.0;
            d.DefaultParameters["MaxValue"] = 255.0;
        });

        Register<ShiftRegisterComponent>("electronics.shiftregister", "ShiftRegister", "Memory", "SHR", d =>
        {
            d.Description = "Serial-in / parallel-out shift register";
            d.Icon = "shiftregister.svg";
            d.DefaultParameters["Length"] = 8.0;
        });

        Register<RAMComponent>("electronics.ram", "RAM", "Memory", "RAM", d =>
        {
            d.Description = "Random-access read/write memory";
            d.Icon = "ram.svg";
            d.DefaultParameters["AddressBitWidth"] = 8.0;
            d.DefaultParameters["DataBitWidth"] = 8.0;
        });

        Register<ROMComponent>("electronics.rom", "ROM", "Memory", "ROM", d =>
        {
            d.Description = "Read-only memory with preloaded contents";
            d.Icon = "rom.svg";
            d.DefaultParameters["AddressBitWidth"] = 8.0;
            d.DefaultParameters["DataBitWidth"] = 8.0;
        });

        Register<RandomComponent>("electronics.random", "Random", "Memory", "RND", d =>
        {
            d.Description = "Pseudo-random number generator (PRNG)";
            d.Icon = "random.svg";
            d.DefaultParameters["BitWidth"] = 8.0;
            d.DefaultParameters["Seed"] = 1.0;
        });

        // ── Logisim Arithmetic Library ──────────────────────────────────────
        Register<AdderComponent>("arith.adder", "Adder", "Arithmetic", "ADD", d => { d.Description = "N-bit binary adder"; d.Icon = "adder.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<SubtractorComponent>("arith.subtractor", "Subtractor", "Arithmetic", "SUB", d => { d.Description = "N-bit binary subtractor"; d.Icon = "subtractor.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<MultiplierComponent>("arith.multiplier", "Multiplier", "Arithmetic", "MUL", d => { d.Description = "N-bit binary multiplier"; d.Icon = "multiplier.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<DividerComponent>("arith.divider", "Divider", "Arithmetic", "DIV", d => { d.Description = "N-bit binary divider"; d.Icon = "divider.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<ComparatorComponent>("arith.comparator", "Comparator", "Arithmetic", "CMP", d => { d.Description = "N-bit magnitude comparator"; d.Icon = "comparator.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<NegatorComponent>("arith.negator", "Negator", "Arithmetic", "NEG", d => { d.Description = "Two's-complement negator"; d.Icon = "negator.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<ShifterComponent>("arith.shifter", "Shifter", "Arithmetic", "SHF", d => { d.Description = "Logical/arithmetic barrel shifter"; d.Icon = "shifter.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<BitAdderComponent>("arith.bitadder", "BitAdder", "Arithmetic", "POP", d => { d.Description = "Population count"; d.Icon = "bitadder.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<BitFinderComponent>("arith.bitfinder", "BitFinder", "Arithmetic", "FND", d => { d.Description = "First/last set bit finder"; d.Icon = "bitfinder.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<SquareRootComponent>("arith.sqrt", "SquareRoot", "Arithmetic", "SQR", d => { d.Description = "Integer square root"; d.Icon = "sqrt.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<AbsoluteValueComponent>("arith.absolute", "AbsoluteValue", "Arithmetic", "ABS", d => { d.Description = "N-bit absolute value"; d.Icon = "absolute.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<MinMaxComponent>("arith.minmax", "MinMax", "Arithmetic", "MM", d => { d.Description = "Minimum/maximum selector"; d.Icon = "minmax.svg"; d.DefaultParameters["BitWidth"] = 8; });
        Register<ExponentiatorComponent>("arith.exponentiator", "Exponentiator", "Arithmetic", "POW", d => { d.Description = "Integer exponentiation"; d.Icon = "exponentiator.svg"; d.DefaultParameters["BitWidth"] = 8; });

        // ── Logisim I/O Library ──────────────────────────────────────────────
        Register<ButtonComponent>("io.button", "Button", "I/O", "BTN", d => { d.Description = "Momentary push-button input"; d.Icon = "button.svg"; });
        Register<DipSwitchComponent>("io.dipswitch", "DIPSwitch", "I/O", "DIP", d => { d.Description = "N-pole DIP switch bank"; d.Icon = "dipswitch.svg"; d.DefaultParameters[nameof(DipSwitchComponent.PoleCount)] = 8; });
        Register<SevenSegmentComponent>("io.sevensegment", "SevenSegment", "I/O", "7SEG", d => { d.Description = "Seven-segment display"; d.Icon = "sevensegment.svg"; });
        Register<HexDigitComponent>("io.hexdigit", "HexDigit", "I/O", "HEX", d => { d.Description = "Hexadecimal digit display"; d.Icon = "hexdigit.svg"; });
        Register<LedBarComponent>("io.ledbar", "LedBar", "I/O", "LBA", d => { d.Description = "LED bar array"; d.Icon = "ledbar.svg"; d.DefaultParameters[nameof(LedBarComponent.LedCount)] = 8; });
        Register<RgbLedComponent>("io.rgbled", "RGBLED", "I/O", "RGB", d => { d.Description = "RGB LED indicator"; d.Icon = "rgbled.svg"; });
        Register<DotMatrixComponent>("io.dotmatrix", "DotMatrix", "I/O", "DM", d => { d.Description = "Scanned LED dot matrix"; d.Icon = "dotmatrix.svg"; d.DefaultParameters[nameof(DotMatrixComponent.Rows)] = 7; d.DefaultParameters[nameof(DotMatrixComponent.Columns)] = 5; });
        Register<JoystickComponent>("io.joystick", "Joystick", "I/O", "JOY", d => { d.Description = "Two-axis joystick input"; d.Icon = "joystick.svg"; });
        Register<KeyboardComponent>("io.keyboard", "Keyboard", "I/O", "KEY", d => { d.Description = "ASCII keyboard buffer"; d.Icon = "keyboard.svg"; });
        Register<TtyComponent>("io.tty", "TTY", "I/O", "TTY", d => { d.Description = "Serial text terminal"; d.Icon = "tty.svg"; });
        Register<VideoComponent>("io.video", "Video", "I/O", "VID", d => { d.Description = "Pixel framebuffer display"; d.Icon = "video.svg"; });
        Register<RealTimeClockComponent>("io.realtimeclock", "RealTimeClock", "I/O", "RTC", d => { d.Description = "Wall-clock time source"; d.Icon = "realtimeclock.svg"; });

        // ── Aliases ──────────────────────────────────────────────────────────
        RegisterAlias("tline", "electrical.transmissionline");
        RegisterAlias("lossytline", "electrical.lossytransmissionline");
        RegisterAlias("coax", "electrical.coaxialcable");
        RegisterAlias("microstrip", "electrical.microstripline");
        RegisterAlias("vdelay", "electrical.voltagedelay");
        RegisterAlias("npn", "electronics.bjt.npn");
        RegisterAlias("pnp", "electronics.bjt.pnp");
        RegisterAlias("nmos", "electronics.mosfet.nmos");
        RegisterAlias("pmos", "electronics.mosfet.pmos");
        RegisterAlias("lamp", "electrical.bulb");
        RegisterAlias("lightbulb", "electrical.bulb");
        RegisterAlias("logicin", "electronics.logicinput");
        RegisterAlias("logicout", "electronics.logicoutput");
        RegisterAlias("clk", "electronics.clock");
        RegisterAlias("vcc", "electrical.power");
        RegisterAlias("pullup", "electrical.pullresistor");
        RegisterAlias("pulldown", "electrical.pullresistor");
        RegisterAlias("netlabel", "general.tunnel");
        RegisterAlias("switch_transistor", "electronics.transistor");
        RegisterAlias("tgate", "electronics.transmissiongate");
        RegisterAlias("extender", "electronics.bitextender");
        RegisterAlias("const", "electronics.constant");
        RegisterAlias("dnc", "general.dnc");
        RegisterAlias("nc", "general.dnc");
        RegisterAlias("mux", "electronics.mux");
        RegisterAlias("add", "arith.adder");
        RegisterAlias("sub", "arith.subtractor");
        RegisterAlias("mul", "arith.multiplier");
        RegisterAlias("div", "arith.divider");
        RegisterAlias("sqrt", "arith.sqrt");
        RegisterAlias("abs", "arith.absolute");
        RegisterAlias("button", "io.button");
        RegisterAlias("dip", "io.dipswitch");
        RegisterAlias("7segment", "io.sevensegment");
        RegisterAlias("rtc", "io.realtimeclock");
        RegisterAlias("demux", "electronics.demux");
        RegisterAlias("dff", "electronics.dff");
        RegisterAlias("jkff", "electronics.jkff");
        RegisterAlias("srff", "electronics.srff");
        RegisterAlias("tff", "electronics.tff");
        RegisterAlias("tristate", "electronics.controlledbuffer");
        RegisterAlias("prng", "electronics.random");
    }
}
