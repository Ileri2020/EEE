using System;
using Microsoft.Maui.Devices;

namespace EESimulator.Engine.Simulation;

public enum PerformanceLevel
{
    HighQuality,
    Balanced,
    HighPerformance,
    LowEndDevice
}

/// <summary>
/// Controls simulation solver frequency, animation rates, particle densities,
/// and rendering complexity according to device capabilities.
/// </summary>
public class SimulationPerformanceProfile
{
    public PerformanceLevel Level { get; set; } = PerformanceLevel.Balanced;

    /// <summary>Target animation rendering frame rate (FPS).</summary>
    public int TargetAnimationFps { get; set; } = 30;

    /// <summary>Minimum interval between continuous simulation solves.</summary>
    public TimeSpan SolveInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Multiplier for electricity flow dot count (0.25 to 1.5).</summary>
    public float ParticleDensityMultiplier { get; set; } = 1.0f;

    /// <summary>Whether to calculate and render bridge hops for crossing wires.</summary>
    public bool EnableWireBridges { get; set; } = true;

    /// <summary>Maximum time-series samples retained for transient waveforms.</summary>
    public int MaxWaveformPoints { get; set; } = 500;

    /// <summary>Whether to cull off-screen components during schematic drawing.</summary>
    public bool EnableViewportCulling { get; set; } = true;

    private static SimulationPerformanceProfile? _current;
    public static SimulationPerformanceProfile Current
    {
        get => _current ??= DefaultForCurrentDevice();
        set => _current = value;
    }

    public static SimulationPerformanceProfile DefaultForCurrentDevice()
    {
        bool isMobile = DeviceInfo.Idiom == DeviceIdiom.Phone;
        return isMobile ? CreateLowEndProfile() : CreateBalancedProfile();
    }

    public static SimulationPerformanceProfile CreateHighQualityProfile() => new()
    {
        Level = PerformanceLevel.HighQuality,
        TargetAnimationFps = 60,
        SolveInterval = TimeSpan.FromMilliseconds(150),
        ParticleDensityMultiplier = 1.0f,
        EnableWireBridges = true,
        MaxWaveformPoints = 1000,
        EnableViewportCulling = true
    };

    public static SimulationPerformanceProfile CreateBalancedProfile() => new()
    {
        Level = PerformanceLevel.Balanced,
        TargetAnimationFps = 30,
        SolveInterval = TimeSpan.FromMilliseconds(200),
        ParticleDensityMultiplier = 0.8f,
        EnableWireBridges = true,
        MaxWaveformPoints = 500,
        EnableViewportCulling = true
    };

    public static SimulationPerformanceProfile CreateLowEndProfile() => new()
    {
        Level = PerformanceLevel.LowEndDevice,
        TargetAnimationFps = 25,
        SolveInterval = TimeSpan.FromMilliseconds(300),
        ParticleDensityMultiplier = 0.5f,
        EnableWireBridges = false,
        MaxWaveformPoints = 300,
        EnableViewportCulling = true
    };
}

