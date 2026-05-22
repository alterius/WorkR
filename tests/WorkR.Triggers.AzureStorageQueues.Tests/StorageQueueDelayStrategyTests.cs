using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

[Trait("Category", "L0")]
public class StorageQueueDelayStrategyTests
{
    // ---------------------------------------------------------------------------
    // Fixed
    // ---------------------------------------------------------------------------

    [Fact]
    public void Fixed_ReturnsConfiguredDelay() =>
        StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(5))(0)
            .ShouldBe(TimeSpan.FromSeconds(5));

    [Fact]
    public void Fixed_ReturnsSameDelayForAnyCount()
    {
        var strategy = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(3));

        strategy(0).ShouldBe(TimeSpan.FromSeconds(3));
        strategy(10).ShouldBe(TimeSpan.FromSeconds(3));
        strategy(int.MaxValue).ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Fixed_WhenDelayIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Fixed(TimeSpan.Zero));

    [Fact]
    public void Fixed_WhenDelayIsNegative_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(-1)));

    // ---------------------------------------------------------------------------
    // Linear
    // ---------------------------------------------------------------------------

    [Fact]
    public void Linear_AtCountZero_ReturnsInitial() =>
        StorageQueueDelayStrategy.Linear(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30))(0)
            .ShouldBe(TimeSpan.FromSeconds(1));

    [Fact]
    public void Linear_IncreasesWithCount()
    {
        var strategy = StorageQueueDelayStrategy.Linear(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));

        strategy(1).ShouldBe(TimeSpan.FromSeconds(3));
        strategy(2).ShouldBe(TimeSpan.FromSeconds(5));
        strategy(3).ShouldBe(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void Linear_ClampsToMax()
    {
        var strategy = StorageQueueDelayStrategy.Linear(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        strategy(2).ShouldBe(TimeSpan.FromSeconds(10));
        strategy(100).ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Linear_DoesNotOverflowAtMaxCount()
    {
        var strategy = StorageQueueDelayStrategy.Linear(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Should.NotThrow(() => strategy(int.MaxValue)).ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Linear_WhenInitialIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Linear(TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)));

    [Fact]
    public void Linear_WhenStepIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Linear(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromSeconds(30)));

    [Fact]
    public void Linear_WhenMaxIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Linear(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.Zero));

    // ---------------------------------------------------------------------------
    // Exponential
    // ---------------------------------------------------------------------------

    [Fact]
    public void Exponential_AtCountZero_ReturnsInitial() =>
        StorageQueueDelayStrategy.Exponential(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))(0)
            .ShouldBe(TimeSpan.FromSeconds(1));

    [Fact]
    public void Exponential_DoublesWithEachCount()
    {
        var strategy = StorageQueueDelayStrategy.Exponential(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));

        strategy(1).ShouldBe(TimeSpan.FromSeconds(2));
        strategy(2).ShouldBe(TimeSpan.FromSeconds(4));
        strategy(3).ShouldBe(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void Exponential_ClampsToMax()
    {
        var strategy = StorageQueueDelayStrategy.Exponential(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

        strategy(4).ShouldBe(TimeSpan.FromSeconds(10));
        strategy(100).ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Exponential_DoesNotOverflowAtMaxCount()
    {
        var strategy = StorageQueueDelayStrategy.Exponential(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));

        Should.NotThrow(() => strategy(int.MaxValue)).ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Exponential_DoesNotOverflowAtCount63()
    {
        var strategy = StorageQueueDelayStrategy.Exponential(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1));

        Should.NotThrow(() => strategy(63)).ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Exponential_WhenInitialIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Exponential(TimeSpan.Zero, TimeSpan.FromSeconds(60)));

    [Fact]
    public void Exponential_WhenMaxIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StorageQueueDelayStrategy.Exponential(TimeSpan.FromSeconds(1), TimeSpan.Zero));
}
