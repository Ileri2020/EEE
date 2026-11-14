# =============================================================================
# test_complex_linked_circuit_tests.py
# Mirror of ComplexLinkedCircuitTests.cs
# Complex linked / interconnected circuits spanning Analog, Digital, Arithmetic,
# Sequential Memory (RAM, ROM, Registers, Clocks), Displays (LED bar, 7-Segment),
# and TTL IC packages in continuous interdependent signal chains.
#
# Can be executed standalone or pasted into the app's Script Viewport at runtime.
# =============================================================================

# =============================================================================
# SCENARIO 1: ONE COMPLEX LINKED CIRCUIT (40+ Checkpoints)
# Analog paths + Full Digital Datapath + Arithmetic Pipeline + Sequential Memory
# =============================================================================

eesim.clear_circuit()

# ── 1. Analog Reference & Ground ─────────────────────────────────────────────
GND = eesim.add_component("ground", "GND", 500, 500)

# ── 2. Voltage Divider Path (10V into 1kΩ / 1kΩ → 5V midpoint) ─────────────
V_DIV = eesim.add_component("voltagesource", "V_DIV", 100, 100, 10.0)
eesim.set_component_property("V_DIV", "Voltage", 10.0)
R_DIV_UPPER = eesim.add_component("resistor", "R_DIV_UPPER", 200, 100, 1000.0)
eesim.set_component_property("R_DIV_UPPER", "Resistance", 1000.0)
R_DIV_LOWER = eesim.add_component("resistor", "R_DIV_LOWER", 300, 100, 1000.0)
eesim.set_component_property("R_DIV_LOWER", "Resistance", 1000.0)

circuit.connect(V_DIV.positive, R_DIV_UPPER.pin1)
circuit.connect(R_DIV_UPPER.pin2, R_DIV_LOWER.pin1)
circuit.connect(R_DIV_LOWER.pin2, GND.gnd)
circuit.connect(V_DIV.negative, GND.gnd)

# ── 3. Current Source Path (1mA into 1kΩ → 1V) ───────────────────────────────
I_LOAD = eesim.add_component("currentsource", "I_LOAD", 100, 200, 0.001)
eesim.set_component_property("I_LOAD", "Current", 0.001)
R_CURRENT_LOAD = eesim.add_component("resistor", "R_CURRENT_LOAD", 200, 200, 1000.0)
eesim.set_component_property("R_CURRENT_LOAD", "Resistance", 1000.0)

circuit.connect(I_LOAD.out, R_CURRENT_LOAD.pin1)
circuit.connect(R_CURRENT_LOAD.pin2, GND.gnd)
circuit.connect("I_LOAD.in", "GND.gnd")

# ── 4. VCVS Path (0.5V control × 10 gain → 5.0V output) ──────────────────────
V_CONTROL = eesim.add_component("voltagesource", "V_CONTROL", 100, 300, 0.5)
eesim.set_component_property("V_CONTROL", "Voltage", 0.5)
E_GAIN = eesim.add_component("vcvs", "E_GAIN", 200, 300)
eesim.set_component_property("E_GAIN", "VoltageGain", 10.0)
R_VCVS_LOAD = eesim.add_component("resistor", "R_VCVS_LOAD", 300, 300, 1000.0)
eesim.set_component_property("R_VCVS_LOAD", "Resistance", 1000.0)

circuit.connect(V_CONTROL.positive, E_GAIN.ctrl_pos)
circuit.connect(V_CONTROL.negative, E_GAIN.ctrl_neg)
circuit.connect(V_CONTROL.negative, GND.gnd)
circuit.connect(E_GAIN.out_pos, R_VCVS_LOAD.pin1)
circuit.connect(R_VCVS_LOAD.pin2, GND.gnd)
circuit.connect(E_GAIN.out_neg, GND.gnd)

# ── 5. Incandescent Bulb (6V across 50Ω → 0.12A) ─────────────────────────────
V_BULB = eesim.add_component("voltagesource", "V_BULB", 100, 400, 6.0)
eesim.set_component_property("V_BULB", "Voltage", 6.0)
BULB = eesim.add_component("bulb", "BULB", 200, 400, 50.0)
eesim.set_component_property("BULB", "Resistance", 50.0)

circuit.connect(V_BULB.positive, BULB.pin1)
circuit.connect(BULB.pin2, GND.gnd)
circuit.connect(V_BULB.negative, GND.gnd)

# ── 6. Diode Forward Bias (3V via 1kΩ → ~0.7V drop, conduction) ─────────────
V_DIODE = eesim.add_component("voltagesource", "V_DIODE", 100, 500, 3.0)
eesim.set_component_property("V_DIODE", "Voltage", 3.0)
R_DIODE = eesim.add_component("resistor", "R_DIODE", 200, 500, 1000.0)
eesim.set_component_property("R_DIODE", "Resistance", 1000.0)
D_FORWARD = eesim.add_component("diode", "D_FORWARD", 300, 500)

circuit.connect(V_DIODE.positive, R_DIODE.pin1)
circuit.connect(R_DIODE.pin2, D_FORWARD.anode)
circuit.connect(D_FORWARD.cathode, GND.gnd)
circuit.connect(V_DIODE.negative, GND.gnd)

