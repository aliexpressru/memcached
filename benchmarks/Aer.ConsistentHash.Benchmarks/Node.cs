using System.Net;
using Aer.ConsistentHash.Abstractions;

namespace Aer.ConsistentHash.Benchmarks;

public class Node: INode
{
    private static readonly int MemcachedPort = 11211;
    
    public string IpAddress { get; init; }

    public string GetKey()
    {
        return IpAddress;
    }

    public EndPoint GetEndpoint()
    {
        return new DnsEndPoint(IpAddress, MemcachedPort);
    }

    private bool Equals(Node other)
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

        return Equals((Node)obj);
    }

    public override int GetHashCode()
    {
        return (IpAddress != null ? IpAddress.GetHashCode() : 0);
    }
}