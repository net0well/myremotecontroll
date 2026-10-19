using System.Diagnostics;

namespace MyRemoteControll.Agent.IntegrationTests.Support;

internal static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed > limit)
            {
                Assert.Fail($"Tempo esgotado esperando: {description}");
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }
}
