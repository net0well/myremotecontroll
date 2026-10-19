using MyRemoteControll.Agent.Hosting;

namespace MyRemoteControll.Agent.Tests.Hosting;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public async Task AC2_SecondInstance_IsRejectedAndSignalsFirstInstance()
    {
        var name = $@"Local\MyRemoteControll.Test.{Guid.NewGuid():N}";
        using var first = SingleInstanceGuard.TryAcquire(name);
        Assert.NotNull(first);

        var signaled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.OnSignal(() => signaled.TrySetResult());

        using var second = SingleInstanceGuard.TryAcquire(name);
        Assert.Null(second);

        SingleInstanceGuard.SignalExisting(name);
        await signaled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AfterFirstInstanceExits_ANewInstanceCanStart()
    {
        var name = $@"Local\MyRemoteControll.Test.{Guid.NewGuid():N}";
        SingleInstanceGuard.TryAcquire(name)!.Dispose();

        using var next = SingleInstanceGuard.TryAcquire(name);

        Assert.NotNull(next);
    }
}
