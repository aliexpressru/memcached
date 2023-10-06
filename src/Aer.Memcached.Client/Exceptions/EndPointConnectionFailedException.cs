namespace Aer.Memcached.Client.Exceptions;

internal class EndPointConnectionFailedException : Exception
{
	public EndPointConnectionFailedException(string endPointAddress) 
		: base($"Could not connect to {endPointAddress}.")
	{ }
}
