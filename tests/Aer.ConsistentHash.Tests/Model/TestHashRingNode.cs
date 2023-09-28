using System.Net;
using Aer.ConsistentHash.Abstractions;

namespace Aer.ConsistentHash.Tests.Model;

public class TestHashRingNode: INode
{
    private readonly string _key = Guid.NewGuid().ToString();

    public string GetKey()
    {
        return _key;
    }

    public EndPoint GetEndpoint()
    {
        throw new NotImplementedException();
    }

    protected bool Equals(TestHashRingNode other)
    {
        return _key == other._key;
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

        return Equals((TestHashRingNode)obj);
    }

    public override int GetHashCode()
    {
        return (_key != null ? _key.GetHashCode() : 0);
    }
}