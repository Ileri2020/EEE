using System;
using System.Text;
using EESimulator.Commands;
using EESimulator.Core.Models;
using EESimulator.Scripting.Execution;

namespace EESimulator.Scripting;

/// <summary>
/// Handles two-way synchronization between the schematic viewport and the python script viewport
/// without recursive feedback loops or destructive overwrites.
/// </summary>
public class ScriptSynchronizer
{
    private readonly CommandManager _commandManager;
    private readonly CircuitNode _circuit;
    private bool _isSynchronizing;

    public event Action<string>? ScriptUpdated;

    private readonly StringBuilder _activeScript = new();

    public string CurrentScript => _activeScript.ToString();

    public ScriptSynchronizer(CommandManager commandManager, CircuitNode circuit)
    {
        _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        _circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));

        _commandManager.CommandExecuted += OnCommandExecuted;
        _commandManager.CommandUndone += OnCommandUndone;
        _commandManager.CommandRedone += OnCommandExecuted;

        // Initialize header
        _activeScript.AppendLine("# EESimulator Script Session");
        _activeScript.AppendLine($"circuit = app.get_current_circuit()");
        _activeScript.AppendLine();
    }

    private void OnCommandExecuted(object? sender, CommandExecutedEventArgs e)
    {
        if (_isSynchronizing) return;

        // If the command came from Python, don't generate duplicate code in the script editor
        if (e.Command.Origin == CommandOrigin.Python)
            return;

        try
        {
            _isSynchronizing = true;
            string statement = e.Command.ToPythonScript();
            if (!string.IsNullOrWhiteSpace(statement))
            {
                _activeScript.AppendLine(statement);
                ScriptUpdated?.Invoke(CurrentScript);
            }
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void OnCommandUndone(object? sender, CommandExecutedEventArgs e)
    {
        if (_isSynchronizing) return;

        try
        {
            _isSynchronizing = true;
            // Regenerate the script to reflect current state after undo
            string fullScript = PythonCodeGenerator.GenerateFullScript(_circuit);
            _activeScript.Clear();
            _activeScript.Append(fullScript);
            ScriptUpdated?.Invoke(CurrentScript);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    public void OnUserEditedScript(string newScript, PythonCommandExecutor executor)
    {
        if (_isSynchronizing) return;

        try
        {
            _isSynchronizing = true;
            _activeScript.Clear();
            _activeScript.Append(newScript);
            executor.ExecuteScript(newScript, out _);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }
}
