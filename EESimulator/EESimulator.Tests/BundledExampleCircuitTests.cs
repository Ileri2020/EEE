using System.Globalization;
using System.Text.RegularExpressions;
using EEE.Services;
using EESimulator.Commands;
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

        var testPointFailures = new List<string>();
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
                Assert.Contains(from.Pins, pin => pin.Id.Equals(connection.FromPin, StringComparison.OrdinalIgnoreCase));
                Assert.Contains(to.Pins, pin => pin.Id.Equals(connection.ToPin, StringComparison.OrdinalIgnoreCase));
            }

            var testProbes = sourceCircuit.Components
                .Where(component => component.ComponentType.Equals("Probe", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.True(testProbes.Length >= 2, $"{exampleName}: expected at least two test points.");
            Assert.All(testProbes, probe =>
            {
                Assert.Contains(sourceCircuit.Connections, connection =>
                    connection.FromComponentId == probe.Id || connection.ToComponentId == probe.Id);
                Assert.True(probe.CustomProperties.ContainsKey("ExpectedVoltage"),
                    $"{exampleName}/{probe.Name}: expected voltage metadata is required.");
            });

            if (testPointExampleIds.Contains(exampleName, StringComparer.Ordinal))
            {
                Assert.NotEmpty(testProbes);
            }

            var simulation = new SpiceSharpAdapter().RunSimulation(sourceCircuit, new SimulationSettings());
            Assert.True(simulation.Success, $"{exampleName}: {simulation.Message}");
            if (simulation.Message.StartsWith("Fallback approximation used", StringComparison.Ordinal))
                testPointFailures.Add($"{exampleName}: {simulation.Message}");

            foreach (var probe in testProbes)
            {
                ConnectionNode probeConnection = sourceCircuit.Connections.Single(connection =>
                    connection.FromComponentId == probe.Id || connection.ToComponentId == probe.Id);
                string probePinId = probeConnection.FromComponentId == probe.Id ? probeConnection.FromPin : probeConnection.ToPin;
                if (!simulation.OperatingPointPinVoltages.TryGetValue((probe.Id, probePinId), out double measuredVoltage))
                {
                    if (!simulation.Message.StartsWith("Fallback approximation used", StringComparison.Ordinal))
                        testPointFailures.Add($"{exampleName}/{probe.Name}: connected node was not solved.");
                    continue;
                }

                if (!double.IsFinite(measuredVoltage))
                {
                    testPointFailures.Add($"{exampleName}/{probe.Name}: voltage is not finite.");
                    continue;
                }

                if (probe.CustomProperties.TryGetValue("ExpectedVoltage", out var expectedValue))
                {
                    if (!double.TryParse(expectedValue?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double expectedVoltage))
                    {
                        testPointFailures.Add($"{exampleName}/{probe.Name}: invalid expected voltage.");
                    }
                    else if (measuredVoltage < expectedVoltage - 1e-4 || measuredVoltage > expectedVoltage + 1e-4)
                    {
                        testPointFailures.Add($"{exampleName}/{probe.Name}: expected {expectedVoltage.ToString("G17", CultureInfo.InvariantCulture)} V, measured {measuredVoltage.ToString("G17", CultureInfo.InvariantCulture)} V.");
                    }
                }
                else
                {
                    testPointFailures.Add($"{exampleName}/{probe.Name} missing expected voltage; measured {measuredVoltage.ToString("G17", CultureInfo.InvariantCulture)} V.");
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
            engine.Execute(script, $"{exampleName}.py", new[] { pythonDirectory });

            Assert.Equal(sourceCircuit.Components.Count(), generatedCircuit.Components.Count());
            Assert.Equal(sourceCircuit.Connections.Count(), generatedCircuit.Connections.Count());
            Assert.All(generatedCircuit.Components, component => Assert.True(component.HasLogicalPlacement,
                $"{exampleName}: generated {component.Name} ({component.ComponentType}) has no logical placement."));
            Assert.All(sourceCircuit.Components.OfType<EESimulator.Core.Components.Electronics.LogicInputComponent>(), input =>
                Assert.Equal(input.State, Assert.IsType<EESimulator.Core.Components.Electronics.LogicInputComponent>(
                    generatedCircuit.FindChild<ComponentNode>(input.Name)).State));
        }

        Assert.Empty(testPointFailures);
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
}
