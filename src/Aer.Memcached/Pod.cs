using System.Net;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;

namespace Aer.Memcached;

public class Pod: INode
{
    public int MemcachedPort { get; init; } = MemcachedConfiguration.DefaultMemcachedPort;
    
    public string IpAddress { get; init; }

    public Pod(string ipAddress)
    {
        if (ipAddress is null or {Length: 0})
        {
            throw new ArgumentException($"Non-empty {nameof(ipAddress)} must be specified");
        }

        IpAddress = ipAddress;
    }

    public Pod(string ipAddress, int port) : this(ipAddress)
    {
        if (port <= 0)
        {
            throw new ArgumentException($"{nameof(port)} must be greater than 0");
        }

        MemcachedPort = port;
    }

    public string GetKey()
    {
        return $"{IpAddress}:{MemcachedPort}";
    }

    public EndPoint GetEndpoint()
    {
        return new DnsEndPoint(IpAddress, MemcachedPort);
    }

    protected bool Equals(Pod other)
    {
        return IpAddress == other.IpAddress && MemcachedPort == other.MemcachedPort;
    }

    public bool Equals(INode other)
    {
        return GetKey() == other?.GetKey();
    }

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

        return Equals((Pod)obj);
    }

    public override int GetHashCode()
    {
        var ipAddressHash = IpAddress.GetHashCode();

        var portHash = MemcachedPort.GetHashCode();
        
        return HashCode.Combine(ipAddressHash, portHash);
    }
}