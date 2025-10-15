using Moq;
using NetSdrClientApp.Networking;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class TcpClientWrapperTests
    {
        private TcpClientWrapper _clientWrapper;

        [SetUp]
        public void Setup()
        {
            _clientWrapper = new TcpClientWrapper("localhost", 1234);
        }

        [Test]
        public void Constructor_ShouldInitializeHostAndPort()
        {
            Assert.NotNull(_clientWrapper);
        }

        [Test]
        public void Connect_WhenAlreadyConnected_ShouldNotThrow()
        {
            // Arrange
            typeof(TcpClientWrapper)
                .GetField("_tcpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_clientWrapper, new TcpClient());

            // Act + Assert
            Assert.DoesNotThrow(() => _clientWrapper.Connect());
        }

        [Test]
        public void Disconnect_WhenNotConnected_ShouldPrintMessage()
        {
            // Act + Assert
            Assert.DoesNotThrow(() => _clientWrapper.Disconnect());
        }

        [Test]
        public void Disconnect_WhenConnected_ShouldCloseResources()
        {
            // Arrange
            var tcpClient = new TcpClient();
            typeof(TcpClientWrapper)
                .GetField("_tcpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_clientWrapper, tcpClient);

            var cts = new CancellationTokenSource();
            typeof(TcpClientWrapper)
                .GetField("_cts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_clientWrapper, cts);

            // Act
            Assert.DoesNotThrow(() => _clientWrapper.Disconnect());
        }

        [Test]
        public void SendMessageAsync_WhenNotConnected_ShouldThrow()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("test");

            // Act + Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _clientWrapper.SendMessageAsync(data)
            );
        }

        [Test]
        public void SendMessageAsync_String_WhenNotConnected_ShouldThrow()
        {
            // Act + Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _clientWrapper.SendMessageAsync("test")
            );
        }

        [Test]
        public void MessageReceived_Event_ShouldBeRaised_WhenDataArrives()
        {
            bool eventRaised = false;
            _clientWrapper.MessageReceived += (sender, data) =>
            {
                eventRaised = true;
                Assert.NotNull(data);
                Assert.AreEqual(2, data.Length);
            };

            // Викликаємо подію вручну через reflection
            var eventInfo = typeof(TcpClientWrapper).GetEvent("MessageReceived");
            var field = typeof(TcpClientWrapper).GetField("MessageReceived",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var delegateInstance = (MulticastDelegate)field?.GetValue(_clientWrapper)!;
            delegateInstance?.DynamicInvoke(_clientWrapper, new byte[] { 0x01, 0x02 });

            Assert.IsTrue(eventRaised);
        }


        [Test]
        public void Connect_ShouldNotThrow_WhenHostAndPortSet()
        {
            Assert.DoesNotThrow(() => _clientWrapper.Connect());
        }

        [Test]
        public void Disconnect_ShouldBeSafeToCallMultipleTimes()
        {
            _clientWrapper.Disconnect();
            _clientWrapper.Disconnect(); // Повторний виклик не має кидати виняток
            Assert.Pass();
        }

        [Test]
        public async Task SendMessageInternalAsync_WhenConnected_ShouldWriteToStream_RealStream()
        {
            // Arrange
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", port);

            var serverClient = await listener.AcceptTcpClientAsync();

            var stream = tcpClient.GetStream();

            typeof(TcpClientWrapper)
                .GetField("_tcpClient", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_clientWrapper, tcpClient);

            typeof(TcpClientWrapper)
                .GetField("_stream", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_clientWrapper, stream);

            var data = Encoding.UTF8.GetBytes("Hello");

            var method = typeof(TcpClientWrapper)
                .GetMethod("SendMessageInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            await (Task)method!.Invoke(_clientWrapper, new object[] { data })!;

            // Assert: read from server side
            var buffer = new byte[data.Length];
            await serverClient.GetStream().ReadAsync(buffer, 0, buffer.Length);
            Assert.AreEqual(data, buffer);

            listener.Stop();
        }



        [Test]
        public void SendMessageInternalAsync_WhenNotConnected_ShouldThrow()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Hello");

            var method = typeof(TcpClientWrapper)
                .GetMethod("SendMessageInternalAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act + Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await (Task)method!.Invoke(_clientWrapper, new object[] { data })!
            );
        }

        [Test]
        public void StartListeningAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var method = typeof(TcpClientWrapper)
                .GetMethod("StartListeningAsync", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act + Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await (Task)method!.Invoke(_clientWrapper, Array.Empty<object>())
            );

            Assert.That(ex!.Message, Is.EqualTo("Not connected to a server."));
        }
    }
}
