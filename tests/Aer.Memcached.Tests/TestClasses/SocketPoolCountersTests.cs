using System.Net;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class SocketPoolCountersTests
{
    private readonly SocketPool _socketPool;
    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;

    public SocketPoolCountersTests()
    {
        _config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            MaxPoolSize = 5,
            ConnectionTimeout = TimeSpan.FromSeconds(5),
            ReceiveTimeout = TimeSpan.FromSeconds(5),
            SocketPoolingTimeout = TimeSpan.FromSeconds(1),
            MaximumSocketCreationAttempts = 3
        };

        var endpoint = new IPEndPoint(IPAddress.Loopback, 11211);
        _socketPool = new SocketPool(endpoint, _config, NullLogger.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _socketPool?.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public void InitialCounters_ShouldBeCorrect()
    {
        // Assert
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task AfterAcquiringSocket_CountersShouldBeUpdated()
    {
        // Act
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);

        // Assert
        Assert.IsNotNull(socket);
        Assert.AreEqual(1, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 1, _socketPool.RemainingPoolCapacity);

        // Cleanup
        socket.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task AfterReturningSocketToPool_CountersShouldBeUpdated()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        // Act - return socket to pool
        socket.Dispose();

        // Wait a bit for async operations
        await Task.Delay(50);

        // Assert
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task AfterDestroyingSocket_CountersShouldBeUpdated()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        // Act - mark socket for destruction and dispose
        socket.ShouldDestroySocket = true;
        socket.Dispose();

        // Wait a bit for async operations
        await Task.Delay(50);

        // Assert
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task MultipleSocketsAcquireAndReturn_CountersShouldBeCorrect()
    {
        // Arrange & Act
        var socket1 = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socket2 = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socket3 = await _socketPool.GetSocketAsync(CancellationToken.None);

        // Assert after acquiring 3 sockets
        Assert.AreEqual(3, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 3, _socketPool.RemainingPoolCapacity);

        // Act - return first socket
        socket1.Dispose();
        await Task.Delay(50);

        // Assert after returning 1 socket
        Assert.AreEqual(2, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 2, _socketPool.RemainingPoolCapacity);

        // Act - destroy second socket
        socket2.ShouldDestroySocket = true;
        socket2.Dispose();
        await Task.Delay(50);

        // Assert after destroying 1 socket
        Assert.AreEqual(1, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 1, _socketPool.RemainingPoolCapacity);

        // Act - return third socket
        socket3.Dispose();
        await Task.Delay(50);

        // Assert after returning last socket
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(2, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ReusingSocketFromPool_CountersShouldBeCorrect()
    {
        // Arrange - acquire and return socket
        var socket1 = await _socketPool.GetSocketAsync(CancellationToken.None);
        socket1.Dispose();
        await Task.Delay(50);

        // Act - acquire socket again (should reuse from pool)
        var socket2 = await _socketPool.GetSocketAsync(CancellationToken.None);

        // Assert
        Assert.IsNotNull(socket2);
        Assert.AreEqual(1, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 1, _socketPool.RemainingPoolCapacity);

        // Cleanup
        socket2.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task MaxPoolSize_ShouldBeLimited()
    {
        // Arrange & Act - acquire all available sockets
        var sockets = new PooledSocket[_config.MaxPoolSize];
        for (int i = 0; i < _config.MaxPoolSize; i++)
        {
            sockets[i] = await _socketPool.GetSocketAsync(CancellationToken.None);
            Assert.IsNotNull(sockets[i]);
        }

        // Assert - all sockets acquired
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(0, _socketPool.RemainingPoolCapacity);

        // Act - try to acquire one more with external cancellation token
        // Should throw OperationCanceledException when token expires
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        try
        {
            await _socketPool.GetSocketAsync(cts.Token);
            Assert.Fail("Should have thrown OperationCanceledException");
        }
        catch (OperationCanceledException)
        {
            // Expected - external token was cancelled
        }

        // Assert - pool state unchanged (no socket was acquired)
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.RemainingPoolCapacity);

        // Cleanup
        foreach (var socket in sockets)
        {
            socket.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task DestroyPooledSocket_CountersShouldBeUpdated()
    {
        // Arrange - create 2 sockets and return them to pool
        var socket1 = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socket2 = await _socketPool.GetSocketAsync(CancellationToken.None);
        socket1.Dispose();
        socket2.Dispose();
        await Task.Delay(50);

        // Assert initial state
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(2, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);

        // Act - destroy one socket from pool
        var destroyed = _socketPool.DestroyPooledSocket();

        // Assert
        Assert.IsTrue(destroyed);
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public void SocketWithoutReturnToPoolCallback_DisposeShouldNotCrash()
    {
        // Arrange - create socket without pool (no callback set)
        var endpoint = new IPEndPoint(IPAddress.Loopback, 11211);
        var socket = new PooledSocket(
            endpoint,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            NullLogger.Instance);

        // Act & Assert - should not crash
        socket.Dispose();

        // Second dispose should also not crash (idempotency)
        socket.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task DirectDestroy_OnPooledSocket_CreatesCounterLeak()
    {
        // This test documents INCORRECT usage - do NOT call Destroy() on pooled sockets!
        // Destroy() should only be called on sockets created without pool.
        // This test exists to document the problem if someone misuses the API.

        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);
        Assert.AreEqual(1, _socketPool.UsedSocketsCount);

        // Act - INCORRECT: call Destroy() directly bypassing pool callback
        // This is WRONG and will cause counter leak!
        socket.Destroy();
        await Task.Delay(50);

        // Assert - this shows the PROBLEM: counters are NOT updated, creating a leak
        // UsedSocketsCount still shows 1 even though socket is destroyed
        Assert.AreEqual(1, _socketPool.UsedSocketsCount,
            "Counter leak: UsedSocketsCount not decremented because Destroy() bypassed pool");
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 1, _socketPool.RemainingPoolCapacity,
            "Counter leak: RemainingPoolCapacity not restored because Destroy() bypassed pool");

        // CORRECT usage: always use Dispose() for pooled sockets, or set ShouldDestroySocket flag
        // socket.Dispose(); // ✓ Correct
        // socket.ShouldDestroySocket = true; socket.Dispose(); // ✓ Also correct
        // socket.Destroy(); // ✗ WRONG for pooled sockets!
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task SocketWithError_AutomarkedForDestruction()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);
        Assert.IsFalse(socket.ShouldDestroySocket, "Socket should initially be marked as reusable");

        // Simulate error in socket operations - this would normally happen inside ReadAsync/WriteAsync
        // For testing, we manually set the flag
        socket.ShouldDestroySocket = true;

        // Act - dispose socket
        socket.Dispose();
        await Task.Delay(50);

        // Assert - socket should be destroyed, not returned to pool
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task MultipleDisposeCalls_ShouldBeIdempotent()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        // Act - dispose multiple times
        socket.Dispose();
        await Task.Delay(50);

        var countersAfterFirstDispose = (
            Used: _socketPool.UsedSocketsCount,
            Pooled: _socketPool.PooledSocketsCount,
            Remaining: _socketPool.RemainingPoolCapacity
        );

        // Second dispose - should not change counters
        socket.Dispose();
        await Task.Delay(50);

        // Assert - counters should remain the same
        Assert.AreEqual(countersAfterFirstDispose.Used, _socketPool.UsedSocketsCount);
        Assert.AreEqual(countersAfterFirstDispose.Pooled, _socketPool.PooledSocketsCount);
        Assert.AreEqual(countersAfterFirstDispose.Remaining, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task MixedOperations_CountersShouldRemainConsistent()
    {
        // This test simulates a real-world scenario with mixed operations
        var socket1 = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socket2 = await _socketPool.GetSocketAsync(CancellationToken.None);

        Assert.AreEqual(2, _socketPool.UsedSocketsCount);

        // Return socket1 to pool
        socket1.Dispose();
        await Task.Delay(50);

        Assert.AreEqual(1, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);

        // Reuse socket from pool
        var socket3 = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.AreEqual(2, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);

        // Destroy socket2 (mark for destruction)
        socket2.ShouldDestroySocket = true;
        socket2.Dispose();
        await Task.Delay(50);

        Assert.AreEqual(1, _socketPool.UsedSocketsCount);
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize - 1, _socketPool.RemainingPoolCapacity);

        // Return socket3
        socket3.Dispose();
        await Task.Delay(50);

        // Final state: all sockets returned or destroyed, capacity restored
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ResetDisposedFlag_ShouldAllowSocketReuse()
    {
        // Arrange - acquire socket and return to pool
        var socket1 = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socket1Id = socket1.InstanceId;
        socket1.Dispose();
        await Task.Delay(50);

        Assert.AreEqual(1, _socketPool.PooledSocketsCount);

        // Act - get socket again (should reuse the same socket with reset flag)
        var socket2 = await _socketPool.GetSocketAsync(CancellationToken.None);

        // Assert - same socket instance reused
        Assert.IsNotNull(socket2);
        Assert.AreEqual(socket1Id, socket2.InstanceId, "Should reuse same socket from pool");
        Assert.IsFalse(socket2.ShouldDestroySocket, "ShouldDestroySocket should be reset to false");

        // Cleanup
        socket2.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task CheckDisposed_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        // Mark for destruction to avoid returning to pool
        socket.ShouldDestroySocket = true;
        socket.Dispose();
        await Task.Delay(50);

        // Act & Assert - operations on disposed socket should throw
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
        {
            await socket.ReadAsync(new byte[10].AsMemory(), 10, CancellationToken.None);
        });

        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
        {
            await socket.WriteAsync(new[] { new ArraySegment<byte>(new byte[10]) });
        });
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ReturnToPoolCallback_ShouldBeInvokedOnDispose()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        var callbackInvoked = false;
        var originalCallback = socket.ReturnToPoolCallback;
        
        // Wrap callback to track invocation
        socket.ReturnToPoolCallback = (s) =>
        {
            callbackInvoked = true;
            originalCallback?.Invoke(s);
        };

        // Act
        socket.Dispose();
        await Task.Delay(50);

        // Assert
        Assert.IsTrue(callbackInvoked, "ReturnToPoolCallback should be invoked on Dispose");
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ReturnToPoolCallback_ShouldNotBeInvokedOnDestroy()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        var callbackInvoked = false;
        var originalCallback = socket.ReturnToPoolCallback;
        
        socket.ReturnToPoolCallback = (s) =>
        {
            callbackInvoked = true;
            originalCallback?.Invoke(s);
        };

        // Act - call Destroy() directly (bypasses callback)
        socket.Destroy();
        await Task.Delay(50);

        // Assert
        Assert.IsFalse(callbackInvoked, "ReturnToPoolCallback should NOT be invoked on Destroy");
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ConcurrentAcquireAndReturn_CountersShouldRemainConsistent()
    {
        // Arrange & Act - multiple threads acquiring and returning sockets
        var tasks = new List<Task>();
        var iterations = 20;

        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
                if (socket != null)
                {
                    await Task.Delay(10); // Simulate some work
                    socket.Dispose();
                }
            }));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(100); // Allow all returns to complete

        // Assert - counters should be consistent
        Assert.AreEqual(0, _socketPool.UsedSocketsCount, "All sockets should be returned");
        Assert.IsTrue(_socketPool.PooledSocketsCount <= _config.MaxPoolSize, 
            "Pooled sockets should not exceed max pool size");
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity,
            "Capacity should be fully restored");
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ConcurrentDisposeOfSameSocket_ShouldBeIdempotent()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.IsNotNull(socket);

        // Act - try to dispose same socket from multiple threads
        var disposeTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => socket.Dispose()))
            .ToList();

        await Task.WhenAll(disposeTasks);
        await Task.Delay(100);

        // Assert - counters should be consistent (only one dispose should take effect)
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task SocketMarkedForDestruction_ShouldNotBePooled()
    {
        // Arrange
        var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socketId = socket.InstanceId;
        
        // Mark for destruction
        socket.ShouldDestroySocket = true;

        // Act
        socket.Dispose();
        await Task.Delay(50);

        // Assert - socket destroyed, not pooled
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);

        // Get another socket - should be new instance
        var newSocket = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.AreNotEqual(socketId, newSocket.InstanceId);

        // Cleanup
        newSocket.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task PoolDisposal_ShouldDestroyAllPooledSockets()
    {
        // Arrange - create separate pool for this test
        var endpoint = new IPEndPoint(IPAddress.Loopback, 11211);
        var testPool = new SocketPool(endpoint, _config, NullLogger.Instance);

        var socket1 = await testPool.GetSocketAsync(CancellationToken.None);
        var socket2 = await testPool.GetSocketAsync(CancellationToken.None);
        
        socket1.Dispose();
        socket2.Dispose();
        await Task.Delay(50);

        Assert.AreEqual(2, testPool.PooledSocketsCount);

        // Act - dispose pool
        testPool.Dispose();

        // Assert - all sockets destroyed
        Assert.AreEqual(0, testPool.PooledSocketsCount);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task ReusedSocket_AfterMarkedForDestruction_ShouldBeNewInstance()
    {
        // Arrange
        var socket1 = await _socketPool.GetSocketAsync(CancellationToken.None);
        var socket1Id = socket1.InstanceId;
        
        // Return to pool
        socket1.Dispose();
        await Task.Delay(50);

        // Get from pool and mark for destruction
        var socket2 = await _socketPool.GetSocketAsync(CancellationToken.None);
        Assert.AreEqual(socket1Id, socket2.InstanceId, "Should reuse same socket");
        
        socket2.ShouldDestroySocket = true;
        socket2.Dispose();
        await Task.Delay(50);

        // Act - get another socket
        var socket3 = await _socketPool.GetSocketAsync(CancellationToken.None);

        // Assert - should be new instance since previous was destroyed
        Assert.AreNotEqual(socket1Id, socket3.InstanceId);
        Assert.IsFalse(socket3.ShouldDestroySocket);

        // Cleanup
        socket3.Dispose();
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public void DestroyPooledSocket_WhenPoolIsEmpty_ShouldReturnFalse()
    {
        // Arrange - ensure pool is empty
        Assert.AreEqual(0, _socketPool.PooledSocketsCount);

        // Act
        var destroyed = _socketPool.DestroyPooledSocket();

        // Assert
        Assert.IsFalse(destroyed, "Should return false when no sockets to destroy");
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }

    [TestMethod]
    [TestCategory("SocketPoolCounters")]
    public async Task SequentialDisposeAndReacquire_ShouldMaintainConsistency()
    {
        // This test verifies that ResetDisposedFlag works correctly in sequence
        for (int i = 0; i < 10; i++)
        {
            // Acquire
            var socket = await _socketPool.GetSocketAsync(CancellationToken.None);
            Assert.IsNotNull(socket);
            Assert.AreEqual(1, _socketPool.UsedSocketsCount);

            // Return
            socket.Dispose();
            await Task.Delay(50);
            Assert.AreEqual(0, _socketPool.UsedSocketsCount);
            Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        }

        // Final consistency check
        Assert.AreEqual(0, _socketPool.UsedSocketsCount);
        Assert.AreEqual(1, _socketPool.PooledSocketsCount);
        Assert.AreEqual(_config.MaxPoolSize, _socketPool.RemainingPoolCapacity);
    }
}