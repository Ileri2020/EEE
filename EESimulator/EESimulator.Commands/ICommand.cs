using System;

namespace EESimulator.Commands;

public enum CommandOrigin
{
    Schematic,
    Python,
    System
}

/// <summary>
/// Universal command interface enabling bidirectional execution, undo/redo, and Python translation.
/// </summary>
public interface ICommand
{
    Guid Id { get; }
    string Description { get; }
    CommandOrigin Origin { get; set; }

    bool Execute();
    bool Undo();

    /// <summary>
    /// Translates this command to its equivalent Python statement for the Python viewport.
    /// </summary>
    string ToPythonScript();
}
