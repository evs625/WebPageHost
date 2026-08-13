// Copyright (c) Thomas Gossler. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebPageHost;

/// <summary>
/// Routes URLs from later processes to the already running window for the same environment.
/// </summary>
internal sealed class ReuseWindowBroker : IDisposable
{
    private readonly Mutex instanceMutex;
    private readonly string pipeName;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private Task? listenerTask;

    private ReuseWindowBroker(Mutex instanceMutex, string pipeName, bool isPrimary)
    {
        this.instanceMutex = instanceMutex;
        this.pipeName = pipeName;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static ReuseWindowBroker Create(string environmentName)
    {
        string keySource = string.IsNullOrWhiteSpace(environmentName) ? "default" : environmentName;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(keySource));
        string key = Convert.ToHexString(hash.AsSpan(0, 8));
        string mutexName = $@"Local\{Common.ProgramName}.ReuseWindow.{key}";
        string pipeName = $"{Common.ProgramName}.ReuseWindow.{key}";

        var mutex = new Mutex(false, mutexName, out bool createdNew);
        return new ReuseWindowBroker(mutex, pipeName, createdNew);
    }

    public bool ForwardUrl(string url, int timeoutMilliseconds = 5000)
    {
        try {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            client.Connect(timeoutMilliseconds);
            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine(url);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            Trace.TraceError($"Could not forward URL to the existing WebPageHost window: {ex.Message}");
            return false;
        }
    }

    public void Start(MainForm form)
    {
        if (!IsPrimary || listenerTask != null) {
            return;
        }

        listenerTask = ListenAsync(form, cancellationTokenSource.Token);
    }

    private async Task ListenAsync(MainForm form, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true);
                string? url = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(url) || form.IsDisposed || !form.IsHandleCreated) {
                    continue;
                }

                _ = form.BeginInvoke(new Action(() => {
                    if (form.IsDisposed) {
                        return;
                    }

                    form.Url = url;
                    if (form.WindowState == FormWindowState.Minimized) {
                        form.WindowState = FormWindowState.Normal;
                    }
                    form.Show();
                    form.BringToFront();
                    form.Activate();
                }));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) {
                Trace.TraceWarning($"Reuse-window listener error: {ex.Message}");
                if (!cancellationToken.IsCancellationRequested) {
                    try {
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (OperationCanceledException) {
                        break;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        instanceMutex.Dispose();
    }
}
