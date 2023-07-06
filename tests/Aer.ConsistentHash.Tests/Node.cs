using System.Net;

namespace Aer.ConsistentHash.Tests;

public class Node: INode
{
    private readonly string _key;

    public Node()
    {
        _key = Guid.NewGuid().ToString();
    }
    
    public string GetKey()
    {
        return _key;
    }

    public EndPoint GetEndpoint()
    {
        throw new NotImplementedException();
    }

    protected bool Equals(Node other)
    {
        return _key == other._key;
    }

    public bool Equals(INode other)
    {
        return GetKey() == other?.GetKey();
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;

        return Equals((Node)obj);
    }

    public override int GetHashCode()
    {
        return (_key != null ? _key.GetHashCode() : 0);
    }
}