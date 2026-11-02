using System;

namespace EESimulator.Core.Components.Base;

/// <summary>
/// Base class for active electronic components and semiconductors (diodes, LEDs, BJTs, MOSFETs, OpAmps, ICs).
/// </summary>
public abstract class ElectronicComponent : ComponentBase
{
    public override string Category => "Electronics";

    /// <summary>SPICE subcircuit/model prefix for electronic components.</summary>
    public virtual string SpicePrefix => "X";

    /// <summary>
    /// Model name or part number (e.g., '1N4148', '2N2222', 'LM741').
    /// </summary>
    public string PartNumber { get; set; } = string.Empty;

    protected ElectronicComponent()
    {
    }
}
