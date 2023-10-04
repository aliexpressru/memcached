using System.Net;

namespace Aer.Memcached.Client.Extensions;

public static class EndPointExtensions
{
	public static string GetEndPointString(this EndPoint endPoint)
	{
		// conditional Replace call here since, according to static analysis,
		// for some reason ToString() can produce null even on non-null endPoint

		var ret = endPoint.ToString()
			?.Replace(
				"Unspecified/",
				string.Empty,
				StringComparison.InvariantCulture);

		return ret ?? "";
	}
}
