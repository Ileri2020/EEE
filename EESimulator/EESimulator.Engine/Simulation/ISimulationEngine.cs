using System;
using System.Collections.Generic;
using EESimulator.Core.Models;

namespace EESimulator.Engine.Simulation;

public enum AnalysisType
{
    OperatingPoint,
    DCSweep,
    Transient,
    AC
}

public class SimulationSettings
{
    public AnalysisType Analysis { get; set; } = AnalysisType.OperatingPoint;
    public double StartTime { get; set; } = 0.0;
    public double StopTime { get; set; } = 0.01;
    public double StepTime { get; set; } = 1e-5;
}

public class SimulationResultsData
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, double> OperatingPointVoltages { get; } = new();
    public Dictionary<string, double> OperatingPointCurrents { get; } = new();
    public List<double> TimePoints { get; } = new();
    public Dictionary<string, List<double>> Waveforms { get; } = new();
}

/// <summary>
/// Simulation engine abstraction, allowing SpiceSharp, Ngspice, or custom solvers
/// to be plugged in interchangeably.
/// </summary>
public interface ISimulationEngine
{
    string EngineName { get; }
    SimulationResultsData RunSimulation(CircuitNode circuit, SimulationSettings settings);
}
