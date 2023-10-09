using System.Net;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;

namespace Aer.Memcached;

public class Pod: INode
{
    private readonly string _nodeKey;

    public string IpAddress { get; }
        
    public int MemcachedPort { get; }

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

    public string GetKey()
    {
        return _nodeKey;
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