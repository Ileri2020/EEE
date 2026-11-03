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

    // Transient parameters
    public double StartTime { get; set; } = 0.0;
    public double StopTime { get; set; } = 0.01;
    public double StepTime { get; set; } = 1e-5;

    /// <summary>Scale elapsed solver seconds for slow device-level state updates.</summary>
    public bool TimeScalingEnabled { get; set; }
    public double DeviceTimeScale { get; set; } = 1.0;
    /// <summary>Elapsed numerical time since the prior dynamic-state update.</summary>
    public double SolverElapsedTimeSeconds { get; set; }
    /// <summary>Accumulated effective component time used by schedules and environmental profiles.</summary>
    public double DeviceTimeSeconds { get; set; }

    // AC Sweep parameters
    public string AcSweepType { get; set; } = "Decade"; // Decade, Octave, Linear
    public int AcPoints { get; set; } = 10;
    public double AcStartFrequency { get; set; } = 1.0;
    public double AcStopFrequency { get; set; } = 1e5;

    // DC Sweep parameters
    public string DcSweepSource { get; set; } = "V1";
    public double DcStart { get; set; } = 0.0;
    public double DcStop { get; set; } = 5.0;
    public double DcStep { get; set; } = 0.1;
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