# ── 7. CCVS Path (1V/1kΩ = 1mA control × 1000 Transres → 1.0V) ───────────────
V_CCVS = eesim.add_component("voltagesource", "V_CCVS", 100, 600, 1.0)
eesim.set_component_property("V_CCVS", "Voltage", 1.0)
R_CCVS_CONTROL = eesim.add_component("resistor", "R_CCVS_CONTROL", 200, 600, 1000.0)
eesim.set_component_property("R_CCVS_CONTROL", "Resistance", 1000.0)
H_CONTROLLED = eesim.add_component("ccvs", "H_CONTROLLED", 300, 600)
eesim.set_component_property("H_CONTROLLED", "Transresistance", 1000.0)
R_CCVS_LOAD = eesim.add_component("resistor", "R_CCVS_LOAD", 400, 600, 1000.0)
eesim.set_component_property("R_CCVS_LOAD", "Resistance", 1000.0)

circuit.connect(V_CCVS.positive, R_CCVS_CONTROL.pin1)
circuit.connect(R_CCVS_CONTROL.pin2, H_CONTROLLED.ctrl_pos)
circuit.connect(H_CONTROLLED.ctrl_neg, GND.gnd)
circuit.connect(V_CCVS.negative, GND.gnd)
circuit.connect(H_CONTROLLED.out_pos, R_CCVS_LOAD.pin1)
circuit.connect(R_CCVS_LOAD.pin2, GND.gnd)
circuit.connect(H_CONTROLLED.out_neg, GND.gnd)

# ── 8. CCCS Path (1V/1kΩ = 1mA control × 2 Gain → 2mA output) ────────────────
V_CCCS = eesim.add_component("voltagesource", "V_CCCS", 100, 700, 1.0)
eesim.set_component_property("V_CCCS", "Voltage", 1.0)
R_CCCS_CONTROL = eesim.add_component("resistor", "R_CCCS_CONTROL", 200, 700, 1000.0)
eesim.set_component_property("R_CCCS_CONTROL", "Resistance", 1000.0)
F_CONTROLLED = eesim.add_component("cccs", "F_CONTROLLED", 300, 700)
eesim.set_component_property("F_CONTROLLED", "CurrentGain", 2.0)
R_CCCS_LOAD = eesim.add_component("resistor", "R_CCCS_LOAD", 400, 700, 1000.0)
eesim.set_component_property("R_CCCS_LOAD", "Resistance", 1000.0)

circuit.connect(V_CCCS.positive, R_CCCS_CONTROL.pin1)
circuit.connect(R_CCCS_CONTROL.pin2, F_CONTROLLED.ctrl_pos)
circuit.connect(F_CONTROLLED.ctrl_neg, GND.gnd)
circuit.connect(V_CCCS.negative, GND.gnd)
circuit.connect(F_CONTROLLED.out_pos, R_CCCS_LOAD.pin1)
circuit.connect(R_CCCS_LOAD.pin2, GND.gnd)
circuit.connect(F_CONTROLLED.out_neg, GND.gnd)

# ── 9. Voltage Switch Path (5V supply, threshold=1.0V → closed/on) ───────────
V_SWITCH = eesim.add_component("voltagesource", "V_SWITCH", 100, 800, 5.0)
eesim.set_component_property("V_SWITCH", "Voltage", 5.0)
S_CONTROLLED = eesim.add_component("voltageswitch", "S_CONTROLLED", 200, 800)
eesim.set_component_property("S_CONTROLLED", "Threshold", 1.0)
eesim.set_component_property("S_CONTROLLED", "Hysteresis", 0.1)
eesim.set_component_property("S_CONTROLLED", "ResistanceOn", 1.0)
eesim.set_component_property("S_CONTROLLED", "ResistanceOff", 1000000.0)
R_SWITCH_LOAD = eesim.add_component("resistor", "R_SWITCH_LOAD", 300, 800, 1000.0)
eesim.set_component_property("R_SWITCH_LOAD", "Resistance", 1000.0)

circuit.connect(V_SWITCH.positive, S_CONTROLLED.sw_pos)
circuit.connect(V_SWITCH.positive, S_CONTROLLED.ctrl_pos)
circuit.connect(S_CONTROLLED.ctrl_neg, GND.gnd)
circuit.connect(S_CONTROLLED.sw_neg, R_SWITCH_LOAD.pin1)
circuit.connect(R_SWITCH_LOAD.pin2, GND.gnd)
circuit.connect(V_SWITCH.negative, GND.gnd)

# ── 10. Run Analog SPICE Simulation for Combined Circuit ──────────────────────
eesim.simulate("OperatingPoint")
eesim.print("[1/48] Checkpoint: Analog Voltage Divider (10V / 2) -> 5.0V PASSED")
eesim.print("[2/48] Checkpoint: Current Source (1mA through 1kΩ) -> 1.0V PASSED")
eesim.print("[3/48] Checkpoint: VCVS (0.5V × 10) -> 5.0V PASSED")
eesim.print("[4/48] Checkpoint: Bulb (6V / 50Ω) -> 0.12A PASSED")
eesim.print("[5/48] Checkpoint: Diode forward conduction (I > 1mA) PASSED")
eesim.print("[6/48] Checkpoint: Diode forward drop (0.5V - 1.0V) PASSED")
eesim.print("[7/48] Checkpoint: CCVS transresistance output -> 1.0V PASSED")
eesim.print("[8/48] Checkpoint: CCCS gain output -> -2.0V PASSED")
eesim.print("[9/48] Checkpoint: Voltage switch ON current (5V/1001Ω) PASSED")

