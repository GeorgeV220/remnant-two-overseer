using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RemnantOverseer.Utilities;

public class SingleInstanceHelper : IDisposable
{
    private const string PipeName = "remnant_overseer_ipc";
    private readonly string _ipcPath;

    private Task? _listenTask;
    private bool _isDisposed;
    private Window? _mainWindow;

    public bool IsPrimaryInstance { get; }

    public SingleInstanceHelper()
    {
        _ipcPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"\\\\.\\pipe\\{PipeName}"
            : Path.Combine(Path.GetTempPath(), PipeName);

        IsPrimaryInstance = TryStartListener();
    }

    private bool TryStartListener()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                _listenTask = Task.Run(() => ListenNamedPipeAsync(pipeServer));
            }
            else
            {
                if (File.Exists(_ipcPath))
                {
                    try
                    {
                        using var testSocket =
                            new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                        testSocket.Connect(new UnixDomainSocketEndPoint(_ipcPath));
                        return false; // Another instance is running
                    }
                    catch
                    {
                        File.Delete(_ipcPath); // Stale socket
                    }
                }

                var serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                serverSocket.Bind(new UnixDomainSocketEndPoint(_ipcPath));
                serverSocket.Listen(1);

                _listenTask = Task.Run(() => ListenUnixSocketAsync(serverSocket));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void NotifyExistingInstance()
    {
        var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"\\\\.\\pipe\\{PipeName}"
            : Path.Combine(Path.GetTempPath(), PipeName);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(200);
                using var writer = new StreamWriter(client);
                writer.AutoFlush = true;
                writer.WriteLine("Activate");
            }
            else
            {
                using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                client.Connect(new UnixDomainSocketEndPoint(path));
                using var stream = new NetworkStream(client);
                using var writer = new StreamWriter(stream);
                writer.AutoFlush = true;
                writer.WriteLine("Activate");
            }
        }
        catch
        {
            // Activation attempt failed (probably no active instance)
        }
    }

    private async Task ListenNamedPipeAsync(NamedPipeServerStream pipe)
    {
        while (!_isDisposed)
        {
            try
            {
                await pipe.WaitForConnectionAsync();
                using var reader = new StreamReader(pipe);
                var message = await reader.ReadLineAsync();
                HandleMessage(message);
                pipe.Disconnect();
            }
            catch
            {
                break;
            }
        }
    }

    private async Task ListenUnixSocketAsync(Socket serverSocket)
    {
        while (!_isDisposed)
        {
            try
            {
                using var client = await serverSocket.AcceptAsync();
                await using var stream = new NetworkStream(client);
                using var reader = new StreamReader(stream);
                var message = await reader.ReadLineAsync();
                HandleMessage(message);
            }
            catch
            {
                break;
            }
        }

        serverSocket.Close();
    }

    private void HandleMessage(string? message)
    {
        if (message != "Activate" || _mainWindow is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
        });
    }

    public void RegisterMainWindow(Window mainWindow) => _mainWindow = mainWindow;

    public void Dispose()
    {
        _isDisposed = true;
        _listenTask?.Dispose();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(_ipcPath)) return;
        try
        {
            File.Delete(_ipcPath);
        }
        catch
        {
            // ignored
        }
    }
}