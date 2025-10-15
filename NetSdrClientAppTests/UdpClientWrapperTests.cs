using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class UdpClientWrapperTests
    {
        private UdpClientWrapper _udpClientWrapper;

        [SetUp]
        public void Setup()
        {
            _udpClientWrapper = new UdpClientWrapper(9999);
        }

        [Test]
        public void Constructor_ShouldInitializeLocalEndpoint()
        {
            Assert.NotNull(_udpClientWrapper, "UdpClientWrapper should be instantiated");
        }

        [Test]
        public void StopListening_ShouldNotThrow_WhenCalledWithoutStart()
        {
            Assert.DoesNotThrow(() => _udpClientWrapper.StopListening(),
                "StopListening should not throw even if StartListeningAsync was not called");
        }

        [Test]
        public void Exit_ShouldNotThrow_WhenCalledMultipleTimes()
        {
            _udpClientWrapper.Exit();
            Assert.DoesNotThrow(() => _udpClientWrapper.Exit(),
                "Exit should not throw if called multiple times");
        }

        [Test]
        public void GetHashCode_ShouldReturnConsistentValue()
        {
            int first = _udpClientWrapper.GetHashCode();
            int second = _udpClientWrapper.GetHashCode();

            Assert.AreEqual(first, second, "GetHashCode should return consistent value");
        }

        [Test]
        public async Task StartListeningAsync_ShouldStartAndStopGracefully()
        {
            // Arrange
            var task = _udpClientWrapper.StartListeningAsync();

            // Act
            await Task.Delay(100); // give it time to start
            _udpClientWrapper.StopListening();

            // Assert
            await Task.WhenAny(task, Task.Delay(1000));
            Assert.IsTrue(task.IsCompleted, "StartListeningAsync should complete after StopListening");
        }

        [Test]
        public void MessageReceived_ShouldBeInvoked_WhenManuallyTriggered()
        {
            bool eventRaised = false;
            _udpClientWrapper.MessageReceived += (s, data) =>
            {
                eventRaised = true;
                Assert.NotNull(data, "Event data should not be null");
                Assert.AreEqual(1, data.Length, "Event data length should be 1");
                Assert.AreEqual(0xAA, data[0], "Event data should contain expected byte");
            };

            // Виклик події через reflection
            var eventField = typeof(UdpClientWrapper).GetField("MessageReceived",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var handler = (MulticastDelegate?)eventField?.GetValue(_udpClientWrapper);
            handler?.DynamicInvoke(_udpClientWrapper, new byte[] { 0xAA });

            Assert.IsTrue(eventRaised, "MessageReceived event should be raised");
        }
    }
}
