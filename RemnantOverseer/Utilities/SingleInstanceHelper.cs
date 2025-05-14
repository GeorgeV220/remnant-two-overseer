using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RemnantOverseer.Utilities;

public class SingleInstanceHelper : IDisposable
{
    private readonly int _port;
    private TcpListener? _listener;
    private Task? _listenTask;

    private Window? _mainWindow;

    public bool IsPrimaryInstance { get; private set; }

    public SingleInstanceHelper(int port)
    {
        _port = port;
        TryStartListener();
    }

    private void TryStartListener()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            IsPrimaryInstance = true;

            _listenTask = Task.Run(async () => await ListenForMessagesAsync());
        }
        catch (SocketException)
        {
            IsPrimaryInstance = false;
        }
    }

    public static void NotifyExistingInstance(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            using var writer = new StreamWriter(client.GetStream());
            writer.WriteLine("Activate");
            writer.Flush();
        }
        catch
        {
            // Connection failed – maybe app closed already
        }
    }

    private async Task ListenForMessagesAsync()
    {
        while (true)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync();
                using var reader = new StreamReader(client.GetStream());
                var message = await reader.ReadLineAsync();

                if (message == "Activate" && _mainWindow is not null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _mainWindow.WindowState = WindowState.Normal;
                        _mainWindow.Activate();
                        _mainWindow.Topmost = true;
                        _mainWindow.Topmost = false;
                    });
                }
            }
            catch
            {
                break;
            }
        }
    }

    public void RegisterMainWindow(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Dispose()
    {
        _listener?.Stop();
        _listenTask?.Dispose();
    }
}