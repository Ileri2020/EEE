using System;

namespace EESimulator.Core.Components.Base;

/// <summary>
/// Base class for structural and non-primitive components (wires, junctions, net labels, annotations, connectors).
/// </summary>
public abstract class GeneralComponent : ComponentBase
{
    public override string Category => "General";

    public override bool IsSimulatable => false;

    protected GeneralComponent()
    {
    }
}
