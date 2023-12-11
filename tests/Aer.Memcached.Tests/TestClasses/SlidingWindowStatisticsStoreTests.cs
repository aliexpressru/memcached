using Aer.Memcached.Client.CacheSync;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class SlidingWindowStatisticsStoreTests
{
    [TestMethod]
    public async Task NoPreviousWindow_TooManyErrors()
    {
        var store = GetStore();
        var key = "test";
        var maxErrors = 5;
        var numberOfExceededErrors = 10;

        await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromSeconds(5));

        var tasks = Enumerable.Range(0, maxErrors + numberOfExceededErrors
                                        - 1) // one for initial creation
            .Select(n => store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromSeconds(5)))
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Count(t => t.Result.IsTooManyErrors).Should().Be(numberOfExceededErrors);
    }

    [TestMethod]
    public async Task NoPreviousWindow_NotTooManyErrors()
    {
        var store = GetStore();
        var key = "test";
        var maxErrors = 5;
        var numberOfExceededErrors = 0;

        await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromSeconds(5));

        var tasks = Enumerable.Range(0, maxErrors + numberOfExceededErrors
                                        - 2) // one for initial creation and another one for the edge of maxErrors
            .Select(n => store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromSeconds(5)))
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Count(t => t.Result.IsTooManyErrors).Should().Be(numberOfExceededErrors);
    }

    [TestMethod]
    public async Task NoPreviousWindow_Always100PercentForCurrentWindowPercentage()
    {
        var store = GetStore();
        var key = "test";
        var maxErrors = 5;
        var numberOfExceededErrors = 10;

        await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(1));

        var tasks = Enumerable.Range(0, maxErrors + numberOfExceededErrors
                                        - 1) // one for initial creation
            .Select(n => store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromSeconds(2)))
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Count(t => t.Result.IsTooManyErrors).Should().Be(numberOfExceededErrors);
    }

    [TestMethod]
    public async Task NoPreviousWindow_CurrentErrorAfterTheRightEdgeOfCurrentWindow_SmallPercentageBeforeCreationOfNewWindow_TooManyErrors()
    {
        var store = GetStore();
        var key = "test";
        var maxErrors = 2;
        var windowMilliseconds = 1000;
        var errorsInWindow = 13;
        var delayToEvenDistributionOfErrors = windowMilliseconds / errorsInWindow;

        await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(1000));
        
        for (int i = 0; i < errorsInWindow; i++)
        {
            await Task.Delay(delayToEvenDistributionOfErrors);
            await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(1000));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(650));

        // making enough errors to have TooManyErrors.
        // is enough to get TooManyErrors in 10-15 percentage window
        var tasks = Enumerable.Range(0, (int)((maxErrors + 1) / 0.10)) // + 1 for the strict edge of max errors
            .Select(n => store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(1000)))
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Count(t => t.Result.IsTooManyErrors).Should().BeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public async Task WithPreviousWindow_TooManyErrors()
    {
        var store = GetStore();
        var key = "test";
        var maxErrors = 2;
        var windowMilliseconds = 1000;
        var errorsInWindow = 13;
        var delayToEvenDistributionOfErrors = windowMilliseconds / errorsInWindow;

        await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(1000));


        for (int i = 0; i < errorsInWindow - 1; i++)
        {
            await Task.Delay(delayToEvenDistributionOfErrors);
            await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(1000));
        }

        var tasks = Enumerable.Range(0, maxErrors)
            .Select(n => store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(1000)))
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Count(t => t.Result.IsTooManyErrors).Should().BeGreaterThanOrEqualTo(1);
    }
    
    [TestMethod]
    public async Task MinimalWorkload()
    {
        var store = GetStore();
        var key = "test";
        var maxErrors = 2;
        var windowMilliseconds = 300;
        
        var statistics = await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(windowMilliseconds));
        statistics.TimeFrameStatistics.Should().BeNull();

        var results = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(windowMilliseconds + 100);
            
            var result = await store.GetErrorStatisticsAsync(key, maxErrors, TimeSpan.FromMilliseconds(windowMilliseconds));
            result.TimeFrameStatistics.Should().BeNull();
            results.Add(result.IsTooManyErrors);
        }

        results.Count(r => r).Should().Be(0);
    }

    private SlidingWindowStatisticsStore GetStore()
    {
        return new SlidingWindowStatisticsStore();
    }
}
