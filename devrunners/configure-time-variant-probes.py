import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
EXAMPLES = ROOT / "Resources" / "Raw" / "Examples"

PROBE_SETTINGS = {
    "colpitts_oscillator.eee": (0.0001, 0.0001, 2, 100),
    "hartley_oscillator.eee": (0.0002, 0.0001, 2, 80),
    "crystal_oscillator.eee": (0.001, 0.0005, 5, 100),
    "wien_bridge_oscillator.eee": (0.01, 0.01, 2, 80),
    "wien_bridge_function_generator.eee": (0.01, 0.01, 2, 80),
    "voltage_controlled_oscillator.eee": (0.0, 0.005, 2, 100),
    "triangle_wave_generator.eee": (0.04, 0.01, 13, 80),
    "sawtooth_wave_generator.eee": (0.0, 0.005, 1, 80),
    "555_timer_monostable.eee": (0.004, 0.061, 100, 60),
    "schmitt_trigger.eee": (0.0, 0.02, 2, 60),
    "llc_resonant_converter.eee": (0.0005, 0.0001, 2, 100),
    "buck_converter.eee": (0.002, 0.0002, 1, 100),
    "synchronous_buck_converter.eee": (0.002, 0.0002, 1, 100),
    "boost_converter.eee": (0.003, 0.0003, 2, 80),
    "buck_boost_converter.eee": (0.003, 0.0003, 2, 80),
    "flyback_smps.eee": (0.001, 0.0001, 1, 100),
    "forward_converter.eee": (0.001, 0.0001, 1, 100),
    "sepic_converter.eee": (0.002, 0.0005, 2, 80),
    "zeta_converter.eee": (0.002, 0.0005, 2, 80),
    "full_wave_bridge_rectifier.eee": (0.01, 0.04, 2, 80),
    "single_phase_controlled_bridge_rectifier.eee": (0.02, 0.04, 2, 80),
    "single_phase_cycloconverter.eee": (0.0, 0.1, 3, 100),
    "single_phase_h_bridge_inverter.eee": (0.02, 0.04, 2, 100),
    "three_phase_inverter.eee": (0.02, 0.04, 2, 100),
    "cockcroft_walton_multiplier.eee": (0.0, 0.05, 2, 80),
    "dc_voltage_doubler.eee": (0.0, 0.05, 2, 80),
    "chebyshev_filter.eee": (0.0, 0.002, 1, 80),
    "butterworth_filter.eee": (0.0, 0.002, 1, 80),
    "active_notch_filter.eee": (0.0, 0.05, 2, 80),
    "class_b_push_pull_amplifier.eee": (0.005, 0.01, 1, 80),
    "class_c_rf_amplifier.eee": (0.0001, 0.0001, 1, 80),
    "class_d_audio_amplifier.eee": (0.001, 0.002, 2, 100),
}

PREFERRED_PROBES = {
    "colpitts_oscillator.eee": "TP_DIVIDER",
    "hartley_oscillator.eee": "TP_TANK",
    "crystal_oscillator.eee": "TP_OUT",
    "wien_bridge_oscillator.eee": "TP_BRIDGE",
    "wien_bridge_function_generator.eee": "TP_OUT",
    "voltage_controlled_oscillator.eee": "TP_TRIANGLE",
    "triangle_wave_generator.eee": "TP_OUT",
    "sawtooth_wave_generator.eee": "TP_OUT",
    "555_timer_monostable.eee": "TP_OUT",
    "schmitt_trigger.eee": "TP_OUT",
}


updated = 0
missing = []
for filename, (start, duration, scale, maximum) in PROBE_SETTINGS.items():
    path = EXAMPLES / filename
    if not path.exists():
        missing.append(filename)
        continue

    with path.open("r", encoding="utf-8-sig") as stream:
        circuit = json.load(stream)

    probes = [
        component
        for component in circuit.get("Components", [])
        if str(component.get("ComponentType", "")).lower() == "probe"
    ]
    if not probes:
        missing.append(filename)
        continue

    preferred_name = PREFERRED_PROBES.get(filename)
    probe = next(
        (candidate for candidate in probes if candidate.get("Name") == preferred_name),
        probes[0],
    )
    string_parameters = probe.setdefault("StringParameters", {})
    custom_properties = probe.setdefault("CustomProperties", {})
    values = {
        "ArrayOutputEnabled": "True",
        "TimeRangeStart": str(start),
        "TimeRangeDuration": str(duration),
        "TimeScaleFactor": str(scale),
        "MaxReadings": str(maximum),
        "RealtimeArray": "False",
    }
    string_parameters.update(values)
    custom_properties.update(values)
    with path.open("w", encoding="utf-8") as stream:
        json.dump(circuit, stream, ensure_ascii=False, indent=2)
    updated += 1

print(f"Configured {updated} time-variant example probe files.")
if missing:
    print("Missing or probe-less files:")
    for filename in missing:
        print(f"  {filename}")
