using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;

namespace Aer.Memcached.Client.Commands;

public class SaslStartCommand: SaslCommandBase
{
    private readonly byte[] _authData;

    public SaslStartCommand(byte[] authData) : base(OpCode.SaslStart)
    {
        _authData = authData;
    }
    
    public override IList<ArraySegment<byte>> GetBuffer()
    {
        var request = new BinaryRequest(OpCode)
        {
            Key = SaslMechanism,
            Data = new ArraySegment<byte>(_authData)
        };

        return request.CreateBuffer();
    }
}