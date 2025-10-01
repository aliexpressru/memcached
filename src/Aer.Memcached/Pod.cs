using System.Net;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;

namespace Aer.Memcached;

/// <summary>
/// Represents a Memcached pod (node) in the cluster.
/// </summary>
public class Pod : INode
{
    private readonly string _nodeKey;

    /// <summary>
    /// The IP address of the Memcached pod.
    /// </summary>
    public string IpAddress { get; }

    /// <summary>
    /// The port on which the Memcached pod is listening.
    /// </summary>
    public int MemcachedPort { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pod"/> class with the specified IP address and port.
    /// </summary>
    /// <param name="ipAddress">The IP address of the Memcached pod.</param>
    /// <param name="port">The port on which the Memcached pod is listening.</param>
    /// <exception cref="ArgumentException">Occurs when either <paramref name="ipAddress"/> or <paramref name="port"/> is not specified or empty.</exception>
    public Pod(string ipAddress, int port = MemcachedConfiguration.DefaultMemcachedPort)
    {
        if (ipAddress is null or {Length: 0})
        {
            throw new ArgumentException($"Non-empty {nameof(ipAddress)} must be specified");
        }

        if (port <= 0)
        {
            throw new ArgumentException($"{nameof(port)} must be greater than 0");
        }

        IpAddress = ipAddress;
        MemcachedPort = port;

        _nodeKey = $"{IpAddress}:{MemcachedPort}";
    }

    /// <inheritdoc/>
    public string GetKey()
    {
        return _nodeKey;
    }

    /// <inheritdoc/>
    public EndPoint GetEndpoint()
    {
        return new DnsEndPoint(IpAddress, MemcachedPort);
    }

    /// <summary>
    /// Determines whether the specified Pod is equal to the current Pod.
    /// </summary>
    /// <param name="other">The pod to compare this pod to.</param>
    protected bool Equals(Pod other)
    {
        return IpAddress == other.IpAddress && MemcachedPort == other.MemcachedPort;
    }

    /// <inheritdoc/>
    public bool Equals(INode other)
    {
        return GetKey() == other?.GetKey();
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((Pod) obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var ipAddressHash = IpAddress.GetHashCode();

        var portHash = MemcachedPort.GetHashCode();

        return HashCode.Combine(ipAddressHash, portHash);
    }
}