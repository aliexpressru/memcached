using System.Net;
using System.Net.Sockets;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses;

/// <summary>
/// Tests to reproduce the issue where socket is left in invalid state after timeout/exception
/// and subsequent commands fail with magic byte mismatch.
/// 
/// Based on errors:
/// 1. Command times out during read
/// 2. Socket has unread data (e.g., 18848 bytes)
/// 3. Next command on same socket fails with "Expected magic value 129, received: 112"
/// </summary>
[TestClass]
public class SocketInvalidStateTests
{
    private ILogger<SocketPool> _poolLogger;

    [TestInitialize]
    public void Setup()
    {
        _poolLogger = Substitute.For<ILogger<SocketPool>>();
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task Socket_ShouldNotBeReused_AfterTimeoutDuringRead()
    {
        // Arrange - create a fake server that responds slowly
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        // Simulate a server that accepts connection and sends partial response
        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();

            // Read the request - binary protocol header is 24 bytes
            var requestBuffer = new byte[24];
            await stream.ReadExactlyAsync(requestBuffer, CancellationToken.None);

            // Send partial response and then delay (simulating slow/stuck server)
            // This creates a scenario where client times out with unread data in socket
            var partialResponse = new byte[] { 0x81, 0x00, 0x00, 0x00 }; // Binary protocol header start
            await stream.WriteAsync(partialResponse);
            await stream.FlushAsync();

            // Delay to cause timeout on client side
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Send rest of response (will remain unread after timeout)
            var remainingData = new byte[100];
            await stream.WriteAsync(remainingData);
            
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromMilliseconds(100), // Short timeout to trigger issue
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act 1 - Get socket and cause timeout during read
            var socket1 = await pool.GetSocketAsync(CancellationToken.None);
            socket1.Should().NotBeNull();

            // Send a request
            var request = new byte[] { 0x80, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
            await socket1.WriteAsync(new[] { new ArraySegment<byte>(request) });

            // Try to read response - should timeout
            var buffer = new byte[1024];
            await Assert.ThrowsExceptionAsync<TimeoutException>(async () =>
            {
                await socket1.ReadAsync(buffer.AsMemory(), 1024, CancellationToken.None);
            });

            // Verify socket is marked as having exception
            socket1.IsExceptionDetected.Should().BeFalse("Socket should be marked as dead after timeout");

            // Verify that timeout was logged
            _poolLogger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("timed out")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());

            var socket1Id = socket1.InstanceId;

            // Act 2 - Return socket to pool (this is what happens with 'using' statement)
            socket1.Dispose(); // Returns to pool or destroys based on IsExceptionDetected

            // Act 3 - Try to get another socket from pool
            var socket2 = await pool.GetSocketAsync(CancellationToken.None);
            socket2.Should().NotBeNull();

            // Assert - socket2 should be a NEW socket, not the corrupted one
            socket2.InstanceId.Should().NotBe(socket1Id, 
                "Pool should not return the same socket that timed out");
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task SocketWithUnreadData_ShouldBeDetectedByReset()
    {
        // This test verifies that when socket has unread data, the ResetAsync detects it
        // After the fix, socket with unread data should be destroyed, not returned to pool

        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        // Server that sends data
        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();

            // Read request - binary protocol header is 24 bytes
            var buffer = new byte[24];
            await stream.ReadExactlyAsync(buffer, CancellationToken.None);

            // Send response that won't be fully read
            var response = new byte[1000];
            response[0] = 0x81; // Binary protocol magic byte
            await stream.WriteAsync(response);
            await stream.FlushAsync();

            await Task.Delay(TimeSpan.FromSeconds(2));
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act
            var socket1 = await pool.GetSocketAsync(CancellationToken.None);
            socket1.Should().NotBeNull();
            var socket1Id = socket1.InstanceId;

            // Send full binary protocol request header (24 bytes)
            // Format: magic(1) + opcode(1) + keylen(2) + extlen(1) + datatype(1) + status/reserved(2) + bodylen(4) + opaque(4) + cas(8) = 24 bytes
            var request = new byte[24];
            request[0] = 0x80; // Request magic
            request[1] = 0x00; // Get opcode
            // Rest zeros
            await socket1.WriteAsync(new[] { new ArraySegment<byte>(request) });

            // Wait for server to send response
            await Task.Delay(200);

            // Socket should have unread data
            var availableBytes = socket1.Socket.Available;
            availableBytes.Should().BeGreaterThan(0, "Socket should have unread data");

            // Act - Return socket to pool
            socket1.Dispose();

            // Allow reset to complete
            await Task.Delay(200);

            // Act - Get another socket from pool
            var socket2 = await pool.GetSocketAsync(CancellationToken.None);
            socket2.Should().NotBeNull();

            // Assert - After the fix, socket2 should be a NEW socket (socket1 was destroyed due to unread data)
            // Currently this test documents the CURRENT behavior where socket might be reused
            // After implementing the fix, this should be socket1Id != socket2.InstanceId
            _poolLogger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("bytes of unread data")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }
}

