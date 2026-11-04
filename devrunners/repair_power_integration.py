from pathlib import Path

workspace = Path(__file__).resolve().parent.parent

# Playback: respect the global enable flag and implement separate stop behavior.
p = workspace / "ViewModels" / "EEWorkspaceViewModel.cs"
s = p.read_text(encoding="utf-8")
s = s.replace('''                            double scale = GetConfiguredDeviceTimeScale();
                            _deviceTimeSeconds += solverDelta * scale;''', '''                            bool timeScalingEnabled = Preferences.Default.Get("Settings.EnableTimeScaling", false);
                            double scale = timeScalingEnabled ? GetConfiguredDeviceTimeScale() : 1.0;
                            _deviceTimeSeconds += solverDelta * scale;''', 1)
s = s.replace('''        PythonApi.OnSimulationStopRequested += () =>
        {
            if (IsSimulationPlaying) ToggleSimulationPlayback();
        };''', '''        PythonApi.OnSimulationStopRequested += () =>
        {
            if (IsSimulationPlaying) ToggleSimulationPlayback();
            _pendingSolverElapsedTimeSeconds = 0;
            _solverTimeSeconds = Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0);
            _deviceTimeSeconds = _solverTimeSeconds * (Preferences.Default.Get("Settings.EnableTimeScaling", false)
                ? GetConfiguredDeviceTimeScale() : 1.0);
            StatusText = "■ Simulation Stopped";
            CircuitChanged?.Invoke();
        };''', 1)
# Insert named power-system dispatch cases.
needle = '                default:\n'
cases = '''                case "add_single_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSinglePhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_voltage", 230), NamedD(n, "frequency", 50), NamedD(n, "length_km", 1),
                        NamedD(n, "resistance_per_km", 0.2), NamedD(n, "inductance_per_km", 1e-3),
                        NamedD(n, "capacitance_per_km", 10e-9), NamedD(n, "conductance_per_km", 0), NamedS(n, "model_type", "Short Line"));
                    return $"[ok] single-phase transmission line {Unquote(args[0])} added";
                }

                case "add_two_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddTwoPhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_voltage", 240), NamedD(n, "frequency", 60), NamedD(n, "length_km", 1),
                        NamedD(n, "phase_displacement", 90), NamedS(n, "configuration", "TrueTwoPhase"));
                    return $"[ok] two-phase transmission line {Unquote(args[0])} added";
                }

                case "add_three_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddThreePhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_line_voltage", 11000), NamedD(n, "frequency", 50), NamedD(n, "length_km", 1),
                        NamedS(n, "phase_sequence", "ABC"), NamedS(n, "connection", "Three-wire"), NamedS(n, "model_type", "Short"));
                    return $"[ok] three-phase transmission line {Unquote(args[0])} added";
                }

                case "add_ac_transformer" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACTransformer(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "primary_voltage", 230), NamedD(n, "secondary_voltage", 115), NamedD(n, "apparent_power", 1000),
                        NamedD(n, "frequency", 50), NamedS(n, "connection", "Single-phase"));
                    return $"[ok] AC transformer {Unquote(args[0])} added";
                }

                case "add_synchronous_generator" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSynchronousGenerator(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_voltage", 400), NamedD(n, "rated_frequency", 50), NamedD(n, "rated_power", 10000),
                        (int)NamedD(n, "phases", 3), (int)NamedD(n, "poles", 4));
                    return $"[ok] synchronous generator {Unquote(args[0])} added";
                }

                case "add_induction_motor" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddInductionMotor(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_voltage", 400), NamedD(n, "rated_power", 1500), NamedD(n, "frequency", 50), (int)NamedD(n, "poles", 4));
                    return $"[ok] induction motor {Unquote(args[0])} added";
                }

                case "add_ac_bulb" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACBulb(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_voltage", 230), NamedD(n, "rated_power", 60), NamedB(n, "initially_on", true), NamedD(n, "dimming_percent", 100));
                    return $"[ok] AC bulb {Unquote(args[0])} added";
                }

                case "add_ac_fan" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACFan(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "rated_voltage", 230), NamedD(n, "rated_power", 75), NamedD(n, "power_factor", 0.8), (int)NamedD(n, "selected_speed", 3));
                    return $"[ok] AC fan {Unquote(args[0])} added";
                }

                case "add_solar_panel" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSolarPanel(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "maximum_power_rating", 300), NamedD(n, "open_circuit_voltage", 40),
                        NamedD(n, "short_circuit_current", 9), NamedD(n, "irradiance", 1000));
                    return $"[ok] solar panel {Unquote(args[0])} added";
                }

                case "add_battery" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddBattery(Unquote(args[0]), NamedD(n, "position_x", 100), NamedD(n, "position_y", 100),
                        NamedD(n, "nominal_voltage", 12), NamedD(n, "capacity_ah", 100),
                        NamedD(n, "initial_soc_percent", 80), NamedS(n, "chemistry", "Lithium-ion"));
                    return $"[ok] battery {Unquote(args[0])} added";
                }

'''
if 'case "add_single_phase_transmission_line"' not in s:
    s = s.replace(needle, cases + needle, 1)
# Helpers.
s = s.replace('''    private static double NamedD(Dictionary<string, string> named, string key, double fallback = 0) =>
        named.TryGetValue(key, out string? value) ? ParseD(value) : fallback;
''', '''    private static double NamedD(Dictionary<string, string> named, string key, double fallback = 0) =>
        named.TryGetValue(key, out string? value) ? ParseD(Unquote(value)) : fallback;

    private static string NamedS(Dictionary<string, string> named, string key, string fallback) =>
        named.TryGetValue(key, out string? value) ? Unquote(value) : fallback;

    private static bool NamedB(Dictionary<string, string> named, string key, bool fallback)
    {
        return named.TryGetValue(key, out string? value) && bool.TryParse(Unquote(value), out bool parsed) ? parsed : fallback;
    }
''', 1)
p.write_text(s, encoding="utf-8")
