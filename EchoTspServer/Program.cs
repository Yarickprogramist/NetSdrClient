using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    // Інтерфейс для логування (можна замінити на ILogger в реальному проєкті)
    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine(message);
    }

    public class EchoServer : IDisposable
    {
        private readonly int _port;
        private readonly ILogger _logger;
        private TcpListener? _listener;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public EchoServer(int port, ILogger? logger = null)
        {
            _port = port;
            _logger = logger ?? new ConsoleLogger();
            _cts = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _logger.Log($"Server started on port {_port}.");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _logger.Log("Client connected.");
                    _ = Task.Run(() => HandleClientAsync(client, _cts.Token));
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
            }

            _logger.Log("Server shutdown.");
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener?.Stop();
            _logger.Log("Server stopped.");
        }

        public static async Task HandleClientAsync(TcpClient client, CancellationToken token, ILogger? logger = null)
        {
            logger ??= new ConsoleLogger();

            using var stream = client.GetStream();
            byte[] buffer = new byte[8192];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break; // client disconnected

                    await stream.WriteAsync(buffer, 0, bytesRead, token);
                    logger.Log($"Echoed {bytesRead} bytes to the client.");
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                logger.Log($"Error: {ex.Message}");
            }
            finally
            {
                client.Close();
                logger.Log("Client disconnected.");
            }
        }

        // Dispose pattern to clean up CancellationTokenSource and listener
        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();
            _cts.Dispose();
            _listener?.Stop();
            _disposed = true;
        }
    }
}
