#if ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Python3Android;

namespace EESimulator.Scripting.Execution;

public sealed class ManagedPythonScriptEngine
{
    private readonly ManagedPythonScriptBridge _bridge;
    private readonly SemaphoreSlim _executionGate = new(1, 1);
    private Task<AndroidPythonEnvironment>? _environmentTask;

    public ManagedPythonScriptEngine(PythonCommandExecutor executor)
        : this(new ManagedPythonScriptBridge(executor))
    {
    }

    public ManagedPythonScriptEngine(ManagedPythonScriptBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public async Task ExecuteAsync(string script, string scriptName, IEnumerable<string>? additionalSearchPaths = null,
        Action<string>? onOutput = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (string.IsNullOrWhiteSpace(scriptName))
            throw new ArgumentException("Script name is required.", nameof(scriptName));

        await _executionGate.WaitAsync();
        try
        {
            AndroidPythonEnvironment environment = await (_environmentTask ??= AndroidPythonEnvironment.Create());
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            string bridgeToken = Guid.NewGuid().ToString("N");
            using var cancellation = new CancellationTokenSource();
            Task bridgeTask = ServeBridgeRequestsAsync(listener, bridgeToken, cancellation.Token);

            try
            {
                string pythonSource = BuildPythonSource(
                    script, scriptName, ((IPEndPoint)listener.LocalEndpoint).Port, bridgeToken, additionalSearchPaths);
                string output = await Task.Run(() =>
                {
                    string result = environment.RunCode(pythonSource, out string error, out int errorCode);
                    if (errorCode != 0)
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                            ? $"Python execution failed with code {errorCode}."
                            : error);
                    return string.IsNullOrWhiteSpace(error) ? result : $"{result}{Environment.NewLine}{error}";
                });
                cancellation.Cancel();
                listener.Stop();
                await bridgeTask;
                if (!string.IsNullOrEmpty(output))
                    onOutput?.Invoke(output);
            }
            finally
            {
                cancellation.Cancel();
                listener.Stop();
                try { await bridgeTask; }
                catch (OperationCanceledException) { }
                catch (SocketException) when (cancellation.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
            }
        }
        finally
        {
            _executionGate.Release();
        }
    }

    public void Execute(string script, string scriptName, IEnumerable<string>? additionalSearchPaths = null,
        Action<string>? onOutput = null) =>
        ExecuteAsync(script, scriptName, additionalSearchPaths, onOutput).GetAwaiter().GetResult();

    public async Task ExecuteFileAsync(string scriptPath, Action<string>? onOutput = null)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("Script path is required.", nameof(scriptPath));
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Python script not found.", scriptPath);

        await ExecuteAsync(File.ReadAllText(scriptPath), scriptPath,
            new[] { Path.GetDirectoryName(scriptPath) ?? string.Empty }, onOutput);
    }

    public void ExecuteFile(string scriptPath) =>
        ExecuteFileAsync(scriptPath).GetAwaiter().GetResult();

    private static string BuildPythonSource(
        string script,
        string scriptName,
        int bridgePort,
        string bridgeToken,
        IEnumerable<string>? additionalSearchPaths)
    {
        var source = new StringBuilder();
        source.AppendLine("import sys");
        foreach (string path in additionalSearchPaths ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(path))
                source.Append("sys.path.insert(0, ").Append(JsonSerializer.Serialize(path)).AppendLine(")");
        }
        source.AppendLine("import eesim");
        source.Append("eesim.attach_socket(").Append(bridgePort).Append(", ")
            .Append(JsonSerializer.Serialize(bridgeToken)).AppendLine(")");
        source.AppendLine("import circuit");
        source.AppendLine("true = True; false = False; null = None; none = None");
        source.AppendLine("try:");
        source.Append("    exec(compile(").Append(JsonSerializer.Serialize(script)).Append(", ")
            .Append(JsonSerializer.Serialize(scriptName)).AppendLine(", 'exec'), globals(), globals())");
        source.AppendLine("finally:");
        source.AppendLine("    eesim.detach_socket()");
        return source.ToString();
    }

    private async Task ServeBridgeRequestsAsync(TcpListener listener, string bridgeToken, CancellationToken cancellationToken)
    {
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) return;

                string requestScript;
                using (JsonDocument request = JsonDocument.Parse(line))
                {
                    string? requestToken = request.RootElement.GetProperty("token").GetString();
                    if (!string.Equals(requestToken, bridgeToken, StringComparison.Ordinal))
                        throw new InvalidDataException("Invalid Python bridge token.");
                    requestScript = request.RootElement.GetProperty("script").GetString() ?? string.Empty;
                }

                Dictionary<string, object?> response;
                try
                {
                    response = await MainThread.InvokeOnMainThreadAsync(() => _bridge.Execute(requestScript))
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    response = new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["message"] = exception.Message,
                        ["result"] = null
                    };
                }

                await writer.WriteLineAsync(JsonSerializer.Serialize(response)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
    }
}
#endif