using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EESimulator.Core.Models;

public class NodeModifiedEventArgs : EventArgs
{
    public string PropertyName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public NodeModifiedEventArgs(string propertyName, object? oldValue = null, object? newValue = null)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

public class NodeHierarchyChangedEventArgs : EventArgs
{
    public RootNode Child { get; }
    public RootNode? OldParent { get; }
    public RootNode? NewParent { get; }

    public NodeHierarchyChangedEventArgs(RootNode child, RootNode? oldParent, RootNode? newParent)
    {
        Child = child;
        OldParent = oldParent;
        NewParent = newParent;
    }
}

/// <summary>
/// Universal base node for all objects in EESimulator, inspired by Blender's scene hierarchy.
/// Supports identity, strict acyclic hierarchy, editor state, metadata, and reactive events.
/// </summary>
public abstract class RootNode
{
    // Identity
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Object";
    public virtual string NodeType => GetType().Name;
    public HashSet<string> Tags { get; } = new();

    // Hierarchy
    [JsonIgnore]
    public RootNode? Parent { get; private set; }
    public List<RootNode> Children { get; } = new();

    // Editor & Lifecycle State
    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; } = false;
    public bool Selected { get; set; } = false;

    // Metadata
    public Dictionary<string, object> CustomProperties { get; set; } = new();
    public object? UserData { get; set; }

    // Events
    public event EventHandler<NodeModifiedEventArgs>? OnModified;
    public event EventHandler<NodeHierarchyChangedEventArgs>? OnChildAdded;
    public event EventHandler<NodeHierarchyChangedEventArgs>? OnChildRemoved;
    public event EventHandler? OnDeleted;

    public void NotifyModified(string propertyName, object? oldValue = null, object? newValue = null)
    {
        OnModified?.Invoke(this, new NodeModifiedEventArgs(propertyName, oldValue, newValue));
    }

    public virtual void AddChild(RootNode child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        if (child == this)
            throw new InvalidOperationException("Cannot add a node as a child of itself.");

        // Prevent circular parenting
        if (IsDescendantOf(child))
            throw new InvalidOperationException($"Cannot add node '{child.Name}' as a child because it is an ancestor of '{Name}'.");

        if (child.Parent != null)
        {
            if (child.Parent == this)
                return; // Already added
            child.Parent.RemoveChild(child);
        }

        child.Parent = this;
        Children.Add(child);
        OnChildAdded?.Invoke(this, new NodeHierarchyChangedEventArgs(child, null, this));
        NotifyModified(nameof(Children));
    }

    public virtual bool RemoveChild(RootNode child)
    {
        if (child == null)
            return false;

        if (Children.Remove(child))
        {
            var oldParent = child.Parent;
            child.Parent = null;
            OnChildRemoved?.Invoke(this, new NodeHierarchyChangedEventArgs(child, oldParent, null));
            NotifyModified(nameof(Children));
            return true;
        }
        return false;
    }

    public virtual void Reparent(RootNode newParent)
    {
        if (newParent == null)
            throw new ArgumentNullException(nameof(newParent));

        if (Parent == newParent)
            return;

        Parent?.RemoveChild(this);
        newParent.AddChild(this);
    }

    public bool IsDescendantOf(RootNode potentialAncestor)
    {
        var current = Parent;
        while (current != null)
        {
            if (current == potentialAncestor)
                return true;
            current = current.Parent;
        }
        return false;
    }

    public virtual void Delete()
    {
        Parent?.RemoveChild(this);
        OnDeleted?.Invoke(this, EventArgs.Empty);
    }

    public virtual T? FindChild<T>(string name) where T : RootNode
    {
        foreach (var child in Children)
        {
            if (child is T typed && child.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return typed;

            var deepFound = child.FindChild<T>(name);
            if (deepFound != null)
                return deepFound;
        }
        return null;
    }

    public virtual T? FindChildById<T>(Guid id) where T : RootNode
    {
        foreach (var child in Children)
        {
            if (child.Id == id && child is T typed)
                return typed;

            var deepFound = child.FindChildById<T>(id);
            if (deepFound != null)
                return deepFound;
        }
        return null;
    }

    public virtual IEnumerable<T> FindChildren<T>() where T : RootNode
    {
        foreach (var child in Children)
        {
            if (child is T typed)
                yield return typed;

            foreach (var deep in child.FindChildren<T>())
                yield return deep;
        }
    }
}
