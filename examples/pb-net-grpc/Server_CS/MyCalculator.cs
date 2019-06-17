using Microsoft.Extensions.Logging;
using Shared_CS;
using System.Threading.Tasks;

namespace Server_CS
{
    public class MyCalculator : ICalculator
    {
        ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
            => new ValueTask<MultiplyResult>(new MultiplyResult { Result = request.X * request.Y });
    }
}
