using Umbra.Core;

namespace Umbra.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task SecondaryInstanceSignalsPrimaryInstance()
    {
        var identity = $"UmbraNative.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(identity);
        Assert.True(primary.IsPrimary);

        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.Listen(() => activated.TrySetResult());

        using var secondary = new SingleInstanceCoordinator(identity);
        Assert.False(secondary.IsPrimary);

        secondary.SignalPrimary();
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void NewInstanceBecomesPrimaryAfterAllPreviousHandlesClose()
    {
        var identity = $"UmbraNative.Tests.{Guid.NewGuid():N}";

        using (var primary = new SingleInstanceCoordinator(identity))
        using (var secondary = new SingleInstanceCoordinator(identity))
        {
            Assert.True(primary.IsPrimary);
            Assert.False(secondary.IsPrimary);
        }

        using var replacement = new SingleInstanceCoordinator(identity);
        Assert.True(replacement.IsPrimary);
    }
}