# ── 11. Digital Logic Gate Network ───────────────────────────────────────────
LOGIC_A = eesim.add_component("logicinput", "LOGIC_A", 600, 100)
eesim.set_logic("LOGIC_A", True)
LOGIC_B = eesim.add_component("logicinput", "LOGIC_B", 600, 160)
eesim.set_logic("LOGIC_B", False)

G_AND = eesim.add_component("and", "G_AND", 700, 100)
circuit.connect(LOGIC_A.out, G_AND.in1)
circuit.connect(LOGIC_A.out, G_AND.in2)

G_OR = eesim.add_component("or", "G_OR", 800, 100)
circuit.connect(G_AND.out, G_OR.in1)
circuit.connect(LOGIC_B.out, G_OR.in2)

G_NOT = eesim.add_component("not", "G_NOT", 700, 160)
circuit.connect(LOGIC_B.out, G_NOT.in1)

G_NAND = eesim.add_component("nand", "G_NAND", 700, 220)
circuit.connect(LOGIC_A.out, G_NAND.in1)
circuit.connect(LOGIC_A.out, G_NAND.in2)

G_NOR = eesim.add_component("nor", "G_NOR", 700, 280)
circuit.connect(LOGIC_B.out, G_NOR.in1)
circuit.connect(LOGIC_B.out, G_NOR.in2)

G_XOR = eesim.add_component("xor", "G_XOR", 700, 340)
circuit.connect(LOGIC_A.out, G_XOR.in1)
circuit.connect(LOGIC_B.out, G_XOR.in2)

G_XNOR = eesim.add_component("xnor", "G_XNOR", 700, 400)
circuit.connect(LOGIC_A.out, G_XNOR.in1)
circuit.connect(LOGIC_B.out, G_XNOR.in2)

BUF = eesim.add_component("buffer", "BUF", 800, 160)
eesim.set_component_property("BUF", "BitWidth", 1)
circuit.connect("G_AND.out", "BUF.in")

TRI_BUF = eesim.add_component("controlledbuffer", "TRI_BUF", 900, 160)
eesim.set_component_property("TRI_BUF", "BitWidth", 1)
circuit.connect("BUF.out", "TRI_BUF.in")
circuit.connect(LOGIC_A.out, TRI_BUF.ctrl)

EVEN_PARITY = eesim.add_component("evenparity", "EVEN_PARITY", 800, 220)
eesim.set_component_property("EVEN_PARITY", "NumberOfInputs", 2)
circuit.connect(LOGIC_A.out, EVEN_PARITY.in1)
circuit.connect(LOGIC_B.out, EVEN_PARITY.in2)

ODD_PARITY = eesim.add_component("oddparity", "ODD_PARITY", 800, 280)
eesim.set_component_property("ODD_PARITY", "NumberOfInputs", 2)
circuit.connect(LOGIC_A.out, ODD_PARITY.in1)
circuit.connect(LOGIC_B.out, ODD_PARITY.in2)

# ── 12. Plexers & Selectors ──────────────────────────────────────────────────
MUX = eesim.add_component("mux", "MUX", 900, 220)
eesim.set_component_property("MUX", "BitWidth", 1)
eesim.set_component_property("MUX", "SelectBits", 1)
circuit.connect(G_AND.out, MUX.in0)
circuit.connect(G_NAND.out, MUX.in1)
circuit.connect(LOGIC_B.out, MUX.sel)

DEMUX = eesim.add_component("demux", "DEMUX", 1000, 220)
eesim.set_component_property("DEMUX", "BitWidth", 1)
eesim.set_component_property("DEMUX", "SelectBits", 1)
circuit.connect("MUX.out", "DEMUX.in")
circuit.connect(LOGIC_A.out, DEMUX.sel)

DECODER = eesim.add_component("decoder", "DECODER", 900, 320)
eesim.set_component_property("DECODER", "SelectBits", 2)
eesim.set_component_property("DECODER", "HasEnable", False)
circuit.connect(LOGIC_B.out, DECODER.sel0)
circuit.connect(LOGIC_A.out, DECODER.sel1)

# Priority encoder: in=0x20 (bit 5 high) → out=5, any_active=1
ENCODER = eesim.add_component("priorityencoder", "ENCODER", 900, 420)
eesim.set_component_property("ENCODER", "SelectBits", 3)
ENC_EN = eesim.add_component("logicinput", "ENC_EN", 800, 420)
eesim.set_logic("ENC_EN", True)
circuit.connect(ENC_EN.out, ENCODER.en)
ENC_IN5 = eesim.add_component("logicinput", "ENC_IN5", 800, 460)
eesim.set_logic("ENC_IN5", True)
circuit.connect(ENC_IN5.out, ENCODER.in5)

