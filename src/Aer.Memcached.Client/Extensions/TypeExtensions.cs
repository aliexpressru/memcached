namespace Aer.Memcached.Client.Extensions;

public static class TypeExtensions
{
    public static string GetTypeName<T>()
    {
        var type = typeof(T);

        return type.GenericTypeArguments.Length == 0
            ? $"-{typeof(T).Name.ToLowerInvariant()}"
            : $"-{typeof(T).Name.ToLowerInvariant()}-" +
              string.Join("-", type.GenericTypeArguments.Select(x => x.Name.ToLowerInvariant()));
    }
}