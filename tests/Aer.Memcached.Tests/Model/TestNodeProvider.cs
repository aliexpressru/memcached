using Aer.Memcached.Abstractions;

namespace Aer.Memcached.Tests.Model;

internal class TestNodeProvider : INodeProvider<Pod>
{
	private readonly List<Pod> _staticPods;

	public TestNodeProvider(IEnumerable<Pod> staticPods)
	{
		_staticPods = staticPods.ToList();
	}

	public bool IsConfigured()
	{
		return true;
	}

	public ICollection<Pod> GetNodes()
	{
		return _staticPods;
	}
}
