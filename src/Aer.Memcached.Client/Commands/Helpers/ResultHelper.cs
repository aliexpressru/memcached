using System.Text;

namespace Aer.Memcached.Client.Commands.Helpers;

internal static class ResultHelper
{
    public static string ProcessResponseData(ReadOnlyMemory<byte> data, string message = "")
    {
        if (data.Length <= 0)
        {
            return string.Empty;
        }

        try
        {
            return message +
                (!string.IsNullOrEmpty(message)
                    ? ": "
                    : "") +
                Encoding.UTF8.GetString(data.Span);
        }
        catch (Exception ex)
        {
            return ex.GetBaseException().Message;
        }
    }
}