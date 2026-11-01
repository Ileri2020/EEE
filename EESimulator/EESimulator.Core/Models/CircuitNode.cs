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

    public CircuitNode()
    {
        Name = "CircuitRoot";
    }

    public IEnumerable<ComponentNode> Components => FindChildren<ComponentNode>();

    public IEnumerable<ConnectionNode> Connections => FindChildren<ConnectionNode>();

    public IEnumerable<GroupNode> Groups => FindChildren<GroupNode>();
}
