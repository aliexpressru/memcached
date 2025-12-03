using System.Net;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses;

/// <summary>
/// Tests for async operations with timeouts to prevent operations hanging indefinitely.
/// without timing out properly.
/// </summary>
[TestClass]
public class AsyncOperationsWithTimeoutTests
{
    private ILogger<PooledSocket> _loggerMock;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = Substitute.For<ILogger<PooledSocket>>();
    }

    [TestMethod]
    [TestCategory("Timeout")]
    public async Task ReadAsync_WhenServerNotResponding_ShouldThrowTimeoutException()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(100);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        // Create a server that accepts connections but never responds
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        try
        {
            // Act - connect successfully
            await socket.ConnectAsync(CancellationToken.None);

            // Try to read data - server won't respond
            var buffer = new byte[100];
            var readTask = socket.ReadAsync(buffer.AsMemory(), 100, CancellationToken.None);

            // Assert - should timeout
            var exception = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await readTask);

            exception.Message.Should().Contain("timed out");
            exception.Message.Should().Contain(receiveTimeout.TotalMilliseconds.ToString());
            
            // Verify that ShouldDestroySocket is set to true (marking socket for destruction)
            socket.ShouldDestroySocket.Should().BeTrue();
            
            // Verify that timeout was logged
            _loggerMock.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("timed out")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }
        finally
        {
            socket.Destroy();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("Timeout")]
    public async Task ReadAsync_WithCancellationToken_ShouldCancelOperation()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromSeconds(10); // Long timeout
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        var cts = new CancellationTokenSource();

        try
        {
            await socket.ConnectAsync(CancellationToken.None);

            // Act - cancel after 100ms
            cts.CancelAfter(100);
            
            var buffer = new byte[100];
            var readTask = socket.ReadAsync(buffer.AsMemory(), 100, cts.Token);

            // Assert - should be cancelled (TaskCanceledException inherits from OperationCanceledException)
            var exception = await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await readTask);
            
            exception.Should().BeAssignableTo<OperationCanceledException>();
        }
        finally
        {
            socket.Destroy();
            listener.Stop();
            cts.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("Timeout")]
    public async Task ReadAsync_WhenSocketDisconnected_ShouldThrowIOException()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromSeconds(1);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        try
        {
            await socket.ConnectAsync(CancellationToken.None);

            // Act - stop the server to simulate disconnection
            listener.Stop();
            
            // Wait a bit for the disconnection to propagate
            await Task.Delay(100);

            var buffer = new byte[100];
            
            // Assert - should throw IOException
            var exception = await Assert.ThrowsExceptionAsync<IOException>(
                async () => await socket.ReadAsync(buffer.AsMemory(), 100, CancellationToken.None));

            // The actual error message contains either "disconnected" or "Connection reset"
            exception.Message.Should().Match(m => 
                m.Contains("disconnected", StringComparison.OrdinalIgnoreCase) || 
                m.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("transport connection", StringComparison.OrdinalIgnoreCase));
            
            // Verify that ShouldDestroySocket is set to true (marking socket for destruction)
            socket.ShouldDestroySocket.Should().BeTrue();
        }
        finally
        {
            socket.Destroy();
        }
    }

    [TestMethod]
    [TestCategory("Timeout")]
    public async Task ResetAsync_WhenUnreadDataExists_ShouldClearDataWithTimeout()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(500);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        // This test verifies that ResetAsync properly uses ReadAsync with timeout
        // to clear unread data from the socket
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        // Accept connection in background and send some data
        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            
            // Send some data that won't be read immediately
            var data = new byte[] { 1, 2, 3, 4, 5 };
            await stream.WriteAsync(data);
            await stream.FlushAsync();
            
            // Keep connection open
            await Task.Delay(TimeSpan.FromSeconds(5));
            client.Close();
        });

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        try
        {
            await socket.ConnectAsync(CancellationToken.None);
            
            // Wait for data to arrive
            await Task.Delay(100);

            // Act - Reset should clear the unread data using ReadAsync with timeout
            await socket.ResetAsync(CancellationToken.None);

            // Assert - Socket should still be valid
            socket.ShouldDestroySocket.Should().BeFalse();
            
            // Verify warning was logged about unread data
            _loggerMock.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("unread data")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }
        finally
        {
            socket.Destroy();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task ReadAsync_MultipleOperations_ShouldNotExceedExpectedTime()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(100);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        var operationsCount = 10;
        
        // This test verifies that multiple operations don't accumulate time
        // beyond their individual timeouts (related to the 5.5s hang issue)
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        try
        {
            await socket.ConnectAsync(CancellationToken.None);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tasks = new List<Task>();

            // Act - try multiple reads that will all timeout
            for (int i = 0; i < operationsCount; i++)
            {
                var buffer = new byte[100];
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await socket.ReadAsync(buffer.AsMemory(), 100, CancellationToken.None);
                    }
                    catch (TimeoutException)
                    {
                        // Expected
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // Assert - Total time should be much less than operationsCount * timeout
            // because operations should run in parallel and each should timeout independently
            var maxExpectedTime = receiveTimeout.TotalMilliseconds * 3; // Some buffer for test execution
            sw.ElapsedMilliseconds.Should().BeLessThan((long)maxExpectedTime,
                $"Multiple timeout operations should not accumulate time beyond individual timeouts. " +
                $"Expected < {maxExpectedTime}ms, but got {sw.ElapsedMilliseconds}ms");
        }
        finally
        {
            socket.Destroy();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("Configuration")]
    public void SocketPoolConfiguration_ReceiveTimeout_ShouldBeConfigurable()
    {
        // Arrange & Act
        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ReceiveTimeout = TimeSpan.FromMilliseconds(500),
            ConnectionTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 10,
            SocketPoolingTimeout = TimeSpan.FromMilliseconds(150)
        };

        // Assert
        config.ReceiveTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
        
        // Validate should not throw for valid configuration
        Action validate = () => config.Validate();
        validate.Should().NotThrow();
    }

    [TestMethod]
    [TestCategory("Configuration")]
    public void SocketPoolConfiguration_InvalidReceiveTimeout_ShouldThrow()
    {
        // Arrange
        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ReceiveTimeout = TimeSpan.Zero, // Invalid
            ConnectionTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 10,
            SocketPoolingTimeout = TimeSpan.FromMilliseconds(150)
        };

        // Act & Assert
        Action validate = () => config.Validate();
        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*ReceiveTimeout*");
    }
}


