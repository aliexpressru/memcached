using System;
using System.Net;

namespace Aer.ConsistentHash;

public interface INode: IEquatable<INode>
{
    string GetKey();

    EndPoint GetEndpoint();
}