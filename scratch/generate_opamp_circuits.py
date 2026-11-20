import json
import uuid
from pathlib import Path

# Deterministic namespace for reproducible IDs
NAMESPACE = uuid.UUID("11223344-5566-7788-99aa-bbccddeeff00")

def make_id(name: str) -> str:
    return str(uuid.uuid5(NAMESPACE, name))

def create_component(stable_type: str, comp_type: str, ref: str, name: str, desc: str,
                     row: int, col: int, elec_params: dict = None, str_params: dict = None,
                     custom_props: dict = None, breaking_current: float = 10.0):
    sparams = {"LogicalRow": str(row), "LogicalColumn": str(col)}
    if str_params:
        sparams.update(str_params)
    return {
        "Id": make_id(name),
        "StableTypeId": stable_type,
        "ComponentType": comp_type,
        "Reference": ref,
        "Name": name,
        "Description": desc,
        "StringParameters": sparams,
        "Rotation": 0.0,
        "ScaleX": 1.0,
        "ScaleY": 1.0,
        "BreakingCurrent": breaking_current,
        "ElectricalParameters": elec_params or {},
        "CustomProperties": custom_props or {}
    }

def create_probe(ref: str, name: str, desc: str, row: int, col: int,
                 expected_voltage: str = None, min_v: str = None, max_v: str = None,
                 array_output: bool = False, time_start: float = 0.0,
                 time_duration: float = 0.01, scale_factor: int = 1, max_readings: int = 50):
    str_params = {
        "MeasurementMode": "Voltage",
        "Unit": "V",
        "LogicalRow": str(row),
        "LogicalColumn": str(col)
    }
    cust_props = {
        "MeasurementMode": "Voltage",
        "Unit": "V"
    }
    if expected_voltage is not None:
        str_params["ExpectedVoltage"] = expected_voltage
        cust_props["ExpectedVoltage"] = expected_voltage
    if min_v is not None and max_v is not None:
        str_params["ExpectedVoltageMinimum"] = min_v
        str_params["ExpectedVoltageMaximum"] = max_v
        cust_props["ExpectedVoltageMinimum"] = min_v
        cust_props["ExpectedVoltageMaximum"] = max_v
    if array_output:
        str_params["ArrayOutputEnabled"] = "True"
        str_params["TimeRangeStart"] = str(time_start)
        str_params["TimeRangeDuration"] = str(time_duration)
        str_params["TimeScaleFactor"] = str(scale_factor)
        str_params["MaxReadings"] = str(max_readings)
        str_params["RealtimeArray"] = "False"
        cust_props["ArrayOutputEnabled"] = "True"
        cust_props["TimeRangeStart"] = str(time_start)
        cust_props["TimeRangeDuration"] = str(time_duration)
        cust_props["TimeScaleFactor"] = str(scale_factor)
        cust_props["MaxReadings"] = str(max_readings)
        cust_props["RealtimeArray"] = "False"

    return {
        "Id": make_id(name),
        "StableTypeId": "general.probe",
        "ComponentType": "Probe",
        "Reference": ref,
        "Name": name,
        "Description": desc,
        "StringParameters": str_params,
        "Rotation": 0.0,
        "ScaleX": 1.0,
        "ScaleY": 1.0,
        "BreakingCurrent": 10.0,
        "ElectricalParameters": {
            "InputImpedance": 10000000.0
        },
        "CustomProperties": cust_props
    }

def create_connection(conn_id_name: str, from_name: str, from_pin: str,
                      to_name: str, to_pin: str, net_name: str):
    return {
        "Id": make_id(conn_id_name),
        "FromComponentId": make_id(from_name),
        "FromPin": from_pin,
        "ToComponentId": make_id(to_name),
        "ToPin": to_pin,
        "NetName": net_name,
        "Resistance": 0.0
    }

def save_eee(filename: str, title: str, desc: str, components: list, connections: list, settings: dict = None):
    data = {
        "FileType": "EEE_PLATFORM_SCHEMATIC",
        "FormatVersion": "1.1.0",
        "Application": "EESimulator",
        "Title": title,
        "Description": desc,
        "LogicalGridRowSpacing": 100.0,
        "LogicalGridColumnSpacing": 100.0,
        "Author": "EESimulator Examples",
        "CreatedAt": "2026-11-20T16:00:00Z",
        "SavedAt": "2026-11-20T16:00:00Z",
        "Revision": "1",
        "Tags": ["OpAmp", "Amplifier", "Educational", "Analog"],
        "SimulationSettings": settings or {
            "AnalysisType": "OperatingPoint",
            "TransientStartTime": 0.0,
            "TransientStopTime": 0.01,
            "TransientStepTime": 1e-05,
            "AcSweepType": "Decade",
            "AcPoints": 10,
            "AcStartFrequency": 1.0,
            "AcStopFrequency": 100000.0,
            "DcSweepSource": "V1",
            "DcStart": 0.0,
            "DcStop": 5.0,
            "DcStep": 0.1
        },
        "Components": components,
        "Connections": connections
    }
    out_path = Path("Resources/Raw/Examples") / filename
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
    print(f"Saved {out_path} ({len(components)} components, {len(connections)} connections)")

