using System.Globalization;
using System.Text.RegularExpressions;
using EEE.Services;
using EESimulator.Commands;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using Xunit;

namespace EESimulator.Tests;

public sealed class BundledExampleCircuitTests
{
    [Fact]
    public void Traffic_light_controller_probes_are_connected_by_component_id()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "traffic_light_controller.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var probes = circuit.Components.OfType<ProbeComponent>().ToArray();
        string endpoints = string.Join(", ", circuit.Connections.Select(connection =>
            $"{connection.FromComponentId}/{connection.ToComponentId}"));

        Assert.All(probes, probe => Assert.True(
            circuit.Connections.Any(connection =>
                connection.FromComponentId == probe.Id || connection.ToComponentId == probe.Id),
            $"{probe.Name} ({probe.Id}) is not connected. Connection endpoints: {endpoints}"));
    }

    [Fact]
    public void Complex_showcase_ground_groups_follow_the_power_zones()
    {
        string repositoryRoot = FindRepositoryRoot();
        string exampleFile = Path.Combine(
            repositoryRoot,
            "Resources",
            "Raw",
            "Examples",
            "complex_linked_component_showcase.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var grounds = circuit.Components.OfType<GroundNode>().ToArray();
        var expectedGroups = new[]
        {
            "Grid", "Power Storage", "Transmission", "Household", "Industrial",
            "Charger", "DC Analog", "Semiconductors", "Digital Logic"
        };

        Assert.Contains(grounds, ground => ground.GroundGroup == GroundComponent.DefaultGroundGroup);
        Assert.All(expectedGroups, group =>
            Assert.Contains(grounds, ground => ground.GroundGroup == group));

        CircuitTopology topology = EESimulator.Core.Electrical.NetlistBuilder.BuildTopology(circuit);
        var groupNets = expectedGroups.Select(group =>
        {
            GroundNode ground = grounds.First(candidate => candidate.GroundGroup == group);
            return topology.PinToNetMap[new EESimulator.Core.Electrical.PinReference(ground.Id, "gnd")];
        }).ToArray();
        Assert.All(groupNets, net => Assert.False(net.IsGround));
        Assert.Equal(groupNets.Length, groupNets.Distinct().Count());
        Assert.All(groupNets.Select((net, index) => (net, index)), item =>
            Assert.Equal(expectedGroups[item.index], item.net.GroundGroup));
    }

    [Fact]
    public void Complete_solar_system_bus_matches_its_expected_operating_point()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "complete_solar_power_system.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());
        Assert.True(simulation.Success, simulation.Message);

        var probe = Assert.IsType<ProbeComponent>(
            FindComponent(circuit, "Probe", "TP_DC_BUS"));
        var panel = Assert.IsType<SolarPanelComponent>(
            FindComponent(circuit, "SolarPanel", "PV1"));
        var battery = Assert.IsType<BatteryComponent>(
            FindComponent(circuit, "Battery", "BAT1"));
        var led = Assert.IsType<LEDComponent>(
            FindComponent(circuit, "LED", "D_STATUS"));
        var ledResistor = FindComponent(circuit, "Resistor", "R_LED");
        double measuredVoltage = simulation.OperatingPointVoltages[$"V({probe.Name})"];
        double expectedVoltage = double.Parse(
            probe.CustomProperties["ExpectedVoltage"].ToString()!,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        Assert.True(Math.Abs(measuredVoltage - expectedVoltage) < 1e-4,
            $"Expected {expectedVoltage:G17} V; measured {measuredVoltage:G17} V; " +
            $"PV Voc={panel.TemperatureAdjustedVoc:G17} V, I={panel.LiveCurrent:G17} A; " +
            $"battery Voc={battery.OpenCircuitVoltage:G17} V, terminal={battery.LiveVoltage:G17} V, " +
            $"pins={string.Join(",", battery.LivePinVoltages.Select(pin => $"{pin.Key}:{pin.Value:G17}"))}; " +
            simulation.Message);
        Assert.True(AreConnected(circuit, battery, "positive", ledResistor, "pin1"));
        Assert.True(AreConnected(circuit, ledResistor, "pin2", led, "anode"));
        Assert.True(Math.Abs(battery.LiveVoltage - measuredVoltage) < 1e-4,
            $"Battery terminal model {battery.LiveVoltage:G17} V does not match bus {measuredVoltage:G17} V.");
        Assert.InRange(Math.Abs(led.LiveCurrent), 0, 0.02);
    }

    [Fact]
    public void PMDC_example_uses_a_back_emf_motor_model()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "permanent_magnet_dc_motor_controller.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var motor = Assert.IsType<PermanentMagnetDcMotorComponent>(
            FindComponent(circuit, "PermanentMagnetDcMotor", "MOTOR"));

        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(simulation.Success, simulation.Message);
        Assert.True(motor.BackEmfVoltage > 0);
        Assert.True(motor.LiveCurrent > 0);
        Assert.True(motor.ElectromagneticTorque > 0);
    }

    [Fact]
    public void BLDC_example_uses_a_trapezoidal_three_phase_machine_and_averaged_drive()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "brushless_dc_motor_drive.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var motor = Assert.IsType<BrushlessDcMotorComponent>(
            FindComponent(circuit, "BrushlessDcMotor", "MOTOR"));
        var drive = Assert.IsType<VariableFrequencyDriveComponent>(
            FindComponent(circuit, "VariableFrequencyDrive", "VFD"));
        Assert.Equal(50, drive.OutputFrequency);
        Assert.All(new[] { "A", "B", "C" }, phase =>
            Assert.True(AreConnected(circuit, drive, phase, motor, phase), $"VFD.{phase} is not connected to MOTOR.{phase}."));

        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(simulation.Success, simulation.Message);
        Assert.True(double.IsFinite(motor.ElectromagneticTorque));
        Assert.Contains("I(MOTOR_phaseB)", simulation.OperatingPointCurrents.Keys);
        Assert.Contains("I(MOTOR_phaseC)", simulation.OperatingPointCurrents.Keys);
    }

    [Fact]
    public void VFD_example_uses_frequency_control_and_honors_its_enable_input()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "variable_frequency_drive.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var drive = Assert.IsType<VariableFrequencyDriveComponent>(
            FindComponent(circuit, "VariableFrequencyDrive", "VFD"));
        var enable = Assert.IsType<EESimulator.Core.Components.Electronics.LogicInputComponent>(
            FindComponent(circuit, "LogicInput", "ENABLE"));
        Assert.Equal(10, drive.OutputFrequency);

        var adapter = new SpiceSharpAdapter();
        var enabledSimulation = adapter.RunSimulation(circuit, new SimulationSettings());
        Assert.True(enabledSimulation.Success, enabledSimulation.Message);
        Assert.True(drive.LivePinVoltages["A"] > 0);

        enable.State = false;
        var disabledSimulation = adapter.RunSimulation(circuit, new SimulationSettings());
        Assert.True(disabledSimulation.Success, disabledSimulation.Message);
        Assert.InRange(Math.Abs(drive.LivePinVoltages["A"]), 0, 1e-8);
    }

    [Fact]
    public void Single_phase_starter_includes_main_and_capacitor_start_windings()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "single_phase_ac_motor_capacitor_starter.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var motor = Assert.IsType<SinglePhaseInductionMotorComponent>(
            FindComponent(circuit, "SinglePhaseInductionMotor", "MOTOR"));
        Assert.True(AreConnected(circuit, motor, "line", FindComponent(circuit, "Probe", "TP_OUT"), "in"));
        Assert.True(motor.StartCapacitance > 0);

        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(simulation.Success, simulation.Message);
        Assert.True(motor.LiveCurrent > 0);
    }

    [Fact]
    public void AVR_example_regulates_generator_field_voltage_through_feedback()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "automatic_voltage_regulator.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var generator = Assert.IsType<SynchronousGeneratorComponent>(
            FindComponent(circuit, "SynchronousGenerator", "GEN"));
        var controller = Assert.IsType<EESimulator.Core.Components.Electronics.OpAmpComponent>(
            FindComponent(circuit, "OpAmp", "AVR"));
        Assert.True(AreConnected(circuit, controller, "out", generator, "field_positive"));
        Assert.True(AreConnected(circuit, generator, "field_negative", FindComponent(circuit, "Ground", "GND"), "gnd"));

        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(simulation.Success, simulation.Message);
        Assert.InRange(generator.FieldExcitation, 0.98, 1.02);
        Assert.InRange(Math.Abs(generator.LivePinVoltages["field_positive"] - 5), 0, 0.02);
        Assert.InRange(
            Math.Abs(simulation.OperatingPointVoltages["V(TP_GEN_A)"] - 13.882816666318293),
            0,
            1e-4);
    }

    [Fact]
    public void Microcontroller_example_wires_all_eight_sum_bits_and_uses_python_booleans()
    {
        string exampleFile = Path.Combine(
            FindRepositoryRoot(),
            "Resources",
            "Raw",
            "Examples",
            "simple_microcontroller_system.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        ComponentNode adder = FindComponent(circuit, "Adder", "ALU_ADD");
        ComponentNode accumulator = FindComponent(circuit, "Register", "ACC");
        ComponentNode lowDigit = FindComponent(circuit, "HexDigit", "RESULT");
        ComponentNode highDigit = FindComponent(circuit, "HexDigit", "RESULT_HI");

        for (int bit = 0; bit < 8; bit++)
            Assert.True(AreConnected(circuit, adder, $"Y{bit}", accumulator, $"D{bit}"),
                $"ALU_ADD.Y{bit} is not connected to ACC.D{bit}.");
        for (int bit = 0; bit < 4; bit++)
        {
            Assert.True(AreConnected(circuit, adder, $"Y{bit}", lowDigit, $"D{bit}"));
            Assert.True(AreConnected(circuit, adder, $"Y{bit + 4}", highDigit, $"D{bit}"));
        }
        Assert.DoesNotContain(circuit.Connections, connection =>
            connection.ToComponentId == accumulator.Id && connection.ToPin == "clr");

        string script = PythonCodeGenerator.GenerateFullScript(circuit);
        Assert.Contains("eesim.set_logic(\"IN0\", True)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("eesim.set_logic(\"IN0\", true)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Complex_showcase_ttl_outputs_match_their_connected_logic_probes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string exampleFile = Path.Combine(
            repositoryRoot,
            "Resources",
            "Raw",
            "Examples",
            "complex_linked_component_showcase.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);

        var simulation = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());
        Assert.True(simulation.Success, simulation.Message);

        ComponentNode ttl = FindComponent(circuit, "7400", "TTL_7400");
        var topology = EESimulator.Core.Electrical.NetlistBuilder.BuildTopology(circuit);
        var componentsById = circuit.Components.ToDictionary(component => component.Id);
        string floatingNetDetails = string.Join("; ", topology.Nets
            .Where(net => net.NetName is "Net_441" or "Net_442")
            .Select(net => $"{net.NetName}=[{string.Join(",", net.ConnectedPins.Select(pin =>
                $"{componentsById[pin.ComponentId].Name}.{pin.PinId}"))}]"));
        foreach (string pinId in new[] { "pin3", "pin6", "pin8", "pin11" })
        {
            ComponentNode logicOutput = circuit.Components.Single(component =>
                component.ComponentType == "LogicOutput" &&
                circuit.Connections.Any(connection =>
                    (connection.FromComponentId == ttl.Id && connection.FromPin == pinId &&
                     connection.ToComponentId == component.Id) ||
                    (connection.ToComponentId == ttl.Id && connection.ToPin == pinId &&
                     connection.FromComponentId == component.Id)));
            bool expected = bool.Parse(logicOutput.CustomProperties["ExpectedLogicState"].ToString()!);
            var logicOutputProbe = Assert.IsType<LogicOutputComponent>(logicOutput);
            string ttlVoltage = ttl.LivePinVoltages.TryGetValue(pinId, out double voltage)
                ? voltage.ToString("G17", CultureInfo.InvariantCulture)
                : "unavailable";
            var net = topology.PinToNetMap[new EESimulator.Core.Electrical.PinReference(ttl.Id, pinId)];
            bool hasSimulatedPinVoltage = simulation.OperatingPointPinVoltages.TryGetValue(
                (ttl.Id, pinId), out double simulatedPinVoltage);
            Assert.True(expected == logicOutputProbe.IsOn,
                $"{pinId}: expected={expected}, digital output={DigitalLogicSimulator.ReadOutputState(ttl, pinId)}, " +
                $"stored voltage={ttl.CustomProperties.GetValueOrDefault($"OutputVoltage:{pinId}")}, " +
                $"measured pin voltage={ttlVoltage}, topology net={net.NetName} (ground={net.IsGround}), " +
                $"simulation pin voltage={(hasSimulatedPinVoltage ? simulatedPinVoltage.ToString("G17", CultureInfo.InvariantCulture) : "unavailable")}, " +
                $"logic probe voltage={logicOutputProbe.LiveVoltage.ToString("G17", CultureInfo.InvariantCulture)}, " +
                $"floating nets={floatingNetDetails}, simulation message={simulation.Message}");
        }
    }

    [Fact]
    public void Bundled_examples_use_grid_calls_and_round_trip_through_the_python_api()
    {
        string repositoryRoot = FindRepositoryRoot();
        string examplesDirectory = Path.Combine(repositoryRoot, "Resources", "Raw", "Examples");
        string pythonDirectory = Path.Combine(repositoryRoot, "Resources", "Raw", "Python");
        string[] exampleFiles = Directory.GetFiles(examplesDirectory, "*.eee");
        string[] additionalExampleIds =
        {
            "wien_bridge_oscillator", "hartley_oscillator", "colpitts_oscillator", "555_astable_oscillator",
            "half_wave_rectifier", "full_wave_bridge_rectifier", "three_phase_bridge_rectifier",
            "adjustable_dc_power_supply", "7805_regulated_power_supply", "buck_converter", "boost_converter",
            "buck_boost_converter", "flyback_smps", "laptop_charger_smps", "lithium_ion_battery_charger",
            "single_phase_h_bridge_inverter", "three_phase_inverter", "class_ab_audio_amplifier",
            "operational_amplifier", "four_bit_adder", "four_bit_alu", "four_bit_cpu", "eight_bit_cpu",
            "three_phase_transformer", "three_phase_synchronous_generator", "three_phase_induction_motor",
            "star_delta_motor_starter", "complete_solar_power_system", "generation_transmission_network"
        };
        string[] newlyAddedExampleIds =
        {
            "crystal_oscillator", "555_timer_monostable", "astable_multivibrator", "voltage_controlled_oscillator",
            "sepic_converter", "forward_converter", "push_pull_converter", "full_bridge_smps",
            "three_phase_controlled_rectifier", "power_factor_correction", "llc_resonant_converter",
            "cockcroft_walton_multiplier", "common_source_mosfet_amplifier", "differential_amplifier",
            "instrumentation_amplifier", "class_d_audio_amplifier", "active_band_pass_filter", "eight_bit_alu",
            "sixteen_by_four_ram", "digital_calculator", "digital_clock", "traffic_light_controller",
            "single_phase_induction_motor", "dc_shunt_motor", "dc_series_motor", "single_phase_transformer",
            "three_phase_star_delta_connection", "mppt_solar_charge_controller", "automatic_generator_changeover"
        };
        string[] currentExampleIds =
        {
            "complex_linked_component_showcase",
            "wien_bridge_function_generator", "sawtooth_wave_generator", "triangle_wave_generator", "pll_frequency_synthesizer",
            "single_phase_controlled_bridge_rectifier", "three_phase_half_wave_rectifier", "three_phase_ac_voltage_controller",
            "single_phase_cycloconverter", "synchronous_buck_converter", "zeta_converter", "dc_voltage_doubler",
            "active_clamp_flyback_converter", "class_a_power_amplifier", "class_b_push_pull_amplifier", "class_c_rf_amplifier",
            "schmitt_trigger", "precision_rectifier", "active_notch_filter", "eight_bit_register_bank",
            "eight_bit_shift_register", "eight_bit_binary_counter", "seven_segment_display_decoder", "digital_stopwatch",
            "simple_microcontroller_system", "permanent_magnet_dc_motor_controller", "brushless_dc_motor_drive",
            "variable_frequency_drive", "single_phase_ac_motor_capacitor_starter", "grid_tied_solar_inverter",
            "automatic_voltage_regulator", "butterworth_filter", "chebyshev_filter", "bessel_filter"
        };
        string[] testPointExampleIds = additionalExampleIds.Concat(newlyAddedExampleIds).Concat(currentExampleIds).ToArray();

        Assert.Equal(99, exampleFiles.Length);
        Assert.Equal(99, ExampleCircuitService.GetExamples().Count);
        Assert.Equal("complex_linked_component_showcase", ExampleCircuitService.GetExamples()[0].Id);
        Assert.All(testPointExampleIds, id =>
        {
            Assert.Contains(exampleFiles, path => Path.GetFileNameWithoutExtension(path) == id);
            Assert.Contains(ExampleCircuitService.GetExamples(), example => example.Id == id);
        });

        foreach (string exampleFile in exampleFiles)
        {
            string exampleName = Path.GetFileNameWithoutExtension(exampleFile);
            CircuitNode sourceCircuit = EeePlatformSerializer.LoadFromEee(exampleFile);

            Assert.All(sourceCircuit.Components, component =>
            {
                Assert.True(component.HasLogicalPlacement, $"{exampleName}: {component.Name} has no row/column.");
                Assert.True(component.LogicalRow > 0, $"{exampleName}: {component.Name} has an invalid row.");
                Assert.True(component.LogicalColumn > 0, $"{exampleName}: {component.Name} has an invalid column.");
            });

            foreach (ConnectionNode connection in sourceCircuit.Connections)
            {
                ComponentNode from = Assert.IsAssignableFrom<ComponentNode>(
                    sourceCircuit.FindChildren<ComponentNode>().SingleOrDefault(component => component.Id == connection.FromComponentId));
                ComponentNode to = Assert.IsAssignableFrom<ComponentNode>(
                    sourceCircuit.FindChildren<ComponentNode>().SingleOrDefault(component => component.Id == connection.ToComponentId));
                Assert.True(from.Pins.Any(pin => pin.Id.Equals(connection.FromPin, StringComparison.OrdinalIgnoreCase)),
                    $"{exampleName}/{from.Name}: pin '{connection.FromPin}' does not exist.");
                Assert.True(to.Pins.Any(pin => pin.Id.Equals(connection.ToPin, StringComparison.OrdinalIgnoreCase)),
                    $"{exampleName}/{to.Name}: pin '{connection.ToPin}' does not exist.");
            }

            var testProbes = sourceCircuit.Components
                .Where(component => component.ComponentType.Equals("Probe", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.True(testProbes.Length >= 2, $"{exampleName}: expected at least two test points.");
            if (exampleName == "complex_linked_component_showcase")
                Assert.True(testProbes.Length >= 40, $"{exampleName}: expected at least 40 section checkpoints.");
            Assert.All(testProbes, probe =>
            {
                Assert.True(sourceCircuit.Connections.Any(connection =>
                        connection.FromComponentId == probe.Id || connection.ToComponentId == probe.Id),
                    $"{exampleName}/{probe.Name}: probe is not connected.");
                bool hasExpectedVoltage = probe.CustomProperties.ContainsKey("ExpectedVoltage");
                bool hasExpectedRange = probe.CustomProperties.ContainsKey("ExpectedVoltageMinimum") &&
                                        probe.CustomProperties.ContainsKey("ExpectedVoltageMaximum");
                Assert.True(hasExpectedVoltage || hasExpectedRange,
                    $"{exampleName}/{probe.Name}: expected voltage or voltage range metadata is required.");
            });

            if (testPointExampleIds.Contains(exampleName, StringComparer.Ordinal))
            {
                Assert.NotEmpty(testProbes);
            }

            var simulation = new SpiceSharpAdapter().RunSimulation(sourceCircuit, new SimulationSettings());
            Assert.True(simulation.Success, $"{exampleName}: {simulation.Message}");

            foreach (var probe in testProbes)
            {
                ConnectionNode probeConnection = sourceCircuit.Connections.Single(connection =>
                    connection.FromComponentId == probe.Id || connection.ToComponentId == probe.Id);
                string probePinId = probeConnection.FromComponentId == probe.Id ? probeConnection.FromPin : probeConnection.ToPin;
                if (!simulation.OperatingPointPinVoltages.ContainsKey((probe.Id, probePinId)) ||
                    !simulation.OperatingPointVoltages.TryGetValue($"V({probe.Name})", out double measuredVoltage))
                {
                    Assert.StartsWith("Fallback approximation used", simulation.Message);
                    continue;
                }

                Assert.True(double.IsFinite(measuredVoltage), $"{exampleName}/{probe.Name}: voltage is not finite.");

                if (probe.CustomProperties.TryGetValue("ExpectedVoltageMinimum", out var minimumValue) &&
                    probe.CustomProperties.TryGetValue("ExpectedVoltageMaximum", out var maximumValue))
                {
                    Assert.True(double.TryParse(minimumValue?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double minimumVoltage),
                        $"{exampleName}/{probe.Name}: invalid expected voltage minimum.");
                    Assert.True(double.TryParse(maximumValue?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double maximumVoltage) && minimumVoltage <= maximumVoltage,
                        $"{exampleName}/{probe.Name}: invalid expected voltage range.");
                }
                else if (probe.CustomProperties.TryGetValue("ExpectedVoltage", out var expectedValue))
                {
                    Assert.True(double.TryParse(expectedValue?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out _),
                        $"{exampleName}/{probe.Name}: invalid expected voltage.");
                }
            }

            if (exampleName == "complex_linked_component_showcase")
            {
                var zoneHeadings = sourceCircuit.Components.OfType<TextAnnotationComponent>().ToArray();
                Assert.Equal(16, zoneHeadings.Length);
                Assert.All(zoneHeadings, heading =>
                    Assert.True(heading.Text.StartsWith("ZONE ", StringComparison.Ordinal), $"{heading.Name} has no zone title."));

                ComponentNode generator = FindComponent(sourceCircuit, "SynchronousGenerator");
                ComponentNode threePhaseLine = FindComponent(sourceCircuit, "ThreePhaseTransmissionLine");
                ComponentNode inductionMotor = FindComponent(sourceCircuit, "InductionMotor");
                foreach (string phase in new[] { "A", "B", "C" })
                {
                    Assert.True(AreConnected(sourceCircuit, generator, phase, threePhaseLine, $"{phase}_send"));
                    Assert.True(AreConnected(sourceCircuit, threePhaseLine, $"{phase}_receive", inductionMotor, phase));
                }
                Assert.True(AreConnected(sourceCircuit, threePhaseLine, "N_receive", inductionMotor, "N"));
                Assert.True(AreConnected(sourceCircuit,
                    FindComponent(sourceCircuit, "TwoPhaseTransmissionLine"), "a_to",
                    FindComponent(sourceCircuit, "Resistor", "R_2PH_A"), "pin1"));
                Assert.True(AreConnected(sourceCircuit,
                    FindComponent(sourceCircuit, "SinglePhaseTransmissionLine"), "to",
                    FindComponent(sourceCircuit, "ACBulb", "BULB_HOUSEHOLD"), "L"));
                Assert.True(AreConnected(sourceCircuit,
                    FindComponent(sourceCircuit, "SolarPanel"), "positive",
                    FindComponent(sourceCircuit, "Resistor", "R_PV_LOAD"), "pin1"));
                Assert.True(AreConnected(sourceCircuit,
                    FindComponent(sourceCircuit, "Battery"), "positive",
                    FindComponent(sourceCircuit, "Resistor", "R_BAT_LOAD"), "pin1"));

                ComponentNode ttl7400 = FindComponent(sourceCircuit, "7400");
                foreach (string pin in new[] { "pin3", "pin6", "pin8", "pin11" })
                {
                    Assert.Contains(sourceCircuit.Connections, connection =>
                        (connection.FromComponentId == ttl7400.Id &&
                         connection.FromPin.Equals(pin, StringComparison.OrdinalIgnoreCase)) ||
                        (connection.ToComponentId == ttl7400.Id &&
                         connection.ToPin.Equals(pin, StringComparison.OrdinalIgnoreCase)));
                }

                var arithmeticDisplays = new Dictionary<string, string>
                {
                    ["MULTIPLIER"] = "LED_BAR_MUL",
                    ["DIVIDER"] = "LED_BAR_DIV",
                    ["SHIFTER"] = "LED_BAR_SHIFT",
                    ["BIT_ADDER"] = "LED_BAR_BITADD",
                    ["BIT_FINDER"] = "LED_BAR_BITFIND",
                    ["SQUARE_ROOT"] = "LED_BAR_SQRT",
                    ["MIN_MAX"] = "LED_BAR_MINMAX"
                };
                foreach ((string componentName, string displayName) in arithmeticDisplays)
                {
                    ComponentNode arithmetic = sourceCircuit.Components.Single(component => component.Name == componentName);
                    ComponentNode display = FindComponent(sourceCircuit, "LedBar", displayName);
                    for (int bit = 0; bit < 8; bit++)
                        Assert.True(AreConnected(sourceCircuit, arithmetic, $"Y{bit}", display, $"D{bit}"),
                            $"{componentName}.Y{bit} is not connected to {displayName}.");
                }

                DigitalLogicSimulator.EvaluateCombinational(sourceCircuit);

                foreach (ComponentNode component in sourceCircuit.Components)
                {
                    foreach (var expectedOutput in component.CustomProperties
                                 .Where(property => property.Key.StartsWith("ExpectedOutput:", StringComparison.Ordinal)))
                    {
                        string pin = expectedOutput.Key["ExpectedOutput:".Length..];
                        Assert.True(bool.TryParse(expectedOutput.Value?.ToString(), out bool expected),
                            $"{exampleName}/{component.Name}: invalid expected output state for {pin}.");
                        Assert.True(expected == DigitalLogicSimulator.ReadOutputState(component, pin),
                            $"{exampleName}/{component.Name}: expected output {pin}={expected}, " +
                            $"but the live digital output was {DigitalLogicSimulator.ReadOutputState(component, pin)}.");
                    }

                    if (component.CustomProperties.TryGetValue("ExpectedRenderedValue", out var expectedRenderedValue))
                    {
                        Assert.True(long.TryParse(expectedRenderedValue?.ToString(), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out long expected),
                            $"{exampleName}/{component.Name}: invalid expected rendered value.");
                        long actual = 0;
                        bool hasRenderedValue = component.CustomProperties.TryGetValue("RenderedValue", out var renderedValue) &&
                                                long.TryParse(renderedValue?.ToString(), NumberStyles.Integer,
                                                    CultureInfo.InvariantCulture, out actual);
                        Assert.True(hasRenderedValue, $"{exampleName}/{component.Name}: display value was not rendered.");
                        Assert.Equal(expected, actual);
                    }

                    if (component.CustomProperties.TryGetValue("ExpectedRenderedSegments", out var expectedSegments))
                    {
                        Assert.True(int.TryParse(expectedSegments?.ToString(), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int expected),
                            $"{exampleName}/{component.Name}: invalid expected segment mask.");
                        int actual = 0;
                        bool hasRenderedSegments = component.CustomProperties.TryGetValue("RenderedSegments", out var renderedSegments) &&
                                                   int.TryParse(renderedSegments?.ToString(), NumberStyles.Integer,
                                                       CultureInfo.InvariantCulture, out actual);
                        Assert.True(hasRenderedSegments, $"{exampleName}/{component.Name}: seven-segment output was not rendered.");
                        Assert.Equal(expected, actual);
                    }

                    if (component.CustomProperties.TryGetValue("ExpectedLogicState", out var expectedLogicState))
                    {
                        Assert.IsType<LogicOutputComponent>(component);
                        Assert.True(bool.TryParse(expectedLogicState?.ToString(), out bool expected),
                            $"{exampleName}/{component.Name}: invalid expected logic state.");
                        Assert.True(expected == Assert.IsType<LogicOutputComponent>(component).IsOn,
                            $"{exampleName}/{component.Name}: rendered logic output does not match its expected state.");
                    }

                    if (component.CustomProperties.TryGetValue("ExpectedIsOn", out var expectedLedState))
                    {
                        Assert.IsType<LEDComponent>(component);
                        Assert.True(bool.TryParse(expectedLedState?.ToString(), out bool expected),
                            $"{exampleName}/{component.Name}: invalid expected LED state.");
                        Assert.Equal(expected, Assert.IsType<LEDComponent>(component).IsOn);
                    }
                }
            }

            string script = PythonCodeGenerator.GenerateFullScript(sourceCircuit);
            var addStatements = Regex.Matches(script, @"(?m)^.+ = circuit\.add_component\(.+$");
            Assert.Equal(sourceCircuit.Components.Count(), addStatements.Count);
            Assert.All(addStatements.Cast<Match>(), statement =>
            {
                Assert.Contains("row=", statement.Value, StringComparison.Ordinal);
                Assert.Contains("col=", statement.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("position=", statement.Value, StringComparison.Ordinal);
            });

            var generatedCircuit = new CircuitNode();
            var executor = new PythonCommandExecutor(
                new PythonApi(new CommandManager(), generatedCircuit));
            var engine = new ManagedPythonScriptEngine(executor);
            try
            {
                engine.Execute(script, $"{exampleName}.py", new[] { pythonDirectory });
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Generated Python round-trip failed for {exampleName}.",
                    exception);
            }

            Assert.Equal(sourceCircuit.Components.Count(), generatedCircuit.Components.Count());
            Assert.Equal(sourceCircuit.Connections.Count(), generatedCircuit.Connections.Count());
            Assert.All(generatedCircuit.Components, component => Assert.True(component.HasLogicalPlacement,
                $"{exampleName}: generated {component.Name} ({component.ComponentType}) has no logical placement."));
            foreach (GroundNode ground in sourceCircuit.Components.OfType<GroundNode>())
            {
                var generatedGround = Assert.IsType<GroundNode>(
                    generatedCircuit.FindChild<ComponentNode>(ground.Name));
                Assert.Equal(ground.GroundGroup, generatedGround.GroundGroup);
            }
            Assert.All(sourceCircuit.Components.OfType<EESimulator.Core.Components.Electronics.LogicInputComponent>(), input =>
                Assert.Equal(input.State, Assert.IsType<EESimulator.Core.Components.Electronics.LogicInputComponent>(
                    generatedCircuit.FindChild<ComponentNode>(input.Name)).State));
        }
    }

    [Fact]
    public void Bundled_example_rc_low_pass_variant_probe_measures_exponential_charging()
    {
        string exampleFile = Path.Combine(FindRepositoryRoot(), "Resources", "Raw", "Examples", "rc_low_pass.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);

        var probe = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_CAP_WAVEFORM"));
        Assert.True(probe.ArrayOutputEnabled);
        Assert.Equal(50, probe.MaxReadings);
        Assert.Equal(0.0005, probe.TimeRangeDuration);

        var settings = new SimulationSettings
        {
            AnalysisType = "Transient",
            StopTime = 0.0005,
            StepTime = 1e-6,
            CaptureAllWaveforms = true
        };

        var (times, values) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, probe, settings);

        Assert.NotEmpty(values);
        Assert.True(values.Length <= probe.MaxReadings);
        Assert.InRange(values.Min(), 0.0, 0.1);
        Assert.InRange(values.Max(), 0.4, 1.05);

        for (int i = 1; i < values.Length; i++)
        {
            Assert.True(values[i] >= values[i - 1] - 1e-4,
                $"Capacitor charge must be monotonic: values[{i}]={values[i]} < values[{i - 1}]={values[i - 1]}");
        }
    }

    [Fact]
    public void Bundled_example_half_wave_rectifier_variant_probes_measure_ac_and_rectified_waveforms()
    {
        string exampleFile = Path.Combine(FindRepositoryRoot(), "Resources", "Raw", "Examples", "half_wave_rectifier.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);

        var acProbe = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_AC_WAVEFORM"));
        var recProbe = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_RECTIFIED"));

        Assert.True(acProbe.ArrayOutputEnabled);
        Assert.True(recProbe.ArrayOutputEnabled);

        var settings = new SimulationSettings
        {
            AnalysisType = "Transient",
            StopTime = 0.04,
            StepTime = 1e-4,
            CaptureAllWaveforms = true
        };

        var (acTimes, acValues) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, acProbe, settings);
        var (recTimes, recValues) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, recProbe, settings);

        Assert.NotEmpty(acValues);
        Assert.InRange(acValues.Min(), -13.0, -10.0);
        Assert.InRange(acValues.Max(), 10.0, 13.0);
        Assert.True(acValues.Max() - acValues.Min() > 20.0, "AC probe must swing > 20 V peak-to-peak.");

        Assert.NotEmpty(recValues);
        Assert.InRange(recValues.Min(), -0.5, 0.5);
        Assert.InRange(recValues.Max(), 8.0, 13.0);
    }

    [Fact]
    public void Bundled_example_555_astable_oscillator_variant_probe_measures_pulse_waveform()
    {
        string exampleFile = Path.Combine(FindRepositoryRoot(), "Resources", "Raw", "Examples", "555_astable_oscillator.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);

        var outProbe = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_OUT"));
        Assert.True(outProbe.ArrayOutputEnabled);

        var settings = new SimulationSettings
        {
            AnalysisType = "Transient",
            StopTime = 0.003,
            StepTime = 1e-5,
            CaptureAllWaveforms = true
        };

        var (times, values) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, outProbe, settings);

        Assert.NotEmpty(values);
        Assert.InRange(values.Min(), 0.0, 0.5);
        Assert.InRange(values.Max(), 4.5, 5.5);
        Assert.True(values.Max() - values.Min() >= 4.0, "Pulse output range must swing by >= 4.0 V.");
    }

    [Fact]
    public void Bundled_example_astable_multivibrator_variant_probes_measure_complementary_outputs()
    {
        string exampleFile = Path.Combine(FindRepositoryRoot(), "Resources", "Raw", "Examples", "astable_multivibrator.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);

        var probeOut1 = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_OUT1"));
        var probeOut2 = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_OUT2"));

        Assert.True(probeOut1.ArrayOutputEnabled);
        Assert.True(probeOut2.ArrayOutputEnabled);

        var settings = new SimulationSettings
        {
            AnalysisType = "Transient",
            StopTime = 0.1,
            StepTime = 5e-5,
            CaptureAllWaveforms = true
        };

        var (_, v1) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, probeOut1, settings);
        var (_, v2) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, probeOut2, settings);

        Assert.NotEmpty(v1);
        Assert.NotEmpty(v2);
        Assert.InRange(v1.Min(), 0.0, 1.5);
        Assert.InRange(v1.Max(), 3.5, 5.5);
        Assert.InRange(v2.Min(), 0.0, 1.5);
        Assert.InRange(v2.Max(), 3.5, 5.5);
    }

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "EEE.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the EEE repository root.");
    }

    private static ComponentNode FindComponent(CircuitNode circuit, string componentType, string? name = null) =>
        circuit.Components.Single(component =>
            component.ComponentType.Equals(componentType, StringComparison.OrdinalIgnoreCase) &&
            (name is null || component.Name.Equals(name, StringComparison.Ordinal)));

    private static bool AreConnected(
        CircuitNode circuit,
        ComponentNode first,
        string firstPin,
        ComponentNode second,
        string secondPin) =>
        circuit.Connections.Any(connection =>
            (connection.FromComponentId == first.Id &&
             connection.FromPin.Equals(firstPin, StringComparison.OrdinalIgnoreCase) &&
             connection.ToComponentId == second.Id &&
             connection.ToPin.Equals(secondPin, StringComparison.OrdinalIgnoreCase)) ||
            (connection.FromComponentId == second.Id &&
             connection.FromPin.Equals(secondPin, StringComparison.OrdinalIgnoreCase) &&
             connection.ToComponentId == first.Id &&
             connection.ToPin.Equals(firstPin, StringComparison.OrdinalIgnoreCase)));
}
