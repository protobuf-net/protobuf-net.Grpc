using Shared_CS;
using System.Threading.Tasks;

namespace Server_CS
{
    public class MyService : ICalculator
    {
        ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
            => new ValueTask<MultiplyResult>(
                new MultiplyResult { Result = request.X * request.Y }
            );
    }
}
