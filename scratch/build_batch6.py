import json, uuid, os

def make_comp(stable_type, comp_type, ref, name, desc, row, col, string_params=None, elec_params=None, custom_props=None, breaking_current=10.0):
    c = {
        "Id": str(uuid.uuid4()),
        "StableTypeId": stable_type,
        "ComponentType": comp_type,
        "Reference": ref,
        "Name": name,
        "Description": desc,
        "StringParameters": {
            "LogicalRow": str(row),
            "LogicalColumn": str(col)
        },
        "Rotation": 0.0,
        "ScaleX": 1.0,
        "ScaleY": 1.0,
        "BreakingCurrent": float(breaking_current),
        "ElectricalParameters": elec_params or {},
        "CustomProperties": custom_props or {}
    }
    if string_params:
        c["StringParameters"].update(string_params)
    return c

def make_conn(from_c, from_pin, to_c, to_pin, net_name="NET"):
    return {
        "Id": str(uuid.uuid4()),
        "FromComponentId": from_c["Id"],
        "FromPin": from_pin,
        "ToComponentId": to_c["Id"],
        "ToPin": to_pin,
        "NetName": net_name,
        "Resistance": 0.0
    }

# ==============================================================================
# 1. three_phase_transformer.eee
# ==============================================================================
def build_three_phase_transformer():
    gnd = make_comp("electrical.ground", "Ground", "GND", "GND", "Ground Reference", 7, 1, breaking_current=100.0)
    
    # 400V line-to-line RMS -> 230.94V phase RMS -> 326.6V peak
    va = make_comp("electrical.voltagesource", "VoltageSource", "V_A", "V_A", "Phase A AC Source", 2, 1,
                   {"WaveformType": "Sine"},
                   {"Voltage": 230.94, "SineOffset": 0.0, "SineAmplitude": 326.6, "SineFrequency": 50.0, "SinePhase": 0.0},
                   breaking_current=50.0)
    vb = make_comp("electrical.voltagesource", "VoltageSource", "V_B", "V_B", "Phase B AC Source", 4, 1,
                   {"WaveformType": "Sine"},
                   {"Voltage": 230.94, "SineOffset": 0.0, "SineAmplitude": 326.6, "SineFrequency": 50.0, "SinePhase": -120.0},
                   breaking_current=50.0)
    vc = make_comp("electrical.voltagesource", "VoltageSource", "V_C", "V_C", "Phase C AC Source", 6, 1,
                   {"WaveformType": "Sine"},
                   {"Voltage": 230.94, "SineOffset": 0.0, "SineAmplitude": 326.6, "SineFrequency": 50.0, "SinePhase": 120.0},
                   breaking_current=50.0)
    
    t1 = make_comp("machine.ac-transformer", "ACTransformer", "T1", "T1", "Three-Phase Y-Y Transformer", 4, 3,
                   {"TransformerType": "ThreePhase", "WindingConnection": "Y-Y", "VectorGroup": "Yy0"},
                   {
                       "NumberOfPhases": 3.0,
                       "RatedPrimaryVoltage": 400.0,
                       "RatedSecondaryVoltage": 230.0,
                       "RatedApparentPower": 10000.0,
                       "PrimaryTurns": 1000.0,
                       "SecondaryTurns": 575.0,
                       "Frequency": 50.0,
                       "PrimaryResistance": 0.1,
                       "SecondaryResistance": 0.05,
                       "PrimaryLeakageInductance": 0.001,
                       "SecondaryLeakageInductance": 0.0005,
                       "MagnetizingInductance": 1.0,
                       "CoreLossResistance": 10000.0,
                       "EfficiencyPercent": 95.0
                   },
                   breaking_current=50.0)
    
    ra = make_comp("electrical.resistor", "Resistor", "R_A", "R_A", "Secondary Phase A Load", 2, 5,
                   elec_params={"Resistance": 1000.0}, breaking_current=10.0)
    rb = make_comp("electrical.resistor", "Resistor", "R_B", "R_B", "Secondary Phase B Load", 4, 5,
                   elec_params={"Resistance": 1000.0}, breaking_current=10.0)
    rc = make_comp("electrical.resistor", "Resistor", "R_C", "R_C", "Secondary Phase C Load", 6, 5,
                   elec_params={"Resistance": 1000.0}, breaking_current=10.0)
    
    tp_pri = make_comp("general.probe", "Probe", "TP_PRI_A", "TP_PRI_A", "Primary Phase A Peak Probe", 1, 2,
                       {"ExpectedVoltage": "326.6"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_sec_a = make_comp("general.probe", "Probe", "TP_SEC_A", "TP_SEC_A", "Secondary Phase A Peak Probe", 1, 5,
                         {"ExpectedVoltage": "187.8"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_sec_b = make_comp("general.probe", "Probe", "TP_SEC_B", "TP_SEC_B", "Secondary Phase B Peak Probe", 3, 5,
                         {"ExpectedVoltage": "187.8"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    
    comps = [gnd, va, vb, vc, t1, ra, rb, rc, tp_pri, tp_sec_a, tp_sec_b]
    
    conns = [
        make_conn(va, "negative", gnd, "gnd", "0"),
        make_conn(vb, "negative", gnd, "gnd", "0"),
        make_conn(vc, "negative", gnd, "gnd", "0"),
        make_conn(va, "positive", t1, "primary_A", "PRI_A"),
        make_conn(va, "positive", tp_pri, "in", "PRI_A"),
        make_conn(vb, "positive", t1, "primary_B", "PRI_B"),
        make_conn(vc, "positive", t1, "primary_C", "PRI_C"),
        make_conn(t1, "primary_neutral", gnd, "gnd", "0"),
        make_conn(t1, "secondary_neutral", gnd, "gnd", "0"),
        make_conn(t1, "secondary_A", ra, "pin1", "SEC_A"),
        make_conn(t1, "secondary_A", tp_sec_a, "in", "SEC_A"),
        make_conn(ra, "pin2", gnd, "gnd", "0"),
        make_conn(t1, "secondary_B", rb, "pin1", "SEC_B"),
        make_conn(t1, "secondary_B", tp_sec_b, "in", "SEC_B"),
        make_conn(rb, "pin2", gnd, "gnd", "0"),
        make_conn(t1, "secondary_C", rc, "pin1", "SEC_C"),
        make_conn(rc, "pin2", gnd, "gnd", "0")
    ]
    
    return {
        "FileType": "EEE-Circuit",
        "FormatVersion": "1.0",
        "Application": "EEE",
        "Title": "Three-Phase Transformer",
        "Description": "Three-phase Y-Y power transformer circuit supplied by balanced 400V line-to-line (230.9V RMS phase, 326.6V peak, 50 Hz) three-phase AC sources, demonstrating a 1000:575 turns ratio step-down to 230V line-to-line (187.8V peak phase) across balanced 1 kΩ secondary phase loads.",
        "Components": comps,
        "Connections": conns
    }

# ==============================================================================
# 2. eight_bit_cpu.eee
# ==============================================================================
def build_eight_bit_cpu():
    # 8 data bus toggle inputs (DATA0..DATA7) - preset to 0x05 (00000101)
    preset_bits = [True, False, True, False, False, False, False, False]
    data_inputs = []
    for i in range(8):
        inp = make_comp("electronics.logicinput", "LogicInput", f"DATA{i}", f"DATA{i}", f"Data Bus Bit {i}",
                        i + 1, 1,
                        {"Value": "True" if preset_bits[i] else "False"},
                        elec_params={"LogicLevel": 1.0 if preset_bits[i] else 0.0},
                        breaking_current=1.0)
        data_inputs.append(inp)
    
    clk = make_comp("electronics.clock", "Clock", "CLK", "CLK", "Datapath Clock Generator", 9, 1,
                    elec_params={"Frequency": 1.0}, breaking_current=1.0)
    run_en = make_comp("electronics.logicinput", "LogicInput", "RUN", "RUN", "Run / Step Enable", 10, 1,
                       {"Value": "True"}, elec_params={"LogicLevel": 1.0}, breaking_current=1.0)
    
    pc = make_comp("electronics.counter", "Counter", "U_PC", "U_PC", "Program Sequence Counter", 9, 3,
                   {"BitWidth": "8", "InitialValue": "0", "MaxValue": "255"}, breaking_current=1.0)
    
    acc = make_comp("electronics.register", "Register", "U_ACC", "U_ACC", "8-Bit Accumulator Register", 5, 3,
                    {"BitWidth": "8", "InitialValue": "0"}, breaking_current=1.0)
    
    alu = make_comp("arith.adder", "Adder", "U_ALU", "U_ALU", "8-Bit Parallel Binary Adder ALU", 5, 5,
                    {"BitWidth": "8"}, breaking_current=1.0)
    
    hex_lo = make_comp("io.hexdigit", "HexDigit", "HEX_LO", "HEX_LO", "Low Nibble Hex Result Display", 4, 7,
                       breaking_current=1.0)
    hex_hi = make_comp("io.hexdigit", "HexDigit", "HEX_HI", "HEX_HI", "High Nibble Hex Result Display", 2, 7,
                       breaking_current=1.0)
    
    flag_cout = make_comp("electronics.logicoutput", "LogicOutput", "FLAG_C", "FLAG_C", "Carry Out Flag", 7, 7,
                          breaking_current=1.0)
    
    tp_acc0 = make_comp("general.probe", "Probe", "TP_ACC0", "TP_ACC0", "ALU Result Bit 0 Probe", 6, 7,
                        {"ExpectedVoltage": "5.0"}, {"InputImpedance": 1e7}, breaking_current=1.0)
    tp_clk = make_comp("general.probe", "Probe", "TP_CLK", "TP_CLK", "Clock Signal Probe", 9, 2,
                       {"ExpectedVoltage": "5.0"}, {"InputImpedance": 1e7}, breaking_current=1.0)
    
    comps = data_inputs + [clk, run_en, pc, acc, alu, hex_lo, hex_hi, flag_cout, tp_acc0, tp_clk]
    
    conns = [
        # Clock & Run wiring
        make_conn(clk, "out", pc, "CLK", "CLK_NET"),
        make_conn(clk, "out", acc, "CLK", "CLK_NET"),
        make_conn(clk, "out", tp_clk, "in", "CLK_NET"),
        make_conn(run_en, "out", pc, "en", "EN_NET"),
        make_conn(run_en, "out", acc, "en", "EN_NET")
    ]
    
    # Connect DATA inputs to ALU A bus, and ACC Q to ALU B bus, and ALU Y back to ACC D bus
    for i in range(8):
        conns.append(make_conn(data_inputs[i], "out", alu, f"A{i}", f"DATA_BUS_{i}"))
        conns.append(make_conn(acc, f"Q{i}", alu, f"B{i}", f"ACC_Q_{i}"))
        conns.append(make_conn(alu, f"Y{i}", acc, f"D{i}", f"ALU_RES_{i}"))
    
    # Low nibble Y0..Y3 to HEX_LO D0..D3
    for i in range(4):
        conns.append(make_conn(alu, f"Y{i}", hex_lo, f"D{i}", f"ALU_RES_{i}"))
    
    # High nibble Y4..Y7 to HEX_HI D0..D3
    for i in range(4):
        conns.append(make_conn(alu, f"Y{i+4}", hex_hi, f"D{i}", f"ALU_RES_{i+4}"))
    
    conns.append(make_conn(alu, "Cout", flag_cout, "in", "COUT_NET"))
    conns.append(make_conn(alu, "Y0", tp_acc0, "in", "ALU_RES_0"))
    
    return {
        "FileType": "EEE-Circuit",
        "FormatVersion": "1.0",
        "Application": "EEE",
        "Title": "Eight-Bit CPU",
        "Description": "Synchronous eight-bit CPU and accumulator datapath architecture featuring an instruction/sequence counter (program counter), an 8-bit accumulator register, an 8-bit arithmetic adder ALU, interactive operand bus inputs, carry status flag, and dual hexadecimal output displays.",
        "Components": comps,
        "Connections": conns
    }

# ==============================================================================
# 3. three_phase_controlled_rectifier.eee
# ==============================================================================
def build_three_phase_controlled_rectifier():
    gnd = make_comp("electrical.ground", "Ground", "GND", "GND", "Ground Reference", 9, 1, breaking_current=100.0)
    
    # 3-Phase Sources (17V peak = 12V RMS, 50 Hz, 120° offsets)
    va = make_comp("electrical.voltagesource", "VoltageSource", "VA", "VA", "Phase A AC Source", 2, 1,
                   {"WaveformType": "Sine"},
                   {"Voltage": 12.0, "SineOffset": 0.0, "SineAmplitude": 17.0, "SineFrequency": 50.0, "SinePhase": 0.0},
                   breaking_current=50.0)
    vb = make_comp("electrical.voltagesource", "VoltageSource", "VB", "VB", "Phase B AC Source", 4, 1,
                   {"WaveformType": "Sine"},
                   {"Voltage": 12.0, "SineOffset": 0.0, "SineAmplitude": 17.0, "SineFrequency": 50.0, "SinePhase": -120.0},
                   breaking_current=50.0)
    vc = make_comp("electrical.voltagesource", "VoltageSource", "VC", "VC", "Phase C AC Source", 6, 1,
                   {"WaveformType": "Sine"},
                   {"Voltage": 12.0, "SineOffset": 0.0, "SineAmplitude": 17.0, "SineFrequency": 50.0, "SinePhase": 120.0},
                   breaking_current=50.0)
    
    # Small source resistance (0.5 Ω)
    ra = make_comp("electrical.resistor", "Resistor", "RA", "RA", "Phase A Source Resistance", 2, 2,
                   elec_params={"Resistance": 0.5}, breaking_current=20.0)
    rb = make_comp("electrical.resistor", "Resistor", "RB", "RB", "Phase B Source Resistance", 4, 2,
                   elec_params={"Resistance": 0.5}, breaking_current=20.0)
    rc = make_comp("electrical.resistor", "Resistor", "RC", "RC", "Phase C Source Resistance", 6, 2,
                   elec_params={"Resistance": 0.5}, breaking_current=20.0)
    
    # Gate firing pulse sources (50 Hz, 60° firing delay)
    # 50 Hz period = 20 ms. 60° delay = 3.33 ms
    vg_a = make_comp("electrical.voltagesource", "VoltageSource", "V_GATE_A", "V_GATE_A", "SCR Gate Pulse Phase A", 1, 3,
                     {"WaveformType": "Pulse"},
                     {"Voltage": 10.0, "PulseInitial": 0.0, "PulsePulsed": 10.0, "PulseDelay": 0.00333, "PulseRiseTime": 1e-6, "PulseFallTime": 1e-6, "PulseWidth": 0.00667, "PulsePeriod": 0.02},
                     breaking_current=10.0)
    vg_b = make_comp("electrical.voltagesource", "VoltageSource", "V_GATE_B", "V_GATE_B", "SCR Gate Pulse Phase B", 3, 3,
                     {"WaveformType": "Pulse"},
                     {"Voltage": 10.0, "PulseInitial": 0.0, "PulsePulsed": 10.0, "PulseDelay": 0.010, "PulseRiseTime": 1e-6, "PulseFallTime": 1e-6, "PulseWidth": 0.00667, "PulsePeriod": 0.02},
                     breaking_current=10.0)
    vg_c = make_comp("electrical.voltagesource", "VoltageSource", "V_GATE_C", "V_GATE_C", "SCR Gate Pulse Phase C", 5, 3,
                     {"WaveformType": "Pulse"},
                     {"Voltage": 10.0, "PulseInitial": 0.0, "PulsePulsed": 10.0, "PulseDelay": 0.01667, "PulseRiseTime": 1e-6, "PulseFallTime": 1e-6, "PulseWidth": 0.00667, "PulsePeriod": 0.02},
                     breaking_current=10.0)
    
    # Controlled Power Switches (NMOS)
    qa = make_comp("electronics.mosfet", "MOSFET", "QA", "QA", "Phase-Controlled Switch A (NMOS)", 2, 4,
                   breaking_current=30.0)
    qb = make_comp("electronics.mosfet", "MOSFET", "QB", "QB", "Phase-Controlled Switch B (NMOS)", 4, 4,
                   breaking_current=30.0)
    qc = make_comp("electronics.mosfet", "MOSFET", "QC", "QC", "Phase-Controlled Switch C (NMOS)", 6, 4,
                   breaking_current=30.0)
    
    # DC Output Filter Capacitor (220 µF shunt)
    c_dc = make_comp("electrical.capacitor", "Capacitor", "C_DC", "C_DC", "DC Smoothing Reservoir Capacitor", 3, 6,
                     elec_params={"Capacitance": 0.000220}, breaking_current=20.0)
    
    # DC Load Resistor & Lamp
    r_load = make_comp("electrical.resistor", "Resistor", "R_LOAD", "R_LOAD", "DC Power Load Resistor", 5, 6,
                       elec_params={"Resistance": 24.0}, breaking_current=10.0)
    lamp = make_comp("electrical.bulb", "Bulb", "LAMP", "LAMP", "DC Output Indicator Lamp", 7, 6,
                     elec_params={"RatedVoltage": 12.0, "Resistance": 24.0, "ThresholdVoltage": 3.0}, breaking_current=10.0)
    
    # Probes
    tp_a = make_comp("general.probe", "Probe", "TP_A", "TP_A", "Phase A Input Voltage Probe", 1, 2,
                     {"ExpectedVoltage": "17.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_ga = make_comp("general.probe", "Probe", "TP_GATE_A", "TP_GATE_A", "Phase A Gate Pulse Probe", 1, 4,
                      {"ExpectedVoltage": "10.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_dc = make_comp("general.probe", "Probe", "TP_DC", "TP_DC", "Rectified DC Bus Probe", 2, 6,
                      {"ExpectedVoltage": "14.5"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    
    comps = [gnd, va, vb, vc, ra, rb, rc, vg_a, vg_b, vg_c, qa, qb, qc, c_dc, r_load, lamp, tp_a, tp_ga, tp_dc]
    
    conns = [
        # Source negatives & gate generator negatives to GND
        make_conn(va, "negative", gnd, "gnd", "0"),
        make_conn(vb, "negative", gnd, "gnd", "0"),
        make_conn(vc, "negative", gnd, "gnd", "0"),
        make_conn(vg_a, "negative", gnd, "gnd", "0"),
        make_conn(vg_b, "negative", gnd, "gnd", "0"),
        make_conn(vg_c, "negative", gnd, "gnd", "0"),
        
        # Phase A
        make_conn(va, "positive", ra, "pin1", "VA_SRC"),
        make_conn(ra, "pin2", qa, "drain", "VA_NODE"),
        make_conn(ra, "pin2", tp_a, "in", "VA_NODE"),
        make_conn(vg_a, "positive", qa, "gate", "GATE_A"),
        make_conn(vg_a, "positive", tp_ga, "in", "GATE_A"),
        make_conn(qa, "body", gnd, "gnd", "0"),
        
        # Phase B
        make_conn(vb, "positive", rb, "pin1", "VB_SRC"),
        make_conn(rb, "pin2", qb, "drain", "VB_NODE"),
        make_conn(vg_b, "positive", qb, "gate", "GATE_B"),
        make_conn(qb, "body", gnd, "gnd", "0"),
        
        # Phase C
        make_conn(vc, "positive", rc, "pin1", "VC_SRC"),
        make_conn(rc, "pin2", qc, "drain", "VC_NODE"),
        make_conn(vg_c, "positive", qc, "gate", "GATE_C"),
        make_conn(qc, "body", gnd, "gnd", "0"),
        
        # Common Cathode / Source DC output bus
        make_conn(qa, "source", r_load, "pin1", "DC_BUS"),
        make_conn(qb, "source", r_load, "pin1", "DC_BUS"),
        make_conn(qc, "source", r_load, "pin1", "DC_BUS"),
        make_conn(r_load, "pin1", c_dc, "pin1", "DC_BUS"),
        make_conn(r_load, "pin1", lamp, "pin1", "DC_BUS"),
        make_conn(r_load, "pin1", tp_dc, "in", "DC_BUS"),
        
        # Return rails to GND
        make_conn(c_dc, "pin2", gnd, "gnd", "0"),
        make_conn(r_load, "pin2", gnd, "gnd", "0"),
        make_conn(lamp, "pin2", gnd, "gnd", "0")
    ]
    
    return {
        "FileType": "EEE-Circuit",
        "FormatVersion": "1.0",
        "Application": "EEE",
        "Title": "Three-Phase Controlled Rectifier",
        "Description": "Three-phase half-wave phase-controlled rectifier circuit with balanced AC sources (17V peak / 12V RMS, 50 Hz, 120° displacement), three NMOS power switching arms operating with phase firing delay, common star neutral return, shunt DC capacitive reservoir filter (220 µF), 24 Ω power load, and status indicator.",
        "Components": comps,
        "Connections": conns
    }

# ==============================================================================
# 4. full_bridge_smps.eee
# ==============================================================================
def build_full_bridge_smps():
    gnd = make_comp("electrical.ground", "Ground", "GND", "GND", "Common Ground Reference", 8, 1, breaking_current=100.0)
    vin = make_comp("electrical.voltagesource", "VoltageSource", "VIN", "VIN", "24V DC Input Power Rail", 3, 1,
                    {"WaveformType": "DC"}, {"Voltage": 24.0}, breaking_current=50.0)
    
    # 50 kHz complementary PWM drivers (45% duty cycle, 180° out of phase)
    # T = 20 µs, width = 9 µs
    pwm_a = make_comp("electrical.voltagesource", "VoltageSource", "PWM_A", "PWM_A", "50 kHz PWM Diagonal Driver A", 1, 1,
                      {"WaveformType": "Pulse"},
                      {"Voltage": 12.0, "PulseInitial": 0.0, "PulsePulsed": 12.0, "PulseDelay": 0.0, "PulseRiseTime": 1e-7, "PulseFallTime": 1e-7, "PulseWidth": 9e-6, "PulsePeriod": 2e-5},
                      breaking_current=10.0)
    pwm_b = make_comp("electrical.voltagesource", "VoltageSource", "PWM_B", "PWM_B", "50 kHz PWM Diagonal Driver B", 5, 1,
                      {"WaveformType": "Pulse"},
                      {"Voltage": 12.0, "PulseInitial": 0.0, "PulsePulsed": 12.0, "PulseDelay": 1e-5, "PulseRiseTime": 1e-7, "PulseFallTime": 1e-7, "PulseWidth": 9e-6, "PulsePeriod": 2e-5},
                      breaking_current=10.0)
    
    # 4 NMOS switches in H-Bridge configuration
    m1 = make_comp("electronics.mosfet", "MOSFET", "M1", "M1", "H-Bridge High-Side Left Switch", 2, 3, breaking_current=30.0)
    m2 = make_comp("electronics.mosfet", "MOSFET", "M2", "M2", "H-Bridge Low-Side Left Switch", 4, 3, breaking_current=30.0)
    m3 = make_comp("electronics.mosfet", "MOSFET", "M3", "M3", "H-Bridge High-Side Right Switch", 2, 4, breaking_current=30.0)
    m4 = make_comp("electronics.mosfet", "MOSFET", "M4", "M4", "H-Bridge Low-Side Right Switch", 4, 4, breaking_current=30.0)
    
    # 2:1 High-Frequency Isolation Transformer
    xfmr = make_comp("machine.ac-transformer", "ACTransformer", "XFMR", "XFMR", "50 kHz 2:1 Step-Down Transformer", 3, 5,
                     {"TransformerType": "TwoWinding", "WindingConnection": "Single-phase"},
                     {
                         "RatedPrimaryVoltage": 24.0,
                         "RatedSecondaryVoltage": 12.0,
                         "RatedApparentPower": 200.0,
                         "PrimaryTurns": 100,
                         "SecondaryTurns": 50,
                         "Frequency": 50000.0,
                         "PrimaryResistance": 0.05,
                         "SecondaryResistance": 0.02,
                         "PrimaryLeakageInductance": 1e-6,
                         "SecondaryLeakageInductance": 0.5e-6,
                         "MagnetizingInductance": 0.001,
                         "CoreLossResistance": 10000.0,
                         "NumberOfPhases": 1,
                         "NumberOfWindings": 2,
                         "EfficiencyPercent": 96.0
                     },
                     breaking_current=50.0)
    
    # Secondary Full-Bridge Rectifier (4 Schottky diodes)
    d1 = make_comp("electronics.diode", "Diode", "D1", "D1", "Secondary High-Side Rectifier 1", 2, 6, breaking_current=20.0)
    d2 = make_comp("electronics.diode", "Diode", "D2", "D2", "Secondary Low-Side Rectifier 1", 4, 6, breaking_current=20.0)
    d3 = make_comp("electronics.diode", "Diode", "D3", "D3", "Secondary High-Side Rectifier 2", 2, 7, breaking_current=20.0)
    d4 = make_comp("electronics.diode", "Diode", "D4", "D4", "Secondary Low-Side Rectifier 2", 4, 7, breaking_current=20.0)
    
    # Buck Output Filter (L = 100 µH, C = 220 µF)
    l_out = make_comp("electrical.inductor", "Inductor", "L_OUT", "L_OUT", "Buck Output Smoothing Inductor", 2, 8,
                      elec_params={"Inductance": 0.000100}, breaking_current=20.0)
    c_out = make_comp("electrical.capacitor", "Capacitor", "C_OUT", "C_OUT", "Shunt Output Filter Capacitor", 4, 8,
                      elec_params={"Capacitance": 0.000220}, breaking_current=20.0)
    
    # Load & Indicator
    r_load = make_comp("electrical.resistor", "Resistor", "R_LOAD", "R_LOAD", "DC Power Load", 3, 9,
                       elec_params={"Resistance": 10.0}, breaking_current=10.0)
    lamp = make_comp("electrical.bulb", "Bulb", "LAMP", "LAMP", "Output Status Indicator Lamp", 5, 9,
                     elec_params={"RatedVoltage": 12.0, "Resistance": 24.0, "ThresholdVoltage": 3.0}, breaking_current=10.0)
    
    # Probes
    tp_vin = make_comp("general.probe", "Probe", "TP_VIN", "TP_VIN", "24V Input Rail Probe", 2, 1,
                       {"ExpectedVoltage": "24.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_br = make_comp("general.probe", "Probe", "TP_BRIDGE_A", "TP_BRIDGE_A", "Bridge Midpoint Node A Probe", 1, 3,
                      {"ExpectedVoltage": "24.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_sec = make_comp("general.probe", "Probe", "TP_SEC", "TP_SEC", "Secondary AC Output Probe", 1, 6,
                       {"ExpectedVoltage": "12.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_vout = make_comp("general.probe", "Probe", "TP_VOUT", "TP_VOUT", "Regulated 10.8V DC Output Probe", 2, 9,
                        {"ExpectedVoltage": "10.8"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    
    comps = [gnd, vin, pwm_a, pwm_b, m1, m2, m3, m4, xfmr, d1, d2, d3, d4, l_out, c_out, r_load, lamp, tp_vin, tp_br, tp_sec, tp_vout]
    
    conns = [
        # DC Input & Drivers to GND
        make_conn(vin, "negative", gnd, "gnd", "0"),
        make_conn(vin, "positive", tp_vin, "in", "VIN_NET"),
        make_conn(vin, "positive", m1, "drain", "VIN_NET"),
        make_conn(vin, "positive", m3, "drain", "VIN_NET"),
        make_conn(pwm_a, "negative", gnd, "gnd", "0"),
        make_conn(pwm_b, "negative", gnd, "gnd", "0"),
        
        # Diagonal Gate Drives
        make_conn(pwm_a, "positive", m1, "gate", "GATE_A"),
        make_conn(pwm_a, "positive", m4, "gate", "GATE_A"),
        make_conn(pwm_b, "positive", m3, "gate", "GATE_B"),
        make_conn(pwm_b, "positive", m2, "gate", "GATE_B"),
        
        # Low side sources to GND
        make_conn(m2, "source", gnd, "gnd", "0"),
        make_conn(m2, "body", gnd, "gnd", "0"),
        make_conn(m4, "source", gnd, "gnd", "0"),
        make_conn(m4, "body", gnd, "gnd", "0"),
        
        # Bridge Midpoints across Transformer Primary
        make_conn(m1, "source", m2, "drain", "MID_A"),
        make_conn(m1, "source", xfmr, "primary_dot", "MID_A"),
        make_conn(m1, "source", tp_br, "in", "MID_A"),
        make_conn(m3, "source", m4, "drain", "MID_B"),
        make_conn(m3, "source", xfmr, "primary", "MID_B"),
        
        # Transformer Secondary to Diode Bridge
        make_conn(xfmr, "secondary_dot", d1, "anode", "SEC_DOT"),
        make_conn(xfmr, "secondary_dot", d2, "cathode", "SEC_DOT"),
        make_conn(xfmr, "secondary_dot", tp_sec, "in", "SEC_DOT"),
        make_conn(xfmr, "secondary", d3, "anode", "SEC_RET"),
        make_conn(xfmr, "secondary", d4, "cathode", "SEC_RET"),
        make_conn(d2, "anode", gnd, "gnd", "0"),
        make_conn(d4, "anode", gnd, "gnd", "0"),
        
        # Rectifier Output to Buck Filter
        make_conn(d1, "cathode", l_out, "pin1", "RECT_OUT"),
        make_conn(d3, "cathode", l_out, "pin1", "RECT_OUT"),
        
        # LC Buck Filter to Load
        make_conn(l_out, "pin2", c_out, "pin1", "VOUT_NET"),
        make_conn(l_out, "pin2", r_load, "pin1", "VOUT_NET"),
        make_conn(l_out, "pin2", lamp, "pin1", "VOUT_NET"),
        make_conn(l_out, "pin2", tp_vout, "in", "VOUT_NET"),
        
        # Filter and Load returns to GND
        make_conn(c_out, "pin2", gnd, "gnd", "0"),
        make_conn(r_load, "pin2", gnd, "gnd", "0"),
        make_conn(lamp, "pin2", gnd, "gnd", "0")
    ]
    
    return {
        "FileType": "EEE-Circuit",
        "FormatVersion": "1.0",
        "Application": "EEE",
        "Title": "Full-Bridge SMPS",
        "Description": "High-efficiency isolated full-bridge (H-bridge) switch-mode power supply (SMPS) converting 24V DC to a regulated 10.8V DC output, featuring four NMOS power MOSFETs driven diagonally at 50 kHz, 2:1 high-frequency step-down isolation transformer, fast secondary full-bridge Schottky diode rectifier, buck LC filter (100 µH inductor, 220 µF shunt capacitor), and 10 Ω load with status indicator.",
        "Components": comps,
        "Connections": conns
    }

# ==============================================================================
# 5. push_pull_converter.eee
# ==============================================================================
def build_push_pull_converter():
    gnd = make_comp("electrical.ground", "Ground", "GND", "GND", "Common Ground Reference", 8, 1, breaking_current=100.0)
    vin = make_comp("electrical.voltagesource", "VoltageSource", "VIN", "VIN", "24V DC Input Power Rail", 4, 1,
                    {"WaveformType": "DC"}, {"Voltage": 24.0}, breaking_current=50.0)
    
    # Dual 50 kHz pulse drives (42% duty cycle, 180° out of phase)
    # T = 20 µs, width = 8.4 µs
    pwm1 = make_comp("electrical.voltagesource", "VoltageSource", "PWM1", "PWM1", "Push-Pull Gate Drive Phase 1", 2, 1,
                     {"WaveformType": "Pulse"},
                     {"Voltage": 12.0, "PulseInitial": 0.0, "PulsePulsed": 12.0, "PulseDelay": 0.0, "PulseRiseTime": 1e-7, "PulseFallTime": 1e-7, "PulseWidth": 8.4e-6, "PulsePeriod": 2e-5},
                     breaking_current=10.0)
    pwm2 = make_comp("electrical.voltagesource", "VoltageSource", "PWM2", "PWM2", "Push-Pull Gate Drive Phase 2", 6, 1,
                     {"WaveformType": "Pulse"},
                     {"Voltage": 12.0, "PulseInitial": 0.0, "PulsePulsed": 12.0, "PulseDelay": 1e-5, "PulseRiseTime": 1e-7, "PulseFallTime": 1e-7, "PulseWidth": 8.4e-6, "PulsePeriod": 2e-5},
                     breaking_current=10.0)
    
    # Dual Low-Side NMOS Switches
    m1 = make_comp("electronics.mosfet", "MOSFET", "M1", "M1", "Push-Pull Low-Side NMOS Switch 1", 3, 3, breaking_current=30.0)
    m2 = make_comp("electronics.mosfet", "MOSFET", "M2", "M2", "Push-Pull Low-Side NMOS Switch 2", 5, 3, breaking_current=30.0)
    
    # Center-Tapped Transformer Stage (Modeled via coupled inductors: L_PRI1, L_PRI2, L_SEC with Mutual Inductance)
    # L_PRI1 = 100 µH, L_PRI2 = 100 µH, L_SEC = 25 µH (2:1 turns ratio)
    l_pri1 = make_comp("electrical.inductor", "Inductor", "L_PRI1", "L_PRI1", "Transformer Primary Upper Half", 2, 4,
                       elec_params={"Inductance": 0.000100}, breaking_current=20.0)
    l_pri2 = make_comp("electrical.inductor", "Inductor", "L_PRI2", "L_PRI2", "Transformer Primary Lower Half", 6, 4,
                       elec_params={"Inductance": 0.000100}, breaking_current=20.0)
    l_sec = make_comp("electrical.inductor", "Inductor", "L_SEC", "L_SEC", "Transformer Secondary Winding", 4, 5,
                      elec_params={"Inductance": 0.000025}, breaking_current=20.0)
    
    k_pri = make_comp("electrical.mutualinductance", "MutualInductance", "K_PRI", "K_PRI", "Primary Inter-Winding Coupling", 4, 4,
                      custom_props={"Inductor1": "L_PRI1", "Inductor2": "L_PRI2", "CouplingFactor": 0.999}, breaking_current=10.0)
    k_sec1 = make_comp("electrical.mutualinductance", "MutualInductance", "K_SEC1", "K_SEC1", "Primary 1 to Secondary Coupling", 3, 5,
                       custom_props={"Inductor1": "L_PRI1", "Inductor2": "L_SEC", "CouplingFactor": 0.995}, breaking_current=10.0)
    k_sec2 = make_comp("electrical.mutualinductance", "MutualInductance", "K_SEC2", "K_SEC2", "Primary 2 to Secondary Coupling", 5, 5,
                       custom_props={"Inductor1": "L_PRI2", "Inductor2": "L_SEC", "CouplingFactor": 0.995}, breaking_current=10.0)
    
    # Secondary Full-Wave Rectifier Diodes
    d1 = make_comp("electronics.diode", "Diode", "D1", "D1", "Secondary Rectifier Diode 1", 3, 6, breaking_current=20.0)
    d2 = make_comp("electronics.diode", "Diode", "D2", "D2", "Secondary Rectifier Diode 2", 5, 6, breaking_current=20.0)
    
    # Buck Output Filter (L = 150 µH, C = 100 µF)
    l_out = make_comp("electrical.inductor", "Inductor", "L_OUT", "L_OUT", "Buck Output Smoothing Inductor", 4, 7,
                      elec_params={"Inductance": 0.000150}, breaking_current=20.0)
    c_out = make_comp("electrical.capacitor", "Capacitor", "C_OUT", "C_OUT", "Shunt Output Filter Capacitor", 6, 7,
                      elec_params={"Capacitance": 0.000100}, breaking_current=20.0)
    
    # Output Load & Indicator
    r_load = make_comp("electrical.resistor", "Resistor", "R_LOAD", "R_LOAD", "DC Power Load", 4, 8,
                       elec_params={"Resistance": 10.0}, breaking_current=10.0)
    lamp = make_comp("electrical.bulb", "Bulb", "LAMP", "LAMP", "Output Status Indicator Lamp", 6, 8,
                     elec_params={"RatedVoltage": 12.0, "Resistance": 24.0, "ThresholdVoltage": 3.0}, breaking_current=10.0)
    
    # Probes
    tp_vin = make_comp("general.probe", "Probe", "TP_VIN", "TP_VIN", "24V Input Rail Probe", 3, 1,
                       {"ExpectedVoltage": "24.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_dr1 = make_comp("general.probe", "Probe", "TP_DRAIN1", "TP_DRAIN1", "M1 Drain Switching Node Probe", 2, 3,
                       {"ExpectedVoltage": "48.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_dr2 = make_comp("general.probe", "Probe", "TP_DRAIN2", "TP_DRAIN2", "M2 Drain Switching Node Probe", 6, 3,
                       {"ExpectedVoltage": "48.0"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    tp_vout = make_comp("general.probe", "Probe", "TP_VOUT", "TP_VOUT", "Regulated 9.5V DC Output Probe", 3, 8,
                        {"ExpectedVoltage": "9.5"}, {"InputImpedance": 1e7}, breaking_current=10.0)
    
    comps = [gnd, vin, pwm1, pwm2, m1, m2, l_pri1, l_pri2, l_sec, k_pri, k_sec1, k_sec2, d1, d2, l_out, c_out, r_load, lamp, tp_vin, tp_dr1, tp_dr2, tp_vout]
    
    conns = [
        # Supply & Pulse Grounds
        make_conn(vin, "negative", gnd, "gnd", "0"),
        make_conn(pwm1, "negative", gnd, "gnd", "0"),
        make_conn(pwm2, "negative", gnd, "gnd", "0"),
        make_conn(m1, "source", gnd, "gnd", "0"),
        make_conn(m1, "body", gnd, "gnd", "0"),
        make_conn(m2, "source", gnd, "gnd", "0"),
        make_conn(m2, "body", gnd, "gnd", "0"),
        
        # VIN Rail to Center Tap (L_PRI1 pin2 and L_PRI2 pin1)
        make_conn(vin, "positive", tp_vin, "in", "VIN_NET"),
        make_conn(vin, "positive", l_pri1, "pin2", "VIN_NET"),
        make_conn(vin, "positive", l_pri2, "pin1", "VIN_NET"),
        
        # Gate Drives to Switches
        make_conn(pwm1, "positive", m1, "gate", "GATE1"),
        make_conn(pwm2, "positive", m2, "gate", "GATE2"),
        
        # Primary Outer Terminals to MOSFET Drains
        make_conn(m1, "drain", l_pri1, "pin1", "DRAIN1"),
        make_conn(m1, "drain", tp_dr1, "in", "DRAIN1"),
        make_conn(m2, "drain", l_pri2, "pin2", "DRAIN2"),
        make_conn(m2, "drain", tp_dr2, "in", "DRAIN2"),
        
        # Secondary Winding to Full-Wave Rectifier
        make_conn(l_sec, "pin1", d1, "anode", "SEC_HI"),
        make_conn(l_sec, "pin2", d2, "anode", "SEC_LO"),
        
        # Rectifier Cathodes to LC Output Filter
        make_conn(d1, "cathode", l_out, "pin1", "RECT_OUT"),
        make_conn(d2, "cathode", l_out, "pin1", "RECT_OUT"),
        
        # LC Filter to Load & Indicator
        make_conn(l_out, "pin2", c_out, "pin1", "VOUT_NET"),
        make_conn(l_out, "pin2", r_load, "pin1", "VOUT_NET"),
        make_conn(l_out, "pin2", lamp, "pin1", "VOUT_NET"),
        make_conn(l_out, "pin2", tp_vout, "in", "VOUT_NET"),
        
        # Return paths to GND
        make_conn(c_out, "pin2", gnd, "gnd", "0"),
        make_conn(r_load, "pin2", gnd, "gnd", "0"),
        make_conn(lamp, "pin2", gnd, "gnd", "0")
    ]
    
    return {
        "FileType": "EEE-Circuit",
        "FormatVersion": "1.0",
        "Application": "EEE",
        "Title": "Push-Pull Converter",
        "Description": "Isolated push-pull DC-DC converter stepping down a 24V DC rail to a regulated 9.5V DC output, featuring dual low-side NMOS power switches driven alternately at 50 kHz (D = 0.42), center-tapped isolation transformer with primary center tap tied to VIN, fast full-wave secondary Schottky rectification, buck LC output filter (150 µH inductor, 100 µF shunt capacitor), and 10 Ω load with status indicator.",
        "Components": comps,
        "Connections": conns
    }

# Execute build
builders = {
    "three_phase_transformer.eee": build_three_phase_transformer,
    "eight_bit_cpu.eee": build_eight_bit_cpu,
    "three_phase_controlled_rectifier.eee": build_three_phase_controlled_rectifier,
    "full_bridge_smps.eee": build_full_bridge_smps,
    "push_pull_converter.eee": build_push_pull_converter
}

for fname, bfunc in builders.items():
    circuit_data = bfunc()
    out_path = os.path.join("Resources", "Raw", "Examples", fname)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(circuit_data, f, indent=2)
    print(f"Generated {fname}: {len(circuit_data['Components'])} comps, {len(circuit_data['Connections'])} conns.")

