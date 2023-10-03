using Aer.Memcached.Client.Commands.Enums;

namespace Aer.Memcached.Client.Commands.Extensions;

internal static class StoreModeExtensions
{
    public static OpCode Resolve(this StoreMode storeMode)
    {
        var opCode = storeMode switch
        {
            StoreMode.Add => OpCode.Add,
            StoreMode.Set => OpCode.Set,
            StoreMode.Replace => OpCode.Replace,
            _ => throw new ArgumentOutOfRangeException(nameof(storeMode), $"{storeMode} is not supported")
        };

        return opCode;
    }
}