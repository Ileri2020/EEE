using System;

namespace EESimulator.Core.Components.Base;

/// <summary>
/// Base class for ideal and passive electrical components (resistors, capacitors, inductors, sources, ground).
/// Maps directly to SPICE primitive models.
/// </summary>
public abstract class ElectricalComponent : ComponentBase
{
    public override string Category => "Electrical";

    /// <summary>
    /// Default SPICE prefix for netlists (e.g., 'R', 'C', 'L', 'V', 'I').
    /// </summary>
    public abstract string SpicePrefix { get; }

    protected ElectricalComponent()
    {
    }
}
