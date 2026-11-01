using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Models;

namespace EESimulator.Core.UI;

/// <summary>
/// Root UI node that groups the entire interface hierarchy under RootNode.
/// Provides convenient query methods for C# ViewModels and Python scripts.
/// </summary>
public class UIRootNode : RootNode
{
    public WorkspaceNode Workspace { get; }

    public UIRootNode()
    {
        Name = "UI_Root";
        Workspace = new WorkspaceNode();
        AddChild(Workspace);
    }

    public UINode? Get(string name)
    {
        return FindChild<UINode>(name);
    }

    public T? Get<T>(string name) where T : UINode
    {
        return FindChild<T>(name);
    }

    public ButtonNode? Button(string name) => Get<ButtonNode>(name);
    public DialogNode? Dialog(string name) => Get<DialogNode>(name);
    public InputNode? Input(string name) => Get<InputNode>(name);
    public DropdownNode? Dropdown(string name) => Get<DropdownNode>(name);
    public SidebarNode? Sidebar(string name) => Get<SidebarNode>(name);
    public TabNode? Tabs(string name) => Get<TabNode>(name);
    public SelectNode? Select(string name) => Get<SelectNode>(name);

    public IEnumerable<UINode> GetAllUIComponents()
    {
        return FindChildren<UINode>();
    }
}