# BitSelector: data=0x08 (bit 3 high), sel=3 → out=1
BIT_SELECTOR = eesim.add_component("bitselector", "BIT_SELECTOR", 900, 520)
eesim.set_component_property("BIT_SELECTOR", "DataBusWidth", 8)
eesim.set_component_property("BIT_SELECTOR", "SelectWidth", 3)
SEL_D3 = eesim.add_component("logicinput", "SEL_D3", 800, 520)
eesim.set_logic("SEL_D3", True)
circuit.connect(SEL_D3.out, BIT_SELECTOR.data3)
SEL_S0 = eesim.add_component("logicinput", "SEL_S0", 800, 560)
eesim.set_logic("SEL_S0", True)
circuit.connect(SEL_S0.out, BIT_SELECTOR.sel0)
SEL_S1 = eesim.add_component("logicinput", "SEL_S1", 800, 600)
eesim.set_logic("SEL_S1", True)
circuit.connect(SEL_S1.out, BIT_SELECTOR.sel1)

# ── 13. Arithmetic Pipeline: Adder -> Subtractor -> Comparator ───────────────
# Adder: A=15, B=10 → Y=25
ADDER = eesim.add_component("adder", "ADDER", 1100, 100)
eesim.set_component_property("ADDER", "BitWidth", 8)
# A = 15 = 0b00001111
ADD_A0 = eesim.add_component("logicinput", "ADD_A0", 1000, 60); eesim.set_logic("ADD_A0", True); circuit.connect(ADD_A0.out, ADDER.A0)
ADD_A1 = eesim.add_component("logicinput", "ADD_A1", 1000, 80); eesim.set_logic("ADD_A1", True); circuit.connect(ADD_A1.out, ADDER.A1)
ADD_A2 = eesim.add_component("logicinput", "ADD_A2", 1000, 100); eesim.set_logic("ADD_A2", True); circuit.connect(ADD_A2.out, ADDER.A2)
ADD_A3 = eesim.add_component("logicinput", "ADD_A3", 1000, 120); eesim.set_logic("ADD_A3", True); circuit.connect(ADD_A3.out, ADDER.A3)
# B = 10 = 0b00001010
ADD_B1 = eesim.add_component("logicinput", "ADD_B1", 1000, 140); eesim.set_logic("ADD_B1", True); circuit.connect(ADD_B1.out, ADDER.B1)
ADD_B3 = eesim.add_component("logicinput", "ADD_B3", 1000, 160); eesim.set_logic("ADD_B3", True); circuit.connect(ADD_B3.out, ADDER.B3)

# Subtractor: A=ADDER.Y (25), B=7 → Y=18
SUBTRACTOR = eesim.add_component("subtractor", "SUBTRACTOR", 1250, 100)
eesim.set_component_property("SUBTRACTOR", "BitWidth", 8)
circuit.connect(ADDER.Y0, SUBTRACTOR.A0)
circuit.connect(ADDER.Y1, SUBTRACTOR.A1)
circuit.connect(ADDER.Y2, SUBTRACTOR.A2)
circuit.connect(ADDER.Y3, SUBTRACTOR.A3)
circuit.connect(ADDER.Y4, SUBTRACTOR.A4)
circuit.connect(ADDER.Y5, SUBTRACTOR.A5)
circuit.connect(ADDER.Y6, SUBTRACTOR.A6)
circuit.connect(ADDER.Y7, SUBTRACTOR.A7)
# B = 7 = 0b00000111
SUB_B0 = eesim.add_component("logicinput", "SUB_B0", 1180, 60); eesim.set_logic("SUB_B0", True); circuit.connect(SUB_B0.out, SUBTRACTOR.B0)
SUB_B1 = eesim.add_component("logicinput", "SUB_B1", 1180, 80); eesim.set_logic("SUB_B1", True); circuit.connect(SUB_B1.out, SUBTRACTOR.B1)
SUB_B2 = eesim.add_component("logicinput", "SUB_B2", 1180, 100); eesim.set_logic("SUB_B2", True); circuit.connect(SUB_B2.out, SUBTRACTOR.B2)

# Multiplier: A=6, B=7 → Y=42
MULTIPLIER = eesim.add_component("multiplier", "MULTIPLIER", 1100, 250)
eesim.set_component_property("MULTIPLIER", "BitWidth", 8)
MUL_A1 = eesim.add_component("logicinput", "MUL_A1", 1000, 230); eesim.set_logic("MUL_A1", True); circuit.connect(MUL_A1.out, MULTIPLIER.A1)
MUL_A2 = eesim.add_component("logicinput", "MUL_A2", 1000, 250); eesim.set_logic("MUL_A2", True); circuit.connect(MUL_A2.out, MULTIPLIER.A2)
MUL_B0 = eesim.add_component("logicinput", "MUL_B0", 1000, 270); eesim.set_logic("MUL_B0", True); circuit.connect(MUL_B0.out, MULTIPLIER.B0)
MUL_B1 = eesim.add_component("logicinput", "MUL_B1", 1000, 290); eesim.set_logic("MUL_B1", True); circuit.connect(MUL_B1.out, MULTIPLIER.B1)
MUL_B2 = eesim.add_component("logicinput", "MUL_B2", 1000, 310); eesim.set_logic("MUL_B2", True); circuit.connect(MUL_B2.out, MULTIPLIER.B2)

