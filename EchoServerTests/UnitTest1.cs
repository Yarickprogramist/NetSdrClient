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
        private EchoServer.ILogger _logger;
        private Mock<EchoServer.ILogger> _loggerMock;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<EchoServer.ILogger>();
            _logger = _loggerMock.Object;
        }

        [Test]
        public async Task HandleClientAsync_EchoesMessage()
        {
            string serverMessage = "Hello";
            byte[] data = Encoding.UTF8.GetBytes(serverMessage);

            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            // Accept client and run server echo handler
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

            // Verify logger called at least once for echo
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Echoed"))), Times.AtLeastOnce);

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

            // Close client immediately
            client.Close();

            // Allow server task to process disconnect
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

            cts.Cancel(); // cancel token to stop echoing

            // Allow server task to handle cancellation
            await Task.Delay(200);

            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Client disconnected."))), Times.AtLeastOnce);

            listener.Stop();
        }
    }
}
