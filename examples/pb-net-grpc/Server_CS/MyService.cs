using Microsoft.Extensions.Logging;
using Shared_CS;
using System.Threading.Tasks;

namespace Server_CS
{
    public class MyService : ICalculator
    {
        private readonly ILogger<MyService> _logger;
        public MyService(ILogger<MyService> logger) => _logger = logger;
        ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
            => new ValueTask<MultiplyResult>(
                new MultiplyResult { Result = request.X * request.Y }
            );

        ValueTask ICalculator.Nil()
        {
            _logger.Log(LogLevel.Information, "Beep!");
            return default;
        }
    }
}
