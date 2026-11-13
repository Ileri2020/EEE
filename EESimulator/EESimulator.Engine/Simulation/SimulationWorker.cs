using System;
using System.Threading;
using System.Threading.Tasks;
using EESimulator.Core.Models;

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

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                // 1. Advance digital logic clock / combinational elements
                DigitalLogicSimulator.Advance(circuit, settings.SolverElapsedTimeSeconds);

                // 2. Execute SpiceSharp solver
                var results = _simManager.Run(circuit, settings);

                // 3. Extract detached immutable snapshot
                var snapshot = SimulationSnapshotFactory.CreateSnapshot(circuit, settings, results);

                return (results, snapshot);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
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
        _gate.Dispose();
    }
}

