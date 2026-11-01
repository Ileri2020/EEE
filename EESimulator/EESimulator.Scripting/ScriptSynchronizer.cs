using System;
using System.Text;
using System.Threading.Tasks;
using EESimulator.Commands;
using EESimulator.Core.Models;
using EESimulator.Scripting.Execution;

namespace EESimulator.Scripting;

/// <summary>
/// Handles two-way synchronization between the schematic viewport and the python script viewport
/// without recursive feedback loops or destructive overwrites.
/// <para>
/// The auto-generated section is always enclosed in a guarded block at the end of the script:
/// <code>
/// # --- [auto-generated: do not edit below] ---
/// ...generated statements...
/// # --- [/auto-generated] ---
/// </code>
/// User code written above this guard is preserved when new commands are appended.
/// </para>
/// </summary>
public class ScriptSynchronizer
{
    private const string AutoGenBegin = "# --- [auto-generated: do not edit below] ---";
    private const string AutoGenEnd   = "# --- [/auto-generated] ---";

    private readonly CommandManager _commandManager;
    private CircuitNode _circuit;
    private bool _isSynchronizing;

    public event Action<string>? ScriptUpdated;

    private readonly StringBuilder _userScript  = new(); // Lines the user wrote
    private readonly StringBuilder _autoScript  = new(); // Auto-generated block

    public string CurrentScript
    {
        get
        {
            var sb = new StringBuilder();
            var user = _userScript.ToString();
            if (!string.IsNullOrWhiteSpace(user))
            {
                sb.Append(user);
                if (!user.EndsWith('\n'))
                    sb.AppendLine();
                sb.AppendLine();
            }

            var auto = _autoScript.ToString();
            if (!string.IsNullOrWhiteSpace(auto))
            {
                sb.AppendLine(AutoGenBegin);
                sb.Append(auto);
                sb.AppendLine(AutoGenEnd);
            }

            return sb.ToString();
        }
    }

    public ScriptSynchronizer(CommandManager commandManager, CircuitNode circuit)
    {
        _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        _circuit        = circuit        ?? throw new ArgumentNullException(nameof(circuit));

        _commandManager.CommandExecuted += OnCommandExecuted;
        _commandManager.CommandUndone   += OnCommandUndone;
        _commandManager.CommandRedone   += OnCommandExecuted;

        // Initialize with header in user section
        _userScript.AppendLine("# EESimulator Script Session");
        _userScript.AppendLine("circuit = app.get_current_circuit()");
        _userScript.AppendLine();
    }

    /// <summary>
    /// Called by the workspace ViewModel after any project load or new-circuit operation.
    /// Replaces the tracked circuit reference and regenerates the full auto-generated block (§5.A).
    /// </summary>
    public void OnCircuitReplaced(CircuitNode newCircuit)
    {
        if (newCircuit == null) return;
        _circuit = newCircuit;

        if (_isSynchronizing) return;

        try
        {
            _isSynchronizing = true;

            // Regenerate the auto block from the full loaded circuit
            string fullScript = PythonCodeGenerator.GenerateFullScript(_circuit);
            _autoScript.Clear();
            _autoScript.Append(fullScript);

            ScriptUpdated?.Invoke(CurrentScript);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void OnCommandExecuted(object? sender, CommandExecutedEventArgs e)
    {
        if (_isSynchronizing) return;

        // Skip commands that originated in the Python executor (avoid echo loop)
        if (e.Command.Origin == CommandOrigin.Python)
            return;

        try
        {
            _isSynchronizing = true;
            string statement = e.Command.ToPythonScript();
            if (!string.IsNullOrWhiteSpace(statement))
            {
                _autoScript.AppendLine(statement);
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
            // Regenerate auto block to reflect the circuit after undo
            string fullScript = PythonCodeGenerator.GenerateFullScript(_circuit);
            _autoScript.Clear();
            _autoScript.Append(fullScript);
            ScriptUpdated?.Invoke(CurrentScript);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    /// <summary>
    /// Called when the user manually edits the script editor.
    /// Splits user-authored lines from the auto-generated guard block to preserve user code,
    /// then executes the combined script asynchronously to avoid blocking the UI thread (§5.E).
    /// </summary>
    public async Task OnUserEditedScriptAsync(string newScript, PythonCommandExecutor executor)
    {
        if (_isSynchronizing) return;

        try
        {
            _isSynchronizing = true;

            // Separate user code from the auto-generated guard block
            int guardStart = newScript.IndexOf(AutoGenBegin, StringComparison.Ordinal);
            if (guardStart >= 0)
            {
                _userScript.Clear();
                _userScript.Append(newScript[..guardStart].TrimEnd());
            }
            else
            {
                // No guard block found — treat entire content as user code
                _userScript.Clear();
                _userScript.Append(newScript);
            }

            // Execute the full script off the UI thread (§5.E)
            await Task.Run(() =>
            {
                executor.ExecuteScript(newScript, out _);
            });
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    /// <summary>
    /// Synchronous overload kept for backward compatibility; prefer <see cref="OnUserEditedScriptAsync"/>.
    /// </summary>
    public void OnUserEditedScript(string newScript, PythonCommandExecutor executor)
    {
        if (_isSynchronizing) return;

        try
        {
            _isSynchronizing = true;

            int guardStart = newScript.IndexOf(AutoGenBegin, StringComparison.Ordinal);
            if (guardStart >= 0)
            {
                _userScript.Clear();
                _userScript.Append(newScript[..guardStart].TrimEnd());
            }
            else
            {
                _userScript.Clear();
                _userScript.Append(newScript);
            }

            executor.ExecuteScript(newScript, out _);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }
}
