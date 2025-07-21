using System.Security.Cryptography;

namespace WorkR.TestApp
{
    public class RandomNumberTrigger : ITrigger<int>
    {
        public async Task Execute(Func<int, CancellationToken, Task> next, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await next(RandomNumberGenerator.GetInt32(100), ct);
                await Task.Delay(RandomNumberGenerator.GetInt32(1000, 10000), ct);
            }
        }
    }
}