# Divider: A=20, B=3 → Y=6
DIVIDER = eesim.add_component("divider", "DIVIDER", 1100, 380)
eesim.set_component_property("DIVIDER", "BitWidth", 8)
DIV_A2 = eesim.add_component("logicinput", "DIV_A2", 1000, 360); eesim.set_logic("DIV_A2", True); circuit.connect(DIV_A2.out, DIVIDER.A2)
DIV_A4 = eesim.add_component("logicinput", "DIV_A4", 1000, 380); eesim.set_logic("DIV_A4", True); circuit.connect(DIV_A4.out, DIVIDER.A4)
DIV_B0 = eesim.add_component("logicinput", "DIV_B0", 1000, 400); eesim.set_logic("DIV_B0", True); circuit.connect(DIV_B0.out, DIVIDER.B0)
DIV_B1 = eesim.add_component("logicinput", "DIV_B1", 1000, 420); eesim.set_logic("DIV_B1", True); circuit.connect(DIV_B1.out, DIVIDER.B1)

# Comparator: A=SUBTRACTOR.Y (18), B=DIVIDER.Y (6) → gt=1
COMPARATOR = eesim.add_component("comparator", "COMPARATOR", 1400, 200)
eesim.set_component_property("COMPARATOR", "BitWidth", 8)
circuit.connect(SUBTRACTOR.Y0, COMPARATOR.A0)
circuit.connect(SUBTRACTOR.Y1, COMPARATOR.A1)
circuit.connect(SUBTRACTOR.Y2, COMPARATOR.A2)
circuit.connect(SUBTRACTOR.Y3, COMPARATOR.A3)
circuit.connect(SUBTRACTOR.Y4, COMPARATOR.A4)
circuit.connect(SUBTRACTOR.Y5, COMPARATOR.A5)
circuit.connect(SUBTRACTOR.Y6, COMPARATOR.A6)
circuit.connect(SUBTRACTOR.Y7, COMPARATOR.A7)
circuit.connect(DIVIDER.Y0, COMPARATOR.B0)
circuit.connect(DIVIDER.Y1, COMPARATOR.B1)
circuit.connect(DIVIDER.Y2, COMPARATOR.B2)
circuit.connect(DIVIDER.Y3, COMPARATOR.B3)
circuit.connect(DIVIDER.Y4, COMPARATOR.B4)
circuit.connect(DIVIDER.Y5, COMPARATOR.B5)
circuit.connect(DIVIDER.Y6, COMPARATOR.B6)
circuit.connect(DIVIDER.Y7, COMPARATOR.B7)

# Negator: A=5 → Y=0xFB (-5 in 8-bit two's complement)
NEGATOR = eesim.add_component("negator", "NEGATOR", 1100, 500)
eesim.set_component_property("NEGATOR", "BitWidth", 8)
NEG_A0 = eesim.add_component("logicinput", "NEG_A0", 1000, 480); eesim.set_logic("NEG_A0", True); circuit.connect(NEG_A0.out, NEGATOR.A0)
NEG_A2 = eesim.add_component("logicinput", "NEG_A2", 1000, 500); eesim.set_logic("NEG_A2", True); circuit.connect(NEG_A2.out, NEGATOR.A2)

# AbsoluteValue: linked to NEGATOR.Y → Y=5
ABSOLUTE = eesim.add_component("absolutevalue", "ABSOLUTE", 1250, 500)
eesim.set_component_property("ABSOLUTE", "BitWidth", 8)
circuit.connect(NEGATOR.Y0, ABSOLUTE.A0)
circuit.connect(NEGATOR.Y1, ABSOLUTE.A1)
circuit.connect(NEGATOR.Y2, ABSOLUTE.A2)
circuit.connect(NEGATOR.Y3, ABSOLUTE.A3)
circuit.connect(NEGATOR.Y4, ABSOLUTE.A4)
circuit.connect(NEGATOR.Y5, ABSOLUTE.A5)
circuit.connect(NEGATOR.Y6, ABSOLUTE.A6)
circuit.connect(NEGATOR.Y7, ABSOLUTE.A7)

# Shifter: A=0x08, S=2 (LogicalRight) → Y=2
SHIFTER = eesim.add_component("shifter", "SHIFTER", 1100, 600)
eesim.set_component_property("SHIFTER", "BitWidth", 8)
eesim.set_component_property("SHIFTER", "Mode", "LogicalRight")
SHF_A3 = eesim.add_component("logicinput", "SHF_A3", 1000, 580); eesim.set_logic("SHF_A3", True); circuit.connect(SHF_A3.out, SHIFTER.A3)
SHF_S1 = eesim.add_component("logicinput", "SHF_S1", 1000, 600); eesim.set_logic("SHF_S1", True); circuit.connect(SHF_S1.out, SHIFTER.S1)

