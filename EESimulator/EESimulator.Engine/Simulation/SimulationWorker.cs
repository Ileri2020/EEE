using System;
using System.Threading;
using System.Threading.Tasks;
using EESimulator.Core.Models;
using EESimulator.Formats;

namespace EESimulator.Engine.Simulation;

/// <summary>
/// Dedicated background simulation worker managing asynchronous solver execution.
/// Ensures electrical simulation runs completely decoupled from the UI thread,
/// with request coalescing and immutable snapshot publication to prevent UI freezes.
/// </summary>
public class SimulationWorker : IDisposable
{
    private readonly SimulationManager _simManager;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts = new();
    private CircuitNode? _solverCircuit;
    private long _sourceStructureVersion = -1;
    private long _sourceConfigurationVersion = -1;
    private long _sourceStateVersion = -1;
    private bool _isDisposed;

    public bool IsBusy => _gate.CurrentCount == 0;

    public SimulationWorker(SimulationManager simManager)
    {
        _simManager = simManager ?? throw new ArgumentNullException(nameof(simManager));
    }

    /// <summary>
    /// Executes a simulation step asynchronously off the UI thread and returns the solver results
    /// alongside a detached snapshot of electrical states.
    /// </summary>
    public async Task<(SimulationResultsData Results, SimulationSnapshot Snapshot)> ExecuteAsync(
        CircuitNode circuit,
        SimulationSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SimulationWorker));
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                long structureVersion = circuit.StructureVersion;
                long configurationVersion = circuit.ConfigurationVersion;
                long stateVersion = circuit.StateVersion;
                if (_solverCircuit == null ||
                    structureVersion != _sourceStructureVersion ||
                    configurationVersion != _sourceConfigurationVersion ||
                    stateVersion != _sourceStateVersion)
                {
                    CircuitNode solverCircuit = CloneCircuitAtStableVersion(
                        circuit, out structureVersion, out configurationVersion, out stateVersion);
                    _solverCircuit = solverCircuit;
                    _sourceStructureVersion = structureVersion;
                    _sourceConfigurationVersion = configurationVersion;
                    _sourceStateVersion = stateVersion;
                }

                var solverCircuitForStep = _solverCircuit
                    ?? throw new InvalidOperationException("The detached solver circuit could not be initialized.");
                // 1. Advance digital logic clock / combinational elements
                DigitalLogicSimulator.Advance(solverCircuitForStep, settings.SolverElapsedTimeSeconds);

                // 2. Execute SpiceSharp solver
                var results = _simManager.Run(solverCircuitForStep, settings);

                // 3. Extract detached immutable snapshot
                var snapshot = SimulationSnapshotFactory.CreateSnapshot(solverCircuitForStep, settings, results);

                return (results, snapshot);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static CircuitNode CloneCircuitAtStableVersion(
        CircuitNode source,
        out long structureVersion,
        out long configurationVersion,
        out long stateVersion)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            long beforeStructure = source.StructureVersion;
            long beforeConfiguration = source.ConfigurationVersion;
            long beforeState = source.StateVersion;
            string json = EeePlatformSerializer.ExportToEee(source);
            long afterStructure = source.StructureVersion;
            long afterConfiguration = source.ConfigurationVersion;
            long afterState = source.StateVersion;
            if (beforeStructure == afterStructure && beforeConfiguration == afterConfiguration && beforeState == afterState)
            {
                structureVersion = afterStructure;
                configurationVersion = afterConfiguration;
                stateVersion = afterState;
                return EeePlatformSerializer.LoadFromEeeJson(json);
            }
        }

        throw new InvalidOperationException("The circuit changed repeatedly while a simulation snapshot was being created. Retry the simulation step.");
    }

    public void CancelCurrent()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _solverCircuit = null;
        _gate.Dispose();
    }
}

