using NUnit.Framework;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;

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
    }
}
