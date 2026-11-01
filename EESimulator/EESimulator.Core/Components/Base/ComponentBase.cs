using System;
using System.Collections.Generic;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Base;

/// <summary>
/// Root abstract base for all modular schematic and circuit components.
/// Inherits directly from ComponentNode (and RootNode) for Blender-like scene hierarchy.
/// </summary>
public abstract class ComponentBase : ComponentNode
{
    /// <summary>
    /// Stable universal type identifier (e.g., 'electrical.resistor', 'electronics.diode')
    /// used for Python scripting, serialization, undo/redo commands, and plugin systems.
    /// </summary>
    public abstract string StableTypeId { get; }

    /// <summary>
    /// Category for palette, sidebar, and grouping.
    /// </summary>
    public virtual string Category => "General";

    /// <summary>
    /// Human-friendly description of this component module.
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// Symbol rendering key for schematic viewport.
    /// </summary>
    public virtual string SymbolKey => ComponentType;

    /// <summary>
    /// SPICE / Simulation model name.
    /// </summary>
    public virtual string ModelName => string.Empty;

    /// <summary>
    /// Whether this component has a valid simulation model (SpiceSharp/nodal).
    /// </summary>
    public virtual bool IsSimulatable => true;

    protected ComponentBase()
    {
    }
}
