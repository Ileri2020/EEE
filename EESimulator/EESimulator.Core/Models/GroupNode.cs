using System;
using System.Collections.Generic;

namespace EESimulator.Core.Models;

/// <summary>
/// Organizational node for grouping components, functional blocks, or subcircuits.
/// </summary>
public class GroupNode : RootNode
{
    public string GroupDescription { get; set; } = string.Empty;

    public GroupNode()
    {
        Name = "Group";
    }

    public GroupNode(string name)
    {
        Name = name;
    }
}
