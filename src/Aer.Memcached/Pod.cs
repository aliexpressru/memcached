using System.Net;
using Aer.ConsistentHash.Abstractions;

namespace Aer.Memcached;

public class Pod: INode
{
    public int MemcachedPort { get; init; } = 11211;
    
    public string IpAddress { get; init; }

    public string GetKey()
    {
        return IpAddress;
    }

    public EndPoint GetEndpoint()
    {
        return new DnsEndPoint(IpAddress, MemcachedPort);
    }

    protected bool Equals(Pod other)
    {
        return IpAddress == other.IpAddress;
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

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((Pod)obj);
    }

    public override int GetHashCode()
    {
        return (IpAddress != null ? IpAddress.GetHashCode() : 0);
    }
}