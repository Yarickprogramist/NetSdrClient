using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;

namespace EchoServerTests
{
    [TestFixture]
    public class EchoServerTests
    {
        private Mock<EchoServer.ILogger> _loggerMock;
        private EchoServer.ILogger _logger;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<EchoServer.ILogger>();
            _logger = _loggerMock.Object;
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithPort_CreatesInstance()
        {
            var server = new EchoServer.EchoServer(5000, _logger);
            server.Should().NotBeNull();
        }

        [Test]
        public void Constructor_WithoutLogger_UsesConsoleLogger()
        {
            var server = new EchoServer.EchoServer(5000);
            server.Should().NotBeNull();
        }

        #endregion

        #region StartAsync Tests

        [Test]
        public async Task StartAsync_StartsServerAndLogsMessage()
        {
            var server = new EchoServer.EchoServer(0, _logger);
            var startTask = Task.Run(() => server.StartAsync());

            await Task.Delay(100); // Give server time to start

            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Server started on port"))), Times.Once);

            server.Stop();
            await startTask;
        }

        [Test]
        public async Task StartAsync_AcceptsClientConnection()
        {
            var server = new EchoServer.EchoServer(0, _logger);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            server = new EchoServer.EchoServer(port, _logger);
            var startTask = Task.Run(() => server.StartAsync());

            await Task.Delay(100);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);

            await Task.Delay(100);

            _loggerMock.Verify(l => l.Log("Client connected."), Times.AtLeastOnce);

            client.Close();
            server.Stop();
            await startTask;
        }

        [Test]
        public async Task StartAsync_StopsWhenCancelled()
        {
            var server = new EchoServer.EchoServer(0, _logger);
            var startTask = Task.Run(() => server.StartAsync());

            await Task.Delay(100);

            server.Stop();
            await startTask;

            _loggerMock.Verify(l => l.Log("Server shutdown."), Times.Once);
        }

        #endregion

        #region Stop Tests

        [Test]
        public async Task Stop_StopsServerAndLogs()
        {
            var server = new EchoServer.EchoServer(0, _logger);
            var startTask = Task.Run(() => server.StartAsync());

            await Task.Delay(100);

            server.Stop();
            await startTask;

            _loggerMock.Verify(l => l.Log("Server stopped."), Times.Once);
            _loggerMock.Verify(l => l.Log("Server shutdown."), Times.Once);
        }

        #endregion

        #region HandleClientAsync Tests

        [Test]
        public async Task HandleClientAsync_EchoesMessage()
        {
            string serverMessage = "Hello";
            byte[] data = Encoding.UTF8.GetBytes(serverMessage);

            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, CancellationToken.None, _logger);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await stream.WriteAsync(data, 0, data.Length);

            byte[] buffer = new byte[data.Length];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);

            Encoding.UTF8.GetString(buffer, 0, read).Should().Be(serverMessage);
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Echoed"))), Times.AtLeastOnce);

            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_EchoesMultipleMessages()
        {
            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, CancellationToken.None, _logger);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            for (int i = 0; i < 3; i++)
            {
                string message = $"Message{i}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(data, 0, data.Length);

                byte[] buffer = new byte[data.Length];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);

                Encoding.UTF8.GetString(buffer, 0, read).Should().Be(message);
            }

            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Echoed"))), Times.AtLeast(3));

            client.Close();
            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_ClientDisconnects_LogsProperly()
        {
            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, CancellationToken.None, _logger);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            client.Close();

            await Task.Delay(200);

            _loggerMock.Verify(l => l.Log("Client disconnected."), Times.AtLeastOnce);

            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_CancelsToken_StopsEchoing()
        {
            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var cts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, cts.Token, _logger);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            byte[] message = Encoding.UTF8.GetBytes("Test");
            await stream.WriteAsync(message, 0, message.Length);

            cts.Cancel();

            await Task.Delay(200);

            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Client disconnected."))), Times.AtLeastOnce);

            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_WithoutLogger_UsesConsoleLogger()
        {
            string serverMessage = "Hello";
            byte[] data = Encoding.UTF8.GetBytes(serverMessage);

            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, CancellationToken.None);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await stream.WriteAsync(data, 0, data.Length);

            byte[] buffer = new byte[data.Length];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);

            Encoding.UTF8.GetString(buffer, 0, read).Should().Be(serverMessage);

            client.Close();
            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_LargeBuffer_EchoesCorrectly()
        {
            byte[] largeData = new byte[8192];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, CancellationToken.None, _logger);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await stream.WriteAsync(largeData, 0, largeData.Length);

            byte[] buffer = new byte[largeData.Length];
            int totalRead = 0;
            while (totalRead < largeData.Length)
            {
                int read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            buffer.Should().BeEquivalentTo(largeData);
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Echoed 8192 bytes"))), Times.AtLeastOnce);

            client.Close();
            listener.Stop();
        }

        #endregion

        #region ConsoleLogger Tests

        [Test]
        public void ConsoleLogger_Log_DoesNotThrow()
        {
            var logger = new EchoServer.ConsoleLogger();
            var action = () => logger.Log("Test message");
            action.Should().NotThrow();
        }

        #endregion
    }
}