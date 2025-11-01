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
        [Test]
        public async Task HandleClientAsync_EchoesMessage()
        {
            var loggerMock = new Mock<EchoServer.ILogger>();
            var serverMessage = "Hello";
            byte[] data = Encoding.UTF8.GetBytes(serverMessage);

            using var client = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                await EchoServer.EchoServer.HandleClientAsync(tcpClient, CancellationToken.None, loggerMock.Object);
            });

            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length);

            byte[] buffer = new byte[data.Length];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);

            Encoding.UTF8.GetString(buffer, 0, read).Should().Be(serverMessage);

            listener.Stop();
        }
    }
}
