using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.UI;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EEE.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EEE.ViewModels;
public partial class EEWorkspaceViewModel
{
    private sealed record ScheduledSimulationInput(
        double TimeSeconds,
        long Sequence,
        string ComponentName,
        string PropertyName,
        object? Value);

    private readonly object _scheduledInputLock = new();
    private readonly List<ScheduledSimulationInput> _scheduledSimulationInputs = new();
    private long _scheduledInputSequence;
    private bool _scheduledInputBoundaryPending;
    private double _lastPlaybackSolvedTimeSeconds;

    private List<ComponentNode> FindConnectedCircuitIsland(ComponentNode selected)
    {
        var components = Circuit.Components.ToList();
        var adjacency = components.ToDictionary(component => component.Id, _ => new HashSet<Guid>());
        foreach (var connection in Circuit.Connections)
        {
            if (adjacency.ContainsKey(connection.FromComponentId) && adjacency.ContainsKey(connection.ToComponentId))
            {
                adjacency[connection.FromComponentId].Add(connection.ToComponentId);
                adjacency[connection.ToComponentId].Add(connection.FromComponentId);
            }
        }
        foreach (var group in components.OfType<TunnelComponent>().GroupBy(tunnel => tunnel.TunnelLabel, StringComparer.OrdinalIgnoreCase))
        {
            var ids = group.Select(tunnel => tunnel.Id).ToArray();
            for (int i = 1; i < ids.Length; i++)
            {
                adjacency[ids[0]].Add(ids[i]);
                adjacency[ids[i]].Add(ids[0]);
            }
        }
        foreach (var groundGroup in components.OfType<GroundNode>().GroupBy(
                     ground => ground.GroundGroup, StringComparer.OrdinalIgnoreCase))
        {
            var groundIds = groundGroup.Select(ground => ground.Id).ToArray();
            for (int i = 1; i < groundIds.Length; i++)
            {
                adjacency[groundIds[0]].Add(groundIds[i]);
                adjacency[groundIds[i]].Add(groundIds[0]);
            }
        }

        var visited = new HashSet<Guid> { selected.Id };
        var queue = new Queue<Guid>();
        queue.Enqueue(selected.Id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in adjacency[current])
                if (visited.Add(neighbor)) queue.Enqueue(neighbor);
        }
        return components.Where(component => visited.Contains(component.Id)).ToList();
    }

    private async Task LoadCustomCircuitLibraryAsync()
    {
        var definitions = await CustomCircuitLibrary.LoadAsync();
        foreach (var definition in definitions)
            _customCircuitDefinitions[definition.Id] = definition;

        if (_remoteApi.IsConfigured)
        {
            try
            {
                var online = await _remoteApi.GetCommunityExamplesAsync();
                foreach (var example in online.Where(example => example.Category.Equals("Custom Components", StringComparison.OrdinalIgnoreCase)))
                {
                    var definition = CustomCircuitLibrary.TryReadRemoteDefinition(example);
                    if (definition == null) continue;
                    _customCircuitDefinitions[definition.Id] = definition;
                    await CustomCircuitLibrary.SaveAsync(definition);
                }
            }
            catch { /* Local custom components remain available while the optional API is offline. */ }
        }
        RefreshCustomCircuitPalette();
    }

    private void RefreshCustomCircuitPalette()
    {
        var existing = GroupedPalette.FirstOrDefault(group => group.Header == "🧩 My Circuits");
        if (existing != null) GroupedPalette.Remove(existing);
        var items = _customCircuitDefinitions.Values.OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new ComponentPaletteItem(
                $"custom:{definition.Id}", "⌗", "MAC", "#D2A8FF", "My Circuits",
                $"Reusable circuit with {definition.Ports.Count} exposed nodes", string.Empty))
            .ToList();
        if (items.Count > 0) GroupedPalette.Add(new ComponentPaletteGroup("🧩 My Circuits", items));
    }

    [RelayCommand]
    private void ToggleViewport()
    {
        if (IsSchematicViewActive)
        {
            IsScriptViewActive = true;
            IsAnalyticsViewActive = false;
            UI.Workspace.SetViewport("Script");
        }
        else if (IsScriptViewActive)
        {
            IsScriptViewActive = false;
            IsAnalyticsViewActive = true;
            UI.Workspace.SetViewport("Analytics");
            _ = RunAnalyticsAsync();
        }
        else
        {
            IsAnalyticsViewActive = false;
            IsScriptViewActive = false;
            UI.Workspace.SetViewport("Schematic");
        }
    }

    [RelayCommand]
    public async Task RunAnalyticsAsync()
    {
        if (IsAnalyticsRunning) return;
        IsAnalyticsRunning = true;
        AnalyticsOutput = "Running circuit analysis…";
        try
        {
            var (result, snapshot) = await _simWorker.ExecuteAsync(Circuit, new SimulationSettings());
            CurrentSimulationSnapshot = snapshot;
            _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            AnalyticsOutput = await Task.Run(() =>
                CircuitAnalyticsService.Analyze(Circuit, result, SelectedAnalyticsMode, SelectedComponent));
            DispatchTriggeredUiEvents();
            PublishUiSyncFrame();
            OnPropertyChanged(nameof(SelectedComponent));
        }
        catch (Exception ex)
        {
            AnalyticsOutput = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyticsRunning = false;
        }
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
        // When component sidebar opens, close control sidebar (all platforms)
        if (IsSidebarVisible)
            IsControlSidebarVisible = false;
    }

    [RelayCommand]
    private void ToggleControlSidebar()
    {
        IsControlSidebarVisible = !IsControlSidebarVisible;
        // When control sidebar opens, close component sidebar (all platforms)
        if (IsControlSidebarVisible)
            IsSidebarVisible = false;
    }

    [RelayCommand]
    private void ToggleLeftToolbar()
    {
        IsLeftToolbarVisible = !IsLeftToolbarVisible;
    }

    private Microsoft.Maui.Dispatching.IDispatcherTimer? _animTimer;
    private bool _playbackSolveRunning;
    private bool _simulationRefreshRequested;
    private bool _isApplyingScript;
    private bool _scriptCircuitRefreshPending;
    private bool _scriptExecutionHadErrors;
    private DateTime _lastPlaybackSolveUtc = DateTime.MinValue;
    private DateTime _lastPlaybackConsoleUpdateUtc = DateTime.MinValue;
    private double _pendingSolverElapsedTimeSeconds;
    private double _solverTimeSeconds;
    private double _deviceTimeSeconds;

    [RelayCommand]
    public void ToggleSimulationPlayback()
    {
        IsSimulationPlaying = !IsSimulationPlaying;
        if (IsSimulationPlaying)
        {
            if (_solverTimeSeconds <= 0)
            {
                _solverTimeSeconds = Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0);
                _deviceTimeSeconds = _solverTimeSeconds * GetConfiguredDeviceTimeScale();
            }
            _lastPlaybackSolvedTimeSeconds = _solverTimeSeconds;
            if (_animTimer == null)
            {
                _animTimer = Application.Current?.Dispatcher?.CreateTimer() ?? Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread()?.CreateTimer();
                if (_animTimer != null)
                {
                    int targetFps = Math.Clamp(SimulationPerformanceProfile.Current.TargetAnimationFps, 15, 60);
                    _animTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps);
                    _animTimer.Tick += (s, e) =>
                    {
                        if (IsSimulationPlaying)
                        {
                            if (_scheduledInputBoundaryPending)
                                return;

                            double solverDelta = _animTimer.Interval.TotalSeconds;
                            double nextInputTime = GetNextScheduledInputTime();
                            bool reachesScheduledInput = double.IsFinite(nextInputTime) &&
                                nextInputTime <= _solverTimeSeconds + solverDelta;
                            if (reachesScheduledInput)
                            {
                                solverDelta = Math.Max(0, nextInputTime - _solverTimeSeconds);
                                _scheduledInputBoundaryPending = true;
                            }

                            _pendingSolverElapsedTimeSeconds += solverDelta;
                            _solverTimeSeconds += solverDelta;
                            double scale = GetConfiguredDeviceTimeScale();
                            _deviceTimeSeconds += solverDelta * scale;
                            ElectricityAnimPhase = (ElectricityAnimPhase + 0.05f) % 1.0f;
                            AnimationFrameRequested?.Invoke();
                            if (reachesScheduledInput)
                            {
                                if (!IsSimulating && !_playbackSolveRunning)
                                    _ = RefreshPlaybackSimulationAsync();
                                return;
                            }
                            if (!IsSimulating && !_playbackSolveRunning &&
                                DateTime.UtcNow - _lastPlaybackSolveUtc >= SimulationPerformanceProfile.Current.SolveInterval)
                                _ = RefreshPlaybackSimulationAsync();
                        }
                    };
                }
            }
            _animTimer?.Start();
            StatusText = "▶ Simulation Running: Electricity flowing along circuits";
            if (_scheduledInputBoundaryPending)
                _ = RefreshPlaybackSimulationAsync();
        }

        else
        {
            _animTimer?.Stop();
            StatusText = "⏸ Simulation Paused";
        }
        CircuitChanged?.Invoke();
    }

    private async Task RefreshPlaybackSimulationAsync()
    {
        if (_playbackSolveRunning || IsSimulating || !IsSimulationPlaying)
        {
            _simulationRefreshRequested = true;
            return;
        }
        _simulationRefreshRequested = false;
        _playbackSolveRunning = true;
        IsSimulating = true;
        _lastPlaybackSolveUtc = DateTime.UtcNow;
        bool segmentCompleted = false;
        try
        {
            var settings = BuildSimulationSettings();
            settings.Analysis = AnalysisType.Transient;
            settings.SolverElapsedTimeSeconds = _pendingSolverElapsedTimeSeconds;
            _pendingSolverElapsedTimeSeconds = 0;
            settings.DeviceTimeSeconds = _deviceTimeSeconds;
            settings.StopTime = _solverTimeSeconds;
            settings.StartTime = Math.Max(0, settings.StopTime - settings.SolverElapsedTimeSeconds);
            if (settings.StopTime <= settings.StartTime)
            {
                double waveformWindow = Math.Max(settings.StepTime * 2, 0.02);
                settings.StartTime = Math.Max(0, settings.StopTime - waveformWindow);
            }
            settings.CaptureAllWaveforms = true;
            var (results, snapshot) = await _simWorker.ExecuteAsync(Circuit, settings);
            segmentCompleted = true;
            _lastPlaybackSolvedTimeSeconds = settings.StopTime;
            CurrentSimulationSnapshot = snapshot;
            if (IsWaveformDrawerOpen)
            {
                EnsureSettingsCached();
                var now = DateTime.UtcNow;
                if (IsVideoRecording || now - _lastGraphRefreshUtc >= TimeSpan.FromMilliseconds(1000.0 / _cachedGraphRefreshHz))
                {
                    GraphSimulationSnapshot = snapshot;
                    _lastGraphRefreshUtc = now;
                }
            }
            if (DateTime.UtcNow - _lastPlaybackConsoleUpdateUtc >= TimeSpan.FromSeconds(1))
            {
                // Show scalar results + only real-time-flagged probe arrays during live playback.
                ConsoleOutput = FormatResults(results) + FormatProbeArrayOutput(realtimeTickOnly: true);
                _lastPlaybackConsoleUpdateUtc = DateTime.UtcNow;
            }
            DispatchTriggeredUiEvents();
            OnPropertyChanged(nameof(SelectedComponent));
            PublishUiSyncFrame();
        }
        catch (Exception ex)
        {
            ConsoleOutput = $"Live simulation failed: {ex.Message}";
        }
        finally
        {
            _playbackSolveRunning = false;
            IsSimulating = false;
            if (_scheduledInputBoundaryPending)
            {
                if (_lastPlaybackSolvedTimeSeconds >= _solverTimeSeconds - 1e-9)
                {
                    _scheduledInputBoundaryPending = false;
                    ApplyDueScheduledInputs(_solverTimeSeconds);
                }
                else if (IsSimulationPlaying && segmentCompleted)
                {
                    _simulationRefreshRequested = true;
                }
            }
            if (_simulationRefreshRequested)
            {
                _simulationRefreshRequested = false;
                if (IsSimulationPlaying)
                    _ = RefreshPlaybackSimulationAsync();
                else
                    _ = SimulateAsync();
            }
        }
    }

    private void RequestSimulationRefresh()
    {
        _simulationRefreshRequested = true;
        if (_isApplyingScript) return;
        if (IsSimulationPlaying)
        {
            if (!_playbackSolveRunning && !IsSimulating)
                _ = RefreshPlaybackSimulationAsync();
            return;
        }
        _ = SimulateAsync();
    }

    [RelayCommand]
    public void ExecuteCustomAction(EEE.Services.CustomControlButton button)
    {
        if (button == null) return;
        var act = button.ActionName;

        switch (act)
        {
            case "Play / Pause Simulation":
                ToggleSimulationPlayback();
                break;
            case "Run Operating Point":
                _ = SimulateAsync();
                break;
            case "Pan Mode":
                SetPanTool();
                break;
            case "Select Mode":
                SetSelectTool();
                break;
            case "Component Edit Dialog":
                if (EditingComponent != null)
                    OpenEditDialog(EditingComponent);
                else
                {
                    var firstComp = Circuit.FindChildren<ComponentNode>().FirstOrDefault();
                    if (firstComp != null) OpenEditDialog(firstComp);
                    else StatusText = "Select a component to edit";
                }
                break;
            case "Move Component":
                ActiveTool = "Move";
                StatusText = "Move tool active: drag component";
                break;
            case "Draw Wire":
                ActiveTool = "Wire";
                SelectTool("Wire");
                StatusText = "Wire tool active: click or drag pins";
                break;
            case "Zoom In":
                SelectTool("ZoomIn");
                break;
            case "Zoom Out":
                SelectTool("ZoomOut");
                break;
            case "Reset / Fit View":
                SelectTool("FitAll");
                break;
            case "Undo":
                Undo();
                break;
            case "Redo":
                Redo();
                break;
            case "Toggle Component Palette":
                ToggleSidebar();
                break;
            case "Toggle Waveform Drawer":
                ToggleWaveformDrawer();
                break;
            case "Toggle Script / Schematic":
                ToggleViewport();
                break;
            case "Open Help":
                ToggleHelp();
                break;
            case "Open Settings":
                _ = OpenSettings();
                break;
            case "Clear Circuit":
                ClearCircuitForScript();
                ScriptText = _sync.CurrentScript;
                break;
            default:
                StatusText = $"Action [{button.Label}]: {act}";
                break;
        }
    }

    [RelayCommand]
    public void DeleteCustomButton(EEE.Services.CustomControlButton button)
    {
        if (button != null)
        {
            var lbl = button.Label;
            EEE.Services.ControlSidebarService.DeleteButton(button);
            StatusText = $"Disconnected and deleted control button [{lbl}]";
        }
    }

    [RelayCommand]
    private async Task GenerateSchemaScriptAsync()
    {
        if (IsAiSchemaRequestInProgress) return;
        bool answeringClarification = IsAwaitingAiReply;
        string requestPrompt = answeringClarification ? _pendingAiPrompt : AiSchemaPrompt.Trim();
        string clarificationAnswer = answeringClarification ? AiSchemaPrompt.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(requestPrompt) || (answeringClarification && string.IsNullOrWhiteSpace(clarificationAnswer)))
        {
            AiSchemaStatus = answeringClarification
                ? "Enter an answer to the AI clarification first."
                : "Describe the schematic change you want first.";
            return;
        }

        try
        {
            IsAiSchemaRequestInProgress = true;
            AiSchemaStatus = "Generating a script draft…";
            string currentCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            if (Circuit.Components.Any() &&
                (_simManager.LastResults == null || !string.Equals(_lastSimulationCircuitSnapshot, currentCircuitSnapshot, StringComparison.Ordinal)))
            {
                AiSchemaStatus = "Measuring the current schematic…";
                var (_, snapshot) = await _simWorker.ExecuteAsync(Circuit, new SimulationSettings());
                CurrentSimulationSnapshot = snapshot;
                _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
                DispatchTriggeredUiEvents();
                PublishUiSyncFrame();
            }
            var draft = await _remoteApi.GenerateSchemaScriptAsync(
                requestPrompt,
                ScriptText,
                ConsoleOutput,
                Circuit,
                _simManager.LastResults,
                IsAiEditMode,
                clarificationAnswer,
                answeringClarification);
            if (!answeringClarification && draft.Questions.Count > 0 && string.IsNullOrWhiteSpace(draft.Script))
            {
                _pendingAiPrompt = requestPrompt;
                IsAwaitingAiReply = true;
                AiSchemaPrompt = string.Empty;
                AiSchemaStatus = $"AI needs one clarification: {draft.Questions[0]} Enter your reply and submit once; it will then return a script without another question.";
                return;
            }
            if (answeringClarification)
            {
                IsAwaitingAiReply = false;
                _pendingAiPrompt = string.Empty;
                if (string.IsNullOrWhiteSpace(draft.Script))
                {
                    AiSchemaPrompt = string.Empty;
                    AiSchemaStatus = "The AI did not return a script after your answer. Start a new request with any missing details included.";
                    return;
                }
            }
            if (draft.Script.Trim().Length == 0)
            {
                AiSchemaStatus = draft.Questions.FirstOrDefault() ?? "The AI service returned an empty script draft.";
                return;
            }

            string normalizedScript = NormalizeAiScriptDraft(draft.Script);
            if (string.IsNullOrWhiteSpace(normalizedScript))
            {
                AiSchemaStatus = string.Join(" ", draft.Questions.Count > 0
                    ? draft.Questions
                    : new[] { "The AI service returned an empty script draft." });
                return;
            }
            string? draftError = ValidateAiScriptDraft(normalizedScript);
            if (draftError != null)
            {
                AiSchemaStatus = $"The AI returned an unsupported script ({draftError}). No draft was applied; try asking again with fewer details.";
                return;
            }

            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                ScriptText = normalizedScript;
                AiSchemaPrompt = string.Empty;
                IsAnalyticsViewActive = false;
                IsScriptViewActive = true;
                UI.Workspace.SetViewport("Script");
                OnPropertyChanged(nameof(ScriptText));
                OnPropertyChanged(nameof(IsScriptViewActive));
            });

            var draftNotes = new List<string>();
            if (!string.IsNullOrWhiteSpace(draft.Summary)) draftNotes.Add(draft.Summary);
            if (draft.Warnings.Count > 0) draftNotes.Add("Warnings: " + string.Join("; ", draft.Warnings));
            draftNotes.Add("Review the draft before Run.");
            AiSchemaStatus = string.Join(" ", draftNotes);
        }
        catch (Exception ex)
        {
            AiSchemaStatus = $"AI request failed: {ex.Message}";
        }
        finally
        {
            IsAiSchemaRequestInProgress = false;
        }
    }

    private static string NormalizeAiScriptDraft(string? script)
    {
        if (string.IsNullOrWhiteSpace(script)) return string.Empty;

        var normalized = script.Trim().Replace("\r\n", "\n");
        if (!normalized.StartsWith("```", StringComparison.Ordinal)) return normalized;

        int firstLineEnd = normalized.IndexOf('\n');
        if (firstLineEnd > 0)
            normalized = normalized[(firstLineEnd + 1)..];
        if (normalized.EndsWith("```", StringComparison.Ordinal))
            normalized = normalized[..^3].TrimEnd();

        return normalized.Trim();
    }

    private static readonly HashSet<string> SupportedAiScriptMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "add_component", "add_ttl", "connect", "set_position", "set_property",
        "set_component_property", "rename_pin", "set_logic", "toggle_logic",
        "set_waveform", "set_live_probe", "fire_event", "toggle_event",
        "rotate_component", "stretch_component", "simulate",
        "add_node", "add_resistor", "add_capacitor", "add_inductor", "add_ground",
        "add_wire", "add_voltage_source", "add_microphone", "toggle_left_toolbar"
    };

    private static string? ValidateAiScriptDraft(string script)
    {
        var lines = script.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var match = Regex.Match(line, @"^eesim\.(?<method>[A-Za-z_]\w*)\s*\(.*\)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success || line.Contains("f\"", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("f'", StringComparison.OrdinalIgnoreCase))
                return $"line {index + 1} is not a single literal eesim call";
            if (!SupportedAiScriptMethods.Contains(match.Groups["method"].Value))
                return $"line {index + 1} uses unsupported call '{match.Groups["method"].Value}'";
        }

        return null;
    }

    private Dictionary<string, object?> DispatchScriptApiCall(string script)
    {
        ConsoleOutput += $">>> {script}\n";
        _lastScriptResult = null;
        string? result = DispatchApiCall(script);
        object? value = _lastScriptResult ?? result;
        bool failed = result is null ||
            result.StartsWith("[error]", StringComparison.OrdinalIgnoreCase);
        if (result?.StartsWith("[unknown", StringComparison.OrdinalIgnoreCase) == true)
        {
            bool success = _pythonCommandExecutor.ExecuteScript(script, out string message);
            failed = !success;
            result = success
                ? (message == "Success" ? null : message)
                : $"[error] {message}";
            value = _pythonCommandExecutor.LastResult;
        }

        if (result is not null)
            ConsoleOutput += result + "\n";

        if (failed || result?.StartsWith("[unknown", StringComparison.OrdinalIgnoreCase) == true)
            _scriptExecutionHadErrors = true;

        return new Dictionary<string, object?>
        {
            ["success"] = !failed,
            ["message"] = result ?? "Script API call produced no result.",
            ["result"] = value
        };
    }

    private void ClearCircuitForScript(bool resetScript = true)
    {
        foreach (var child in Circuit.Children.ToList())
            Circuit.RemoveChild(child);

        CommandManager.Clear();
        if (resetScript)
        {
            _sync.Reset();
            ScriptText = _sync.CurrentScript;
        }
        SelectedComponent = null;
        SelectedConnection = null;
        WaveformComponent = null;
        WaveformSignals.Clear();
        WaveformTraceCandidates.Clear();
        OnPropertyChanged(nameof(WaveformGraphSummary));
        if (resetScript)
            IsWaveformDrawerOpen = false;
        ConsoleOutput = string.Empty;
        StatusText = resetScript ? "Circuit and script cleared" : "Circuit cleared by script";
        ProjectName = "Untitled.eesim";
        CanUndo = false;
        CanRedo = false;
        CurrentSimulationSnapshot = null;
        ResetPendingUiSnapshots();
        CircuitChanged?.Invoke();
    }

    private static async Task<string> EnsurePythonModuleDirectoryAsync()
    {
        string moduleDirectory = Path.Combine(FileSystem.CacheDirectory, "Python");
        Directory.CreateDirectory(moduleDirectory);

        foreach (string module in new[] { "eesim.py", "circuit.py" })
        {
            await using Stream source = await FileSystem.OpenAppPackageFileAsync($"Python/{module}");
            await using FileStream destination = File.Create(Path.Combine(moduleDirectory, module));
            await source.CopyToAsync(destination);
        }

        return moduleDirectory;
    }

    [RelayCommand]
    private async Task RunScriptAsync()
    {
        // If a simulation is currently running, cancel it immediately so the new script
        // doesn't have to wait up to minutes for the old transient solve to complete.
        if (_isApplyingScript)
        {
            StatusText = "Script is already being applied — please wait a moment.";
            return;
        }

        if (IsSimulating || _playbackSolveRunning)
        {
            // Cancel the in-flight solve and stop live playback before proceeding.
            _simWorker.CancelCurrent();
            IsSimulationPlaying = false;
            _animTimer?.Stop();
            // Give the cancelled task a brief moment to release the semaphore gate.
            await Task.Delay(80);
            _playbackSolveRunning = false;
            IsSimulating = false;
            _simulationRefreshRequested = false;
        }

        _isApplyingScript = true;
        _scriptCircuitRefreshPending = false;
        try
        {
            StatusText    = "Running script…";
            ConsoleOutput = "";
            _scriptExecutionHadErrors = false;
            string pythonModuleDirectory = await EnsurePythonModuleDirectoryAsync();
            await _pythonScriptEngine.ExecuteAsync(
                ScriptText,
                "workspace.py",
                new[] { pythonModuleDirectory },
                output =>
                {
                    if (Microsoft.Maui.ApplicationModel.MainThread.IsMainThread)
                        ConsoleOutput += output;
                    else
                        Microsoft.Maui.ApplicationModel.MainThread
                            .InvokeOnMainThreadAsync(() => ConsoleOutput += output)
                            .GetAwaiter()
                            .GetResult();
                });
            bool scriptHadErrors = _scriptExecutionHadErrors;

            ApplyLogicalSchematicLayout();

            // Switch the viewport to show the new circuit immediately after the script builds it.
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsAnalyticsViewActive = false;
                IsScriptViewActive    = false;
                UI.Workspace.SetViewport("Schematic");
                OnPropertyChanged(nameof(IsSchematicViewActive));
                OnPropertyChanged(nameof(IsScriptViewActive));
                CircuitChanged?.Invoke();
                _scriptCircuitRefreshPending = false;
            });

            var commandOutput = ConsoleOutput;
            _simulationRefreshRequested = false;
            IsSimulating = true;
            StatusText = scriptHadErrors
                ? "Script had errors. Simulating the resulting circuit state for AI feedback…"
                : "Changes applied. Running operating-point simulation for AI feedback…";
            var (feedback, snapshot) = await _simWorker.ExecuteAsync(Circuit, new SimulationSettings());
            CurrentSimulationSnapshot = snapshot;
            _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            ConsoleOutput = commandOutput + "\n" + FormatResults(feedback) + FormatProbeArrayOutput();
            DispatchTriggeredUiEvents();
            OnPropertyChanged(nameof(SelectedComponent));
            AiSchemaStatus = feedback.Success
                ? scriptHadErrors
                    ? "Some script commands failed. Simulation feedback reflects the resulting partial state; the next AI request includes the errors and measurements."
                    : "Simulation feedback refreshed. The next AI request includes per-component measurements."
                : $"Simulation feedback unavailable: {feedback.Message}";
            StatusText = feedback.Success
                ? scriptHadErrors ? "Script partially applied; simulation complete" : "Script applied and simulated ✓"
                : scriptHadErrors ? "Script had errors; simulation failed" : "Script applied; simulation failed";
        }
        catch (Exception ex)
        {
            if (PythonApi.IsVideoRecording)
                PythonApi.StopVideoRecording();
            ConsoleOutput += $"ERROR: {ex.Message}\n";
            StatusText = "Script error";
        }
        finally
        {
            IsSimulating = false;
            OnPropertyChanged(nameof(SelectedComponent));
            _isApplyingScript = false;
            if (_scriptCircuitRefreshPending)
            {
                _scriptCircuitRefreshPending = false;
                CircuitChanged?.Invoke();
            }
            PublishUiSyncFrame();
            if (_simulationRefreshRequested)
            {
                _simulationRefreshRequested = false;
                if (IsSimulationPlaying)
                    _ = RefreshPlaybackSimulationAsync();
                else
                    _ = SimulateAsync();
            }
        }

        await Task.CompletedTask;
    }

    public SimulationSettings BuildSimulationSettings()
    {
        var analysis = SelectedAnalysisType?.ToLowerInvariant() switch
        {
            "transient" => AnalysisType.Transient,
            "ac" => AnalysisType.AC,
            "dcsweep" or "dc sweep" => AnalysisType.DCSweep,
            _ => AnalysisType.OperatingPoint
        };

        double scale = GetConfiguredDeviceTimeScale();
        bool scaleEnabled = Preferences.Default.Get("Settings.EnableTimeScaling", false);
        bool automaticStep = Preferences.Default.Get("Settings.AutomaticTimeStepSelection", true);
        double configuredStart = Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0);
        double configuredDuration = Math.Max(Preferences.Default.Get("Settings.SimulationDurationSeconds", 1.0), 1e-6);
        double configuredStep = Math.Max(Preferences.Default.Get("Settings.SimulationTimeStepSeconds", 0.04), 1e-6);
        double solverStart = scaleEnabled ? configuredStart : (SimTransientStartTime >= 0 ? SimTransientStartTime : configuredStart);
        double solverStop = scaleEnabled ? configuredStart + configuredDuration :
            (SimTransientStopTime > solverStart ? SimTransientStopTime : solverStart + configuredDuration);
        double solverStep = scaleEnabled ? configuredStep : (SimTransientStepTime > 0 ? SimTransientStepTime : configuredStep);
        // AutomaticTimeStep: cap at (window/200) so each adaptive solve is at most 200 SpiceSharp steps.
        // 200 is fast enough to feel real-time on mobile yet precise enough for most circuits.
        if (automaticStep)
            solverStep = Math.Min(solverStep, Math.Max((solverStop - solverStart) / 200.0, 1e-6));
        return new SimulationSettings
        {
            Analysis = analysis,
            StartTime = solverStart,
            StopTime = solverStop,
            StepTime = solverStep,
            TimeScalingEnabled = scaleEnabled,
            DeviceTimeScale = scaleEnabled ? scale : 1.0,
            SolverElapsedTimeSeconds = analysis == AnalysisType.Transient
                ? Math.Max(0, SimTransientStopTime - SimTransientStartTime)
                : 0,
            DeviceTimeSeconds = _deviceTimeSeconds > 0 ? _deviceTimeSeconds : solverStart * scale,
            AcSweepType = SimAcSweepType ?? "Decade",
            AcPoints = SimAcPoints > 0 ? SimAcPoints : 10,
            AcStartFrequency = SimAcStartFrequency > 0 ? SimAcStartFrequency : 1.0,
            AcStopFrequency = SimAcStopFrequency > SimAcStartFrequency ? SimAcStopFrequency : 1e5,
            DcSweepSource = string.IsNullOrWhiteSpace(SimDcSweepSource) ? "V1" : SimDcSweepSource.Trim(),
            DcStart = SimDcStart,
            DcStop = SimDcStop,
            DcStep = SimDcStep != 0 ? SimDcStep : 0.1
        };
    }

    private static double GetConfiguredDeviceTimeScale()
    {
        string simulationUnit = Preferences.Default.Get("Settings.SimulationTimeUnit", "seconds");
        string deviceUnit = Preferences.Default.Get("Settings.DeviceTimeUnit", "hours");
        double simulationAmount = Math.Max(Preferences.Default.Get("Settings.SimulationTimeAmount", 30.0), 1e-9);
        double deviceAmount = Math.Max(Preferences.Default.Get("Settings.DeviceTimeAmount", 1.0), 1e-9);
        return deviceAmount * SettingsViewModel.TimeUnitSeconds(deviceUnit) /
               (simulationAmount * SettingsViewModel.TimeUnitSeconds(simulationUnit));
    }

    [RelayCommand]
    public void OpenSimSettings() => IsSimSettingsVisible = true;

    [RelayCommand]
    public void CloseSimSettings() => IsSimSettingsVisible = false;

    [RelayCommand]
    public async Task RunConfiguredSimulationAsync()
    {
        IsSimSettingsVisible = false;
        await SimulateAsync();
    }

    [RelayCommand]
    public void ViewSpiceNetlist()
    {
        var settings = BuildSimulationSettings();
        GeneratedSpiceNetlist = SpiceNetlistExporter.ExportToSpice(Circuit, settings);
        IsNetlistModalVisible = true;
    }

    [RelayCommand]
    public void CloseSpiceNetlist() => IsNetlistModalVisible = false;

    [RelayCommand]
    public async Task CopySpiceNetlistAsync()
    {
        if (!string.IsNullOrEmpty(GeneratedSpiceNetlist))
        {
            await Clipboard.Default.SetTextAsync(GeneratedSpiceNetlist);
            StatusText = "SPICE Netlist copied to clipboard ✓";
        }
    }

    [RelayCommand]
    private async Task SimulateAsync()
    {
        if (_playbackSolveRunning || IsSimulating)
        {
            _simulationRefreshRequested = true;
            return;
        }
        _simulationRefreshRequested = false;
        try
        {
            IsSimulating  = true;
            StatusText    = $"Simulating ({SelectedAnalysisType})…";
            ConsoleOutput = "";

            var settings = BuildSimulationSettings();
            var (results, snapshot) = await _simWorker.ExecuteAsync(Circuit, settings);
            CurrentSimulationSnapshot = snapshot;
            if (settings.Analysis == AnalysisType.Transient)
            {
                double scale = settings.TimeScalingEnabled ? settings.DeviceTimeScale : 1.0;
                _solverTimeSeconds = settings.StopTime;
                _deviceTimeSeconds = settings.DeviceTimeSeconds +
                    Math.Max(0, settings.StopTime - settings.StartTime) * scale;
            }
            _ = Task.Run(() =>
            {
                _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            });
            DispatchTriggeredUiEvents();

            ConsoleOutput = FormatResults(results) + FormatProbeArrayOutput();
            AiSchemaStatus = results.Success
                ? "Simulation feedback refreshed for the next AI request."
                : $"Simulation feedback unavailable: {results.Message}";
            StatusText    = results.Success
                ? $"{settings.Analysis} simulation complete ✓"
                : $"Sim error: {results.Message}";
        }
        catch (Exception ex)
        {
            ConsoleOutput = $"Simulation failed: {ex.Message}";
            StatusText    = "Simulation error";
        }
        finally
        {
            IsSimulating = false;
            OnPropertyChanged(nameof(SelectedComponent));
            PublishUiSyncFrame();
            if (_simulationRefreshRequested)
            {
                _simulationRefreshRequested = false;
                if (IsSimulationPlaying)
                    _ = RefreshPlaybackSimulationAsync();
                else
                    _ = SimulateAsync();
            }
        }
    }

    [RelayCommand]
    private void AddComponentToCircuit(ComponentPaletteItem item)
    {
        var name = GetNextReference(item.Type);
        (double X, double Y) position = ViewportComponentPlacementRequested?.Invoke() ?? (200, 150);
        double cx = position.X;
        double cy = position.Y;

        if (item.Type.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            var definitionId = item.Type["custom:".Length..];
            if (!_customCircuitDefinitions.TryGetValue(definitionId, out var definition))
            {
                StatusText = "That saved custom component could not be loaded.";
                return;
            }
            var custom = new CustomCircuitComponent { Name = name, Reference = name, X = cx, Y = cy };
            custom.SetDefinition(definition);
            var customCommand = new AddComponentCommand(Circuit, custom) { Origin = CommandOrigin.Schematic };
            CommandManager.ExecuteCommand(customCommand);
            ApplyLogicalSchematicLayout();
            StatusText = $"Added custom component {name} ({definition.Name})";
            CircuitChanged?.Invoke();
            return;
        }

        var cmd = new AddComponentCommand(Circuit, item.Type, name, cx, cy, 0)
        {
            Origin = CommandOrigin.Schematic
        };
        CommandManager.ExecuteCommand(cmd);
        ApplyLogicalSchematicLayout();
        StatusText = $"Added {name}";

        // Notify the canvas to redraw immediately
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    private void ClearCircuitAndScript()
    {
        ClearCircuitForScript();
    }

    [RelayCommand]
    private void NewCircuit()
    {
        foreach (var child in Circuit.Children.ToList())
            Circuit.RemoveChild(child);

        CommandManager.Clear();
        _sync.Reset();
        ScriptText    = _sync.CurrentScript;
        SelectedComponent = null;
        SelectedConnection = null;
        WaveformComponent = null;
        WaveformSignals.Clear();
        WaveformTraceCandidates.Clear();
        OnPropertyChanged(nameof(WaveformGraphSummary));
        IsWaveformDrawerOpen = false;
        ConsoleOutput = string.Empty;
        StatusText    = "New circuit";
        ProjectName   = "Untitled.eesim";
        CanUndo       = false;
        CanRedo       = false;
        CurrentSimulationSnapshot = null;
        ResetPendingUiSnapshots();
        CircuitChanged?.Invoke();
    }

    // Called by the canvas when user clicks a component
    public void OnComponentSelected(ComponentNode comp)
    {
        SelectedConnection = null;
        SelectedComponent = comp;
        SelectedComponentEventTriggers.Clear();
        foreach (var trigger in comp.EventTriggers)
            SelectedComponentEventTriggers.Add(trigger);
        EditName = comp.Reference;
        EditType = comp.ComponentType;
        EditDescription = comp.CustomProperties.TryGetValue("Description", out var dVal) ? dVal?.ToString() ?? comp.Description : comp.Description;
        EditBreakingCurrent = comp.BreakingCurrent;

        var param = comp.ElectricalParameters.FirstOrDefault();
        EditParamName = param.Key ?? "Value";
        EditParamValue = param.Value;
        EditShowLiveProbe = comp.ShowLiveProbe;
        EditShowWaveform = comp.ShowWaveform;
        if (comp is ACSourceComponent acSource)
        {
            EditAcSourceWaveformType = acSource.WaveformType;
            EditAcSourceVoltage = acSource.Voltage;
            EditAcSourceFrequency = acSource.Frequency;
            EditAcSourcePhaseDegrees = acSource.PhaseDegrees;
            EditAcSourceOffset = acSource.Offset;
        }

        string paramStr = param.Key != null ? $"{param.Key}={param.Value:G4}" : "";
        ActiveComponentInfo = $"{comp.Reference}  [{comp.ComponentType}]  ({comp.X:F0}, {comp.Y:F0})  {paramStr}";
        StatusText = $"Selected {comp.Reference} [{comp.ComponentType}]";
    }

    public void OnLogicInputToggled(ComponentNode component)
    {
        StatusText = $"{component.Reference} toggled";
        RequestSimulationRefresh();
        CircuitChanged?.Invoke();
    }

    public void OnConnectionSelected(ConnectionNode connection)
    {
        SelectedComponent = null;
        SelectedConnection = connection;
        SelectedDescriptionTab = "Properties";
        EditConnectionNetName = connection.NetName;
        EditConnectionResistance = connection.Resistance;
        ActiveComponentInfo = $"Wire {connection.NetName}: {connection.FromPin} → {connection.ToPin}";
        StatusText = $"Selected wire {connection.NetName}";
    }


}
