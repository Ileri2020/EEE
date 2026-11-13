using System;
using System.Collections.Generic;

namespace EESimulator.Core.Models;

/// <summary>
/// Root document node representing a complete circuit schematic project.
/// </summary>
public class CircuitNode : RootNode
{
    public string Title { get; set; } = "Untitled Circuit";

    public string Description { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public long StructureVersion { get; private set; } = 1;

    public void InvalidateStructure()
    {
        StructureVersion++;
    }

    public CircuitNode()
    {
        Name = "CircuitRoot";
        OnChildAdded += (_, _) => StructureVersion++;
        OnChildRemoved += (_, _) => StructureVersion++;
        OnModified += (_, _) => StructureVersion++;
    }

    public IEnumerable<ComponentNode> Components => FindChildren<ComponentNode>();

    public IEnumerable<ConnectionNode> Connections => FindChildren<ConnectionNode>();

    public IEnumerable<GroupNode> Groups => FindChildren<GroupNode>();
}