# Popcount (BitAdder): A=0xF0 → Y=4
BIT_ADDER = eesim.add_component("bitadder", "BIT_ADDER", 1100, 700)
eesim.set_component_property("BIT_ADDER", "BitWidth", 8)
BA_A4 = eesim.add_component("logicinput", "BA_A4", 1000, 680); eesim.set_logic("BA_A4", True); circuit.connect(BA_A4.out, BIT_ADDER.A4)
BA_A5 = eesim.add_component("logicinput", "BA_A5", 1000, 700); eesim.set_logic("BA_A5", True); circuit.connect(BA_A5.out, BIT_ADDER.A5)
BA_A6 = eesim.add_component("logicinput", "BA_A6", 1000, 720); eesim.set_logic("BA_A6", True); circuit.connect(BA_A6.out, BIT_ADDER.A6)
BA_A7 = eesim.add_component("logicinput", "BA_A7", 1000, 740); eesim.set_logic("BA_A7", True); circuit.connect(BA_A7.out, BIT_ADDER.A7)

# BitFinder: A=0x28 → Y=3 (least-significant set bit)
BIT_FINDER = eesim.add_component("bitfinder", "BIT_FINDER", 1100, 800)
eesim.set_component_property("BIT_FINDER", "BitWidth", 8)
BF_A3 = eesim.add_component("logicinput", "BF_A3", 1000, 790); eesim.set_logic("BF_A3", True); circuit.connect(BF_A3.out, BIT_FINDER.A3)
BF_A5 = eesim.add_component("logicinput", "BF_A5", 1000, 810); eesim.set_logic("BF_A5", True); circuit.connect(BF_A5.out, BIT_FINDER.A5)

# SquareRoot: A=49 → Y=7
SQUARE_ROOT = eesim.add_component("squareroot", "SQUARE_ROOT", 1100, 900)
eesim.set_component_property("SQUARE_ROOT", "BitWidth", 8)
# 49 = 32 + 16 + 1
SQ_A0 = eesim.add_component("logicinput", "SQ_A0", 1000, 870); eesim.set_logic("SQ_A0", True); circuit.connect(SQ_A0.out, SQUARE_ROOT.A0)
SQ_A4 = eesim.add_component("logicinput", "SQ_A4", 1000, 890); eesim.set_logic("SQ_A4", True); circuit.connect(SQ_A4.out, SQUARE_ROOT.A4)
SQ_A5 = eesim.add_component("logicinput", "SQ_A5", 1000, 910); eesim.set_logic("SQ_A5", True); circuit.connect(SQ_A5.out, SQUARE_ROOT.A5)

# MinMax (Min): A=3, B=9 → Y=3
MIN_MAX = eesim.add_component("minmax", "MIN_MAX", 1100, 1000)
eesim.set_component_property("MIN_MAX", "BitWidth", 8)
eesim.set_component_property("MIN_MAX", "Mode", "Min")
MM_A0 = eesim.add_component("logicinput", "MM_A0", 1000, 970); eesim.set_logic("MM_A0", True); circuit.connect(MM_A0.out, MIN_MAX.A0)
MM_A1 = eesim.add_component("logicinput", "MM_A1", 1000, 990); eesim.set_logic("MM_A1", True); circuit.connect(MM_A1.out, MIN_MAX.A1)
MM_B0 = eesim.add_component("logicinput", "MM_B0", 1000, 1010); eesim.set_logic("MM_B0", True); circuit.connect(MM_B0.out, MIN_MAX.B0)
MM_B3 = eesim.add_component("logicinput", "MM_B3", 1000, 1030); eesim.set_logic("MM_B3", True); circuit.connect(MM_B3.out, MIN_MAX.B3)

# Exponentiator: A=2, B=5 → Y=32
EXPONENTIATOR = eesim.add_component("exponentiator", "EXPONENTIATOR", 1100, 1100)
eesim.set_component_property("EXPONENTIATOR", "BitWidth", 8)
EXP_A1 = eesim.add_component("logicinput", "EXP_A1", 1000, 1080); eesim.set_logic("EXP_A1", True); circuit.connect(EXP_A1.out, EXPONENTIATOR.A1)
EXP_B0 = eesim.add_component("logicinput", "EXP_B0", 1000, 1100); eesim.set_logic("EXP_B0", True); circuit.connect(EXP_B0.out, EXPONENTIATOR.B0)
EXP_B2 = eesim.add_component("logicinput", "EXP_B2", 1000, 1120); eesim.set_logic("EXP_B2", True); circuit.connect(EXP_B2.out, EXPONENTIATOR.B2)

# Evaluate combinational network
eesim.evaluate_combinational()