# -------------------------------------------------------------------------
# 1. Inverting Amplifier
# -------------------------------------------------------------------------
def generate_inverting_amplifier():
    pfx = "inv_amp_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN", pfx+"V_IN", "1.0V DC Input Signal",
                         3, 1, elec_params={"Voltage": 1.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.resistor", "Resistor", "R_IN", pfx+"R_IN", "Input Resistor (10 kOhm)",
                         3, 2, elec_params={"Resistance": 10000.0}),
        create_component("electrical.resistor", "Resistor", "R_FB", pfx+"R_FB", "Feedback Resistor (20 kOhm, Gain = -2.0)",
                         2, 3, elec_params={"Resistance": 20000.0}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Inverting Stage)",
                         3, 3, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Output Load Resistor (10 kOhm)",
                         4, 5, elec_params={"Resistance": 10000.0}),
        # Probes
        create_probe("TP_IN", pfx+"TP_IN", "Input Voltage Test Point (1.0 V)", 2, 1,
                     expected_voltage="1.0", min_v="0.98", max_v="1.02"),
        create_probe("TP_VIRTUAL_GND", pfx+"TP_VIRTUAL_GND", "Inverting Summing Junction Virtual Ground (0.0 V)", 2, 2,
                     expected_voltage="0.0", min_v="-0.01", max_v="0.01"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Inverted Output Test Point (-2.0 V, Gain = -2.0)", 3, 5,
                     expected_voltage="-2.0", min_v="-2.05", max_v="-1.95", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies: V_POS pos -> U1:v_pos, neg -> GND; V_NEG pos -> GND, neg -> U1:v_neg
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Input: V_IN pos -> R_IN:pin1, neg -> GND
        create_connection(pfx+"c5", pfx+"V_IN", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_IN", "positive", pfx+"R_IN", "pin1", "NET_IN"),
        # Op-Amp Non-inverting input grounded
        create_connection(pfx+"c7", pfx+"U1", "in_pos", pfx+"GND", "gnd", "0"),
        # Inverting summing junction: R_IN:pin2 -> U1:in_neg, R_FB:pin1 -> U1:in_neg
        create_connection(pfx+"c8", pfx+"R_IN", "pin2", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c9", pfx+"R_FB", "pin1", pfx+"U1", "in_neg", "NET_INV"),
        # Output: U1:out -> R_FB:pin2, U1:out -> R_LOAD:pin1
        create_connection(pfx+"c10", pfx+"U1", "out", pfx+"R_FB", "pin2", "NET_OUT"),
        create_connection(pfx+"c11", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        # Load ground: R_LOAD:pin2 -> GND
        create_connection(pfx+"c12", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c13", pfx+"TP_IN", "in", pfx+"V_IN", "positive", "NET_IN"),
        create_connection(pfx+"c14", pfx+"TP_VIRTUAL_GND", "in", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c15", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c16", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c17", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("inverting_amplifier.eee", "Inverting Amplifier",
             "Classic inverting operational amplifier stage with dual split DC supply (+15V, -15V, middle GND reference), input resistor Rin = 10 kOhm, feedback resistor Rf = 20 kOhm, gain Av = -Rf/Rin = -2.0, virtual ground summing junction, and 10 kOhm load.",
             comps, conns)

# -------------------------------------------------------------------------
# 2. Non-Inverting Amplifier
# -------------------------------------------------------------------------
def generate_non_inverting_amplifier():
    pfx = "noninv_amp_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN", pfx+"V_IN", "1.0V DC Non-Inverting Input Signal",
                         2, 1, elec_params={"Voltage": 1.0}, str_params={"WaveformType": "DC"}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Non-Inverting Stage)",
                         3, 3, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R1", pfx+"R1", "Feedback Divider Shunt Resistor (10 kOhm)",
                         4, 2, elec_params={"Resistance": 10000.0}),
        create_component("electrical.resistor", "Resistor", "R2", pfx+"R2", "Feedback Divider Series Resistor (20 kOhm, Gain = 1 + R2/R1 = 3.0)",
                         3, 2, elec_params={"Resistance": 20000.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Output Load Resistor (10 kOhm)",
                         4, 5, elec_params={"Resistance": 10000.0}),
        # Probes
        create_probe("TP_IN", pfx+"TP_IN", "Non-Inverting Input Test Point (1.0 V)", 1, 1,
                     expected_voltage="1.0", min_v="0.98", max_v="1.02"),
        create_probe("TP_FEEDBACK", pfx+"TP_FEEDBACK", "Feedback Node Test Point (1.0 V, Virtual Short)", 4, 3,
                     expected_voltage="1.0", min_v="0.98", max_v="1.02"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Non-Inverted Output Test Point (3.0 V, Gain = 3.0)", 3, 5,
                     expected_voltage="3.0", min_v="2.95", max_v="3.05", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Input directly to non-inverting (+) input
        create_connection(pfx+"c5", pfx+"V_IN", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_IN", "positive", pfx+"U1", "in_pos", "NET_IN"),
        # Feedback divider: R1 from GND to in_neg; R2 from in_neg to out
        create_connection(pfx+"c7", pfx+"R1", "pin1", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c8", pfx+"R1", "pin2", pfx+"U1", "in_neg", "NET_FB"),
        create_connection(pfx+"c9", pfx+"R2", "pin1", pfx+"U1", "in_neg", "NET_FB"),
        create_connection(pfx+"c10", pfx+"R2", "pin2", pfx+"U1", "out", "NET_OUT"),
        # Output load
        create_connection(pfx+"c11", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        create_connection(pfx+"c12", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c13", pfx+"TP_IN", "in", pfx+"U1", "in_pos", "NET_IN"),
        create_connection(pfx+"c14", pfx+"TP_FEEDBACK", "in", pfx+"U1", "in_neg", "NET_FB"),
        create_connection(pfx+"c15", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c16", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c17", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("non_inverting_amplifier.eee", "Non-Inverting Amplifier",
             "Non-inverting operational amplifier stage with dual split DC supplies (+15V, -15V, middle GND reference), input signal applied directly to (+) input, resistor divider feedback (R1 = 10 kOhm, R2 = 20 kOhm) on (-) input, closed-loop gain Av = 1 + R2/R1 = 3.0, and 10 kOhm load.",
             comps, conns)

# -------------------------------------------------------------------------
# 3. Voltage Follower (Buffer)
# -------------------------------------------------------------------------
def generate_voltage_follower():
    pfx = "buffer_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN", pfx+"V_IN", "2.5V DC Input Voltage",
                         2, 1, elec_params={"Voltage": 2.5}, str_params={"WaveformType": "DC"}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Unity-Gain Buffer)",
                         3, 3, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Heavy Output Load Resistor (1 kOhm)",
                         4, 5, elec_params={"Resistance": 1000.0}),
        # Probes
        create_probe("TP_IN", pfx+"TP_IN", "High-Impedance Input Test Point (2.5 V)", 1, 1,
                     expected_voltage="2.5", min_v="2.48", max_v="2.52"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Low-Impedance Buffered Output Test Point (2.5 V, Av = 1.0)", 3, 5,
                     expected_voltage="2.5", min_v="2.48", max_v="2.52", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Input to non-inverting (+)
        create_connection(pfx+"c5", pfx+"V_IN", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_IN", "positive", pfx+"U1", "in_pos", "NET_IN"),
        # Direct negative feedback: U1:out to U1:in_neg
        create_connection(pfx+"c7", pfx+"U1", "out", pfx+"U1", "in_neg", "NET_OUT"),
        # Load
        create_connection(pfx+"c8", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        create_connection(pfx+"c9", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c10", pfx+"TP_IN", "in", pfx+"U1", "in_pos", "NET_IN"),
        create_connection(pfx+"c11", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c12", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c13", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("voltage_follower.eee", "Voltage Follower",
             "Unity-gain voltage follower (buffer) amplifier with dual split DC supply (+15V, -15V, middle GND reference), direct feedback connection from output to (-) input (Av = 1.0, Vout = Vin = 2.5V), high input impedance preventing source loading, and low output impedance driving 1 kOhm load.",
             comps, conns)

# -------------------------------------------------------------------------
# 4. Summing Amplifier
# -------------------------------------------------------------------------
def generate_summing_amplifier():
    pfx = "sum_amp_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN1", pfx+"V_IN1", "Channel 1 Input (1.0 V DC)",
                         2, 1, elec_params={"Voltage": 1.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN2", pfx+"V_IN2", "Channel 2 Input (2.0 V DC)",
                         4, 1, elec_params={"Voltage": 2.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.resistor", "Resistor", "R1", pfx+"R1", "Channel 1 Weight Resistor (10 kOhm)",
                         2, 2, elec_params={"Resistance": 10000.0}),
        create_component("electrical.resistor", "Resistor", "R2", pfx+"R2", "Channel 2 Weight Resistor (10 kOhm)",
                         4, 2, elec_params={"Resistance": 10000.0}),
        create_component("electrical.resistor", "Resistor", "R_FB", pfx+"R_FB", "Summing Feedback Resistor (10 kOhm)",
                         1, 3, elec_params={"Resistance": 10000.0}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Summing Stage)",
                         3, 3, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Output Load Resistor (10 kOhm)",
                         4, 5, elec_params={"Resistance": 10000.0}),
        # Probes
        create_probe("TP_IN1", pfx+"TP_IN1", "Channel 1 Input Probe (1.0 V)", 1, 1,
                     expected_voltage="1.0", min_v="0.98", max_v="1.02"),
        create_probe("TP_IN2", pfx+"TP_IN2", "Channel 2 Input Probe (2.0 V)", 5, 1,
                     expected_voltage="2.0", min_v="1.98", max_v="2.02"),
        create_probe("TP_SUM", pfx+"TP_SUM", "Summing Junction Virtual Ground Probe (0.0 V)", 3, 2,
                     expected_voltage="0.0", min_v="-0.01", max_v="0.01"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Weighted Sum Output Probe (-3.0 V = -(V1 + V2))", 3, 5,
                     expected_voltage="-3.0", min_v="-3.05", max_v="-2.95", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Input sources grounded
        create_connection(pfx+"c5", pfx+"V_IN1", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_IN2", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c7", pfx+"V_IN1", "positive", pfx+"R1", "pin1", "NET_IN1"),
        create_connection(pfx+"c8", pfx+"V_IN2", "positive", pfx+"R2", "pin1", "NET_IN2"),
        # Summing junction at in_neg
        create_connection(pfx+"c9", pfx+"R1", "pin2", pfx+"U1", "in_neg", "NET_SUM"),
        create_connection(pfx+"c10", pfx+"R2", "pin2", pfx+"U1", "in_neg", "NET_SUM"),
        create_connection(pfx+"c11", pfx+"R_FB", "pin1", pfx+"U1", "in_neg", "NET_SUM"),
        # Non-inverting input grounded
        create_connection(pfx+"c12", pfx+"U1", "in_pos", pfx+"GND", "gnd", "0"),
        # Output
        create_connection(pfx+"c13", pfx+"U1", "out", pfx+"R_FB", "pin2", "NET_OUT"),
        create_connection(pfx+"c14", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        create_connection(pfx+"c15", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c16", pfx+"TP_IN1", "in", pfx+"V_IN1", "positive", "NET_IN1"),
        create_connection(pfx+"c17", pfx+"TP_IN2", "in", pfx+"V_IN2", "positive", "NET_IN2"),
        create_connection(pfx+"c18", pfx+"TP_SUM", "in", pfx+"U1", "in_neg", "NET_SUM"),
        create_connection(pfx+"c19", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c20", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c21", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("summing_amplifier.eee", "Summing Amplifier",
             "Multi-input operational amplifier summing amplifier (audio/signal mixer) with dual split DC supply (+15V, -15V, middle GND reference), two weighted channel inputs (V1 = 1.0V, V2 = 2.0V, R1 = R2 = 10 kOhm), feedback resistor Rf = 10 kOhm producing inverted sum Vout = -(V1 + V2) = -3.0V at virtual ground summing junction.",
             comps, conns)

# -------------------------------------------------------------------------
# 5. Op-Amp Integrator
# -------------------------------------------------------------------------
def generate_opamp_integrator():
    pfx = "integ_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN", pfx+"V_IN", "0.5V DC Input Step Signal",
                         3, 1, elec_params={"Voltage": 0.5}, str_params={"WaveformType": "DC"}),
        create_component("electrical.resistor", "Resistor", "R_IN", pfx+"R_IN", "Integrator Input Resistor (10 kOhm)",
                         3, 2, elec_params={"Resistance": 10000.0}),
        create_component("electrical.capacitor", "Capacitor", "C_FB", pfx+"C_FB", "Feedback Integrating Capacitor (1.0 uF)",
                         2, 3, elec_params={"Capacitance": 1e-6}),
        create_component("electrical.resistor", "Resistor", "R_SHUNT", pfx+"R_SHUNT", "Parallel DC Bleed Stabilization Resistor (100 kOhm)",
                         1, 3, elec_params={"Resistance": 100000.0}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Integrator Stage)",
                         3, 3, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Output Load Resistor (10 kOhm)",
                         4, 5, elec_params={"Resistance": 10000.0}),
        # Probes
        create_probe("TP_IN", pfx+"TP_IN", "Integrator Step Input Probe (0.5 V)", 2, 1,
                     expected_voltage="0.5", min_v="0.48", max_v="0.52"),
        create_probe("TP_INV", pfx+"TP_INV", "Inverting Summing Junction Virtual Ground (0.0 V)", 2, 2,
                     expected_voltage="0.0", min_v="-0.01", max_v="0.01"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Integrated Linear Ramp Output Probe (-5.0 V DC operating point)", 3, 5,
                     expected_voltage="-5.0", min_v="-5.1", max_v="-4.9", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Input
        create_connection(pfx+"c5", pfx+"V_IN", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_IN", "positive", pfx+"R_IN", "pin1", "NET_IN"),
        # Non-inverting input grounded
        create_connection(pfx+"c7", pfx+"U1", "in_pos", pfx+"GND", "gnd", "0"),
        # Inverting summing junction: R_IN:pin2, C_FB:pin1, R_SHUNT:pin1 -> U1:in_neg
        create_connection(pfx+"c8", pfx+"R_IN", "pin2", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c9", pfx+"C_FB", "pin1", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c10", pfx+"R_SHUNT", "pin1", pfx+"U1", "in_neg", "NET_INV"),
        # Output junction: U1:out -> C_FB:pin2, R_SHUNT:pin2, R_LOAD:pin1
        create_connection(pfx+"c11", pfx+"U1", "out", pfx+"C_FB", "pin2", "NET_OUT"),
        create_connection(pfx+"c12", pfx+"U1", "out", pfx+"R_SHUNT", "pin2", "NET_OUT"),
        create_connection(pfx+"c13", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        create_connection(pfx+"c14", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c15", pfx+"TP_IN", "in", pfx+"V_IN", "positive", "NET_IN"),
        create_connection(pfx+"c16", pfx+"TP_INV", "in", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c17", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c18", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c19", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("opamp_integrator.eee", "Op-Amp Integrator",
             "Operational amplifier practical integrator circuit with dual split DC supply (+15V, -15V, middle GND reference), input resistor Rin = 10 kOhm, feedback capacitor Cf = 1.0 uF, parallel DC bleed resistor Rshunt = 100 kOhm for practical DC operating point stability (Vout = -5.0V), ramp rate dV/dt = -Vin/(Rin*Cf) = -50 V/s, and 10 kOhm load.",
             comps, conns)

# -------------------------------------------------------------------------
# 6. Op-Amp Differentiator
# -------------------------------------------------------------------------
def generate_opamp_differentiator():
    pfx = "diff_amp_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN", pfx+"V_IN", "1.0V DC Input Signal",
                         3, 1, elec_params={"Voltage": 1.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.capacitor", "Capacitor", "C_IN", pfx+"C_IN", "Differentiator Input Capacitor (100 nF)",
                         3, 2, elec_params={"Capacitance": 1e-7}),
        create_component("electrical.resistor", "Resistor", "R_SERIES", pfx+"R_SERIES", "High-Frequency Stability Series Resistor (100 Ohm)",
                         3, 3, elec_params={"Resistance": 100.0}),
        create_component("electrical.resistor", "Resistor", "R_FB", pfx+"R_FB", "Differentiator Feedback Resistor (10 kOhm)",
                         2, 4, elec_params={"Resistance": 10000.0}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Differentiator Stage)",
                         3, 4, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Output Load Resistor (10 kOhm)",
                         4, 5, elec_params={"Resistance": 10000.0}),
        # Probes
        create_probe("TP_IN", pfx+"TP_IN", "Input Signal Probe (1.0 V)", 2, 1,
                     expected_voltage="1.0", min_v="0.98", max_v="1.02"),
        create_probe("TP_INV", pfx+"TP_INV", "Inverting Summing Junction Virtual Ground (0.0 V)", 2, 3,
                     expected_voltage="0.0", min_v="-0.01", max_v="0.01"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Differentiated Output Probe (0.0 V DC steady state, Vout = -Rf*C*dVin/dt)", 3, 5,
                     expected_voltage="0.0", min_v="-0.02", max_v="0.02", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Input to C_IN
        create_connection(pfx+"c5", pfx+"V_IN", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_IN", "positive", pfx+"C_IN", "pin1", "NET_IN"),
        # C_IN:pin2 to R_SERIES:pin1
        create_connection(pfx+"c7", pfx+"C_IN", "pin2", pfx+"R_SERIES", "pin1", "NET_SERIES"),
        # R_SERIES:pin2 to U1:in_neg and R_FB:pin1
        create_connection(pfx+"c8", pfx+"R_SERIES", "pin2", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c9", pfx+"R_FB", "pin1", pfx+"U1", "in_neg", "NET_INV"),
        # Non-inverting input grounded
        create_connection(pfx+"c10", pfx+"U1", "in_pos", pfx+"GND", "gnd", "0"),
        # Output
        create_connection(pfx+"c11", pfx+"U1", "out", pfx+"R_FB", "pin2", "NET_OUT"),
        create_connection(pfx+"c12", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        create_connection(pfx+"c13", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c14", pfx+"TP_IN", "in", pfx+"V_IN", "positive", "NET_IN"),
        create_connection(pfx+"c15", pfx+"TP_INV", "in", pfx+"U1", "in_neg", "NET_INV"),
        create_connection(pfx+"c16", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c17", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c18", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("opamp_differentiator.eee", "Op-Amp Differentiator",
             "Operational amplifier practical differentiator circuit with dual split DC supply (+15V, -15V, middle GND reference), input differentiating capacitor Cin = 100 nF, series damping resistor Rs = 100 Ohm for high-frequency noise immunity, feedback resistor Rf = 10 kOhm (Vout = -Rf*Cin*dVin/dt), 0.0V DC steady state, and 10 kOhm load.",
             comps, conns)

# -------------------------------------------------------------------------
# 7. Op-Amp Comparator
# -------------------------------------------------------------------------
def generate_opamp_comparator():
    pfx = "comp_"
    comps = [
        create_component("electrical.ground", "Ground", "GND", pfx+"GND", "Circuit Ground Reference", 6, 1),
        create_component("electrical.voltagesource", "VoltageSource", "V_POS", pfx+"V_POS", "+15V DC Positive Op-Amp Supply",
                         1, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_NEG", pfx+"V_NEG", "-15V DC Negative Op-Amp Supply",
                         5, 4, elec_params={"Voltage": 15.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_REF", pfx+"V_REF", "2.0V DC Reference Voltage",
                         2, 1, elec_params={"Voltage": 2.0}, str_params={"WaveformType": "DC"}),
        create_component("electrical.voltagesource", "VoltageSource", "V_IN", pfx+"V_IN", "3.5V DC Input Signal (Vin > Vref)",
                         4, 1, elec_params={"Voltage": 3.5}, str_params={"WaveformType": "DC"}),
        create_component("electronics.opamp", "OpAmp", "U1", pfx+"U1", "Operational Amplifier (LM741 Open-Loop Comparator)",
                         3, 3, elec_params={"Aol": 100000.0, "GBW": 1000000.0, "SR": 0.5, "VccPlus": 15.0, "VccMinus": -15.0}),
        create_component("electrical.resistor", "Resistor", "R_LOAD", pfx+"R_LOAD", "Output Load Resistor (10 kOhm)",
                         4, 5, elec_params={"Resistance": 10000.0}),
        # Probes
        create_probe("TP_REF", pfx+"TP_REF", "Inverting Reference Voltage Probe (2.0 V)", 1, 1,
                     expected_voltage="2.0", min_v="1.98", max_v="2.02"),
        create_probe("TP_IN", pfx+"TP_IN", "Non-Inverting Input Signal Probe (3.5 V)", 5, 1,
                     expected_voltage="3.5", min_v="3.48", max_v="3.52"),
        create_probe("TP_OUT", pfx+"TP_OUT", "Comparator Saturated High Output Probe (+15.0 V Positive Rail Saturation)", 3, 5,
                     expected_voltage="15.0", min_v="14.0", max_v="16.0", array_output=True,
                     time_start=0.0, time_duration=0.01, scale_factor=1, max_readings=50),
        create_probe("TP_V_POS", pfx+"TP_V_POS", "Positive Rail Supply (+15.0 V)", 1, 5,
                     expected_voltage="15.0", min_v="14.9", max_v="15.1"),
        create_probe("TP_V_NEG", pfx+"TP_V_NEG", "Negative Rail Supply (-15.0 V)", 5, 5,
                     expected_voltage="-15.0", min_v="-15.1", max_v="-14.9"),
    ]
    conns = [
        # Supplies
        create_connection(pfx+"c1", pfx+"V_POS", "positive", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c2", pfx+"V_POS", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c3", pfx+"V_NEG", "positive", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c4", pfx+"V_NEG", "negative", pfx+"U1", "v_neg", "V_NEG"),
        # Inputs: V_REF to in_neg (-), V_IN to in_pos (+)
        create_connection(pfx+"c5", pfx+"V_REF", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c6", pfx+"V_REF", "positive", pfx+"U1", "in_neg", "NET_REF"),
        create_connection(pfx+"c7", pfx+"V_IN", "negative", pfx+"GND", "gnd", "0"),
        create_connection(pfx+"c8", pfx+"V_IN", "positive", pfx+"U1", "in_pos", "NET_IN"),
        # Output to Load (Open Loop, no negative feedback)
        create_connection(pfx+"c9", pfx+"U1", "out", pfx+"R_LOAD", "pin1", "NET_OUT"),
        create_connection(pfx+"c10", pfx+"R_LOAD", "pin2", pfx+"GND", "gnd", "0"),
        # Probes
        create_connection(pfx+"c11", pfx+"TP_REF", "in", pfx+"U1", "in_neg", "NET_REF"),
        create_connection(pfx+"c12", pfx+"TP_IN", "in", pfx+"U1", "in_pos", "NET_IN"),
        create_connection(pfx+"c13", pfx+"TP_OUT", "in", pfx+"U1", "out", "NET_OUT"),
        create_connection(pfx+"c14", pfx+"TP_V_POS", "in", pfx+"U1", "v_pos", "V_POS"),
        create_connection(pfx+"c15", pfx+"TP_V_NEG", "in", pfx+"U1", "v_neg", "V_NEG"),
    ]
    save_eee("opamp_comparator.eee", "Op-Amp Comparator",
             "Open-loop operational amplifier voltage comparator with dual split DC supply (+15V, -15V, middle GND reference), reference voltage Vref = 2.0V on inverting input (-), input signal Vin = 3.5V on non-inverting input (+), open-loop gain saturating output to positive supply rail (+15.0V), and 10 kOhm load.",
             comps, conns)

if __name__ == "__main__":
    generate_inverting_amplifier()
    generate_non_inverting_amplifier()
    generate_voltage_follower()
    generate_summing_amplifier()
    generate_opamp_integrator()
    generate_opamp_differentiator()
    generate_opamp_comparator()
