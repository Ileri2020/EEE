using System;
using System.Collections.Generic;

namespace EESimulator.Commands;

public class CommandExecutedEventArgs : EventArgs
{
    public ICommand Command { get; }
    public CommandExecutedEventArgs(ICommand command) => Command = command;
}

/// <summary>
/// Central Command Manager providing undo/redo stacks and broadcasting execution events
/// to both the schematic canvas and the python scripting synchronizer.
/// </summary>
public class CommandManager
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
    public event EventHandler<CommandExecutedEventArgs>? CommandUndone;
    public event EventHandler<CommandExecutedEventArgs>? CommandRedone;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public bool ExecuteCommand(ICommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        if (command.Execute())
        {
            _undoStack.Push(command);
            _redoStack.Clear();
            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command));
            return true;
        }
        return false;
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        var cmd = _undoStack.Pop();
        if (cmd.Undo())
        {
            _redoStack.Push(cmd);
            CommandUndone?.Invoke(this, new CommandExecutedEventArgs(cmd));
            return true;
        }
        return false;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        var cmd = _redoStack.Pop();
        if (cmd.Execute())
        {
            _undoStack.Push(cmd);
            CommandRedone?.Invoke(this, new CommandExecutedEventArgs(cmd));
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
