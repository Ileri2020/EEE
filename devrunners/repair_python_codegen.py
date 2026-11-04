from pathlib import Path
p = Path(__file__).resolve().parent.parent / "EESimulator" / "EESimulator.Scripting" / "PythonCodeGenerator.cs"
text = p.read_text(encoding="utf-8")
needle = '            // ── Structural / Measurement / General ───────────────────────────\n'
block = '''            // ── Power Systems, Machines, Loads & Storage ──────────────────
            SinglePhaseTransmissionLineComponent line1 =>
                $"{varName} = circuit.add_single_phase_transmission_line(\"{comp.Name}\", position=({x}, {y}), rated_voltage={line1.RatedVoltage}, frequency={line1.Frequency}, length_km={line1.LengthKm}, resistance_per_km={line1.ResistancePerKm}, inductance_per_km={line1.InductancePerKm}, capacitance_per_km={line1.CapacitancePerKm}, conductance_per_km={line1.ConductancePerKm}, model_type=\"{line1.ModelType}\")",

            TwoPhaseTransmissionLineComponent line2 =>
                $"{varName} = circuit.add_two_phase_transmission_line(\"{comp.Name}\", position=({x}, {y}), rated_voltage={line2.RatedLineVoltage}, frequency={line2.Frequency}, length_km={line2.LengthKm}, phase_displacement={line2.PhaseDisplacementDegrees}, configuration=\"{line2.ConductorConfiguration}\")",

            ThreePhaseTransmissionLineComponent line3 =>
                $"{varName} = circuit.add_three_phase_transmission_line(\"{comp.Name}\", position=({x}, {y}), rated_line_voltage={line3.RatedLineVoltage}, frequency={line3.Frequency}, length_km={line3.LengthKm}, phase_sequence=\"{line3.PhaseSequence}\", connection=\"{line3.Connection}\", model_type=\"{line3.ModelType}\")",

            ACTransformerComponent transformer =>
                $"{varName} = circuit.add_ac_transformer(\"{comp.Name}\", position=({x}, {y}), primary_voltage={transformer.RatedPrimaryVoltage}, secondary_voltage={transformer.RatedSecondaryVoltage}, apparent_power={transformer.RatedApparentPower}, frequency={transformer.Frequency}, connection=\"{transformer.WindingConnection}\")",

            SynchronousGeneratorComponent generator =>
                $"{varName} = circuit.add_synchronous_generator(\"{comp.Name}\", position=({x}, {y}), rated_voltage={generator.RatedVoltage}, rated_frequency={generator.RatedFrequency}, rated_power={generator.RatedApparentPower}, phases={generator.NumberOfPhases}, poles={generator.NumberOfPoles})",

            InductionMotorComponent motor =>
                $"{varName} = circuit.add_induction_motor(\"{comp.Name}\", position=({x}, {y}), rated_voltage={motor.RatedVoltage}, rated_power={motor.RatedPower}, frequency={motor.Frequency}, poles={motor.NumberOfPoles})",

            ACBulbComponent bulb =>
                $"{varName} = circuit.add_ac_bulb(\"{comp.Name}\", position=({x}, {y}), rated_voltage={bulb.RatedVoltage}, rated_power={bulb.RatedPower}, initially_on={bulb.InitiallyOn.ToString().ToLowerInvariant()}, dimming_percent={bulb.DimmingPercent})",

            ACFanComponent fan =>
                $"{varName} = circuit.add_ac_fan(\"{comp.Name}\", position=({x}, {y}), rated_voltage={fan.RatedVoltage}, rated_power={fan.RatedPower}, power_factor={fan.PowerFactor}, selected_speed={fan.SelectedSpeed})",

            SolarPanelComponent panel =>
                $"{varName} = circuit.add_solar_panel(\"{comp.Name}\", position=({x}, {y}), maximum_power_rating={panel.MaximumPowerRating}, open_circuit_voltage={panel.OpenCircuitVoltage}, short_circuit_current={panel.ShortCircuitCurrent}, irradiance={panel.SolarIrradiance})",

            BatteryComponent battery =>
                $"{varName} = circuit.add_battery(\"{comp.Name}\", position=({x}, {y}), nominal_voltage={battery.NominalVoltage}, capacity_ah={battery.CapacityAh}, initial_soc_percent={battery.InitialSocPercent}, chemistry=\"{battery.Chemistry}\")",

'''
if needle not in text:
    raise SystemExit("Structural marker not found")
p.write_text(text.replace(needle, block + needle, 1), encoding="utf-8")