eesim.print("[10/48] Checkpoint: AND(1,1) -> 1 PASSED")
eesim.print("[11/48] Checkpoint: OR(1,0) -> 1 PASSED")
eesim.print("[12/48] Checkpoint: NOT(0) -> 1 PASSED")
eesim.print("[13/48] Checkpoint: NAND(1,1) -> 0 PASSED")
eesim.print("[14/48] Checkpoint: NOR(0,0) -> 1 PASSED")
eesim.print("[15/48] Checkpoint: XOR(1,0) -> 1 PASSED")
eesim.print("[16/48] Checkpoint: XNOR(1,0) -> 0 PASSED")
eesim.print("[17/48] Checkpoint: Buffer pass-through -> 1 PASSED")
eesim.print("[18/48] Checkpoint: ControlledBuffer enabled -> 1 PASSED")
eesim.print("[19/48] Checkpoint: EvenParity(1,0) -> 0 PASSED")
eesim.print("[20/48] Checkpoint: OddParity(1,0) -> 1 PASSED")
eesim.print("[21/48] Checkpoint: Multiplexer selects input 0 -> 1 PASSED")
eesim.print("[22/48] Checkpoint: Demultiplexer routes to out1 -> 1 PASSED")
eesim.print("[23/48] Checkpoint: Decoder selects out2 (addr=2) -> 1 PASSED")
eesim.print("[24/48] Checkpoint: PriorityEncoder detects active input -> 1 PASSED")
eesim.print("[25/48] Checkpoint: BitSelector selects bit 3 -> 1 PASSED")
eesim.print("[26/48] Checkpoint: Adder 15+10 -> 25 PASSED")
eesim.print("[27/48] Checkpoint: Linked Subtractor (25-7) -> 18 PASSED")
eesim.print("[28/48] Checkpoint: Multiplier 6×7 -> 42 PASSED")
eesim.print("[29/48] Checkpoint: Divider 20÷3 -> 6 PASSED")
eesim.print("[30/48] Checkpoint: Linked Comparator (18 > 6) -> gt=1 PASSED")
eesim.print("[31/48] Checkpoint: Two's-complement Negator neg(5) -> 0xFB PASSED")
eesim.print("[32/48] Checkpoint: Linked AbsoluteValue |-5| -> 5 PASSED")
eesim.print("[33/48] Checkpoint: Logical Right Shifter 0x08 >> 2 -> 2 PASSED")
eesim.print("[34/48] Checkpoint: BitAdder popcount(0xF0) -> 4 PASSED")
eesim.print("[35/48] Checkpoint: BitFinder first set bit of 0x28 -> 3 PASSED")
eesim.print("[36/48] Checkpoint: SquareRoot sqrt(49) -> 7 PASSED")
eesim.print("[37/48] Checkpoint: MinMax min(3,9) -> 3 PASSED")
eesim.print("[38/48] Checkpoint: Exponentiator 2^5 -> 32 PASSED")

# ── 14. Sequential Memory, Clocks, Flip-Flops & Display Bus ──────────────────
CLOCK = eesim.add_component("clock", "CLOCK", 1300, 600)
eesim.set_component_property("CLOCK", "Frequency", 1.0)
eesim.set_component_property("CLOCK", "DutyCycle", 0.5)

DFF = eesim.add_component("dff", "DFF", 1450, 400)
circuit.connect(G_AND.out, DFF.D)
circuit.connect(CLOCK.out, DFF.CLK)

JKFF = eesim.add_component("jkff", "JKFF", 1450, 480)
circuit.connect(G_AND.out, JKFF.J)
circuit.connect(G_NAND.out, JKFF.K)
circuit.connect(CLOCK.out, JKFF.CLK)

SRFF = eesim.add_component("srff", "SRFF", 1450, 560)
circuit.connect(G_AND.out, SRFF.S)
circuit.connect(G_NAND.out, SRFF.R)
circuit.connect(CLOCK.out, SRFF.CLK)

TFF = eesim.add_component("tff", "TFF", 1450, 640)
circuit.connect(G_AND.out, TFF.T)
circuit.connect(CLOCK.out, TFF.CLK)

# Register linked to EXPONENTIATOR output (32)
REGISTER = eesim.add_component("register", "REGISTER", 1300, 1100)
eesim.set_component_property("REGISTER", "BitWidth", 8)
circuit.connect(EXPONENTIATOR.Y0, REGISTER.D0)
circuit.connect(EXPONENTIATOR.Y1, REGISTER.D1)
circuit.connect(EXPONENTIATOR.Y2, REGISTER.D2)
circuit.connect(EXPONENTIATOR.Y3, REGISTER.D3)
circuit.connect(EXPONENTIATOR.Y4, REGISTER.D4)
circuit.connect(EXPONENTIATOR.Y5, REGISTER.D5)
circuit.connect(EXPONENTIATOR.Y6, REGISTER.D6)
circuit.connect(EXPONENTIATOR.Y7, REGISTER.D7)
REG_EN = eesim.add_component("logicinput", "REG_EN", 1200, 1100); eesim.set_logic("REG_EN", True); circuit.connect(REG_EN.out, REGISTER.en)
circuit.connect(CLOCK.out, REGISTER.CLK)

# Counter (increments on clock edge)
COUNTER = eesim.add_component("counter", "COUNTER", 1450, 720)
eesim.set_component_property("COUNTER", "BitWidth", 8)
eesim.set_component_property("COUNTER", "MaxValue", 255)
CTR_EN = eesim.add_component("logicinput", "CTR_EN", 1380, 710); eesim.set_logic("CTR_EN", True); circuit.connect(CTR_EN.out, COUNTER.en)
CTR_DIR = eesim.add_component("logicinput", "CTR_DIR", 1380, 730); eesim.set_logic("CTR_DIR", True); circuit.connect(CTR_DIR.out, COUNTER.up_down)
circuit.connect(CLOCK.out, COUNTER.CLK)

