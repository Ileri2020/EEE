using System;
using System.Collections.Generic;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;

namespace EESimulator.Engine;

/// <summary>
/// Orchestrates circuit simulations, connects with ISimulationEngine adapters,
/// and stores execution history.
/// </summary>
public class SimulationManager
{
    private ISimulationEngine _engine;

    public ISimulationEngine Engine
    {
        get => _engine;
        set => _engine = value ?? throw new ArgumentNullException(nameof(value));
    }

    public SimulationResultsData? LastResults { get; private set; }

    public SimulationManager()
    {
        _engine = new SpiceSharpAdapter();
    }

    public SimulationManager(ISimulationEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public SimulationResultsData Run(CircuitNode circuit, SimulationSettings? settings = null)
    {
        settings ??= new SimulationSettings();
        LastResults = _engine.RunSimulation(circuit, settings);
        return LastResults;
    }
}
