using System.Net;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;

namespace Aer.ConsistentHash.Benchmarks.Model;

internal class Node: INode
{
    public string IpAddress { get; init; }

    public string GetKey()
    {
        return $"{IpAddress}:{MemcachedConfiguration.DefaultMemcachedPort}";
    }

    public EndPoint GetEndpoint()
    {
        return new DnsEndPoint(IpAddress, MemcachedConfiguration.DefaultMemcachedPort);
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