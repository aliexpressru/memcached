namespace Aer.Memcached.Tests.Model.StoredObjects;

internal record SimpleRecord
{
	public long Long { get; init; }

	public int Int { get; init; }

	public DateTime DateTime { get; set; }
}