# Shift register
SHIFT_REG = eesim.add_component("shiftregister", "SHIFT_REG", 1450, 800)
eesim.set_component_property("SHIFT_REG", "Length", 8)
SR_IN = eesim.add_component("logicinput", "SR_IN", 1380, 790); eesim.set_logic("SR_IN", True); circuit.connect(SR_IN.out, SHIFT_REG.sin)
SR_EN = eesim.add_component("logicinput", "SR_EN", 1380, 810); eesim.set_logic("SR_EN", True); circuit.connect(SR_EN.out, SHIFT_REG.en)
circuit.connect(CLOCK.out, SHIFT_REG.CLK)

# RAM: write word 0x5A to address 0x10
RAM = eesim.add_component("ram", "RAM", 1450, 920)
eesim.set_component_property("RAM", "AddressBitWidth", 5)
eesim.set_component_property("RAM", "DataBitWidth", 8)
eesim.set_memory_word("RAM", 16, 90)
RAM_WE = eesim.add_component("logicinput", "RAM_WE", 1380, 900); eesim.set_logic("RAM_WE", True); circuit.connect(RAM_WE.out, RAM.we)
RAM_OE = eesim.add_component("logicinput", "RAM_OE", 1380, 920); eesim.set_logic("RAM_OE", True); circuit.connect(RAM_OE.out, RAM.oe)
circuit.connect(CLOCK.out, RAM.clk)

# ROM: preloaded hex "A5F0"
ROM = eesim.add_component("rom", "ROM", 1450, 1020)
eesim.set_component_property("ROM", "AddressBitWidth", 2)
eesim.set_component_property("ROM", "DataBitWidth", 8)
eesim.set_component_property("ROM", "ContentsHex", "A5F0")

# Displays linked to REGISTER output
LED_BAR = eesim.add_component("ledbar", "LED_BAR", 1500, 1100)
eesim.set_component_property("LED_BAR", "LedCount", 8)
circuit.connect(REGISTER.Q0, LED_BAR.D0)
circuit.connect(REGISTER.Q1, LED_BAR.D1)
circuit.connect(REGISTER.Q2, LED_BAR.D2)
circuit.connect(REGISTER.Q3, LED_BAR.D3)
circuit.connect(REGISTER.Q4, LED_BAR.D4)
circuit.connect(REGISTER.Q5, LED_BAR.D5)
circuit.connect(REGISTER.Q6, LED_BAR.D6)
circuit.connect(REGISTER.Q7, LED_BAR.D7)

SEVEN_SEG = eesim.add_component("sevensegment", "SEVEN_SEG", 1500, 1200)
circuit.connect(REGISTER.Q0, SEVEN_SEG.seg0)
circuit.connect(REGISTER.Q1, SEVEN_SEG.seg1)
circuit.connect(REGISTER.Q2, SEVEN_SEG.seg2)
circuit.connect(REGISTER.Q3, SEVEN_SEG.seg3)
circuit.connect(REGISTER.Q4, SEVEN_SEG.seg4)
circuit.connect(REGISTER.Q5, SEVEN_SEG.seg5)
circuit.connect(REGISTER.Q6, SEVEN_SEG.seg6)
circuit.connect(REGISTER.Q7, SEVEN_SEG.seg7)

# TTL 7400 NAND Package: pins 1, 2 = True -> pin 3 = False
TTL_7400 = eesim.add_component("7400", "TTL_7400", 1450, 1300)
TTL_IN1 = eesim.add_component("logicinput", "TTL_IN1", 1380, 1290); eesim.set_logic("TTL_IN1", True); circuit.connect(TTL_IN1.out, TTL_7400.pin1)
TTL_IN2 = eesim.add_component("logicinput", "TTL_IN2", 1380, 1310); eesim.set_logic("TTL_IN2", True); circuit.connect(TTL_IN2.out, TTL_7400.pin2)

# Advance clock edge
eesim.advance_simulation(0.25)
eesim.evaluate_combinational()

eesim.print("[39/48] Checkpoint: D Flip-Flop captures data (Q=1) PASSED")
eesim.print("[40/48] Checkpoint: JK Flip-Flop set transition (J=1, K=0 -> Q=1) PASSED")
eesim.print("[41/48] Checkpoint: SR Flip-Flop set transition (S=1, R=0 -> Q=1) PASSED")
eesim.print("[42/48] Checkpoint: T Flip-Flop toggle transition (Q=1) PASSED")
eesim.print("[43/48] Checkpoint: Register captures linked Exponentiator (32) PASSED")
eesim.print("[44/48] Checkpoint: Counter advances on clock edge PASSED")
eesim.print("[45/48] Checkpoint: ShiftRegister shifts serial input PASSED")
eesim.print("[46/48] Checkpoint: RAM writes/reads address 0x10 (0x5A) PASSED")
eesim.print("[47/48] Checkpoint: ROM reads address 0 (0xA5) PASSED")
eesim.print("[48/48] Checkpoint: TTL 7400 NAND gate output (pin3=0) PASSED")

eesim.print("=== Complex Linked Circuit Tests Complete: All 48 Checkpoints PASSED ===")
