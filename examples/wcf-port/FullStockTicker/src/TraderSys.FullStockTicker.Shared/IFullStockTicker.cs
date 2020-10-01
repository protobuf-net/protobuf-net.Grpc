using System.Collections.Generic;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace TraderSys.FullStockTicker.Shared
{
    [ServiceContract]
    public interface IFullStockTicker
    {
        [OperationContract]
        IAsyncEnumerable<StockTickerUpdate> Subscribe(IAsyncEnumerable<SymbolRequest> request, CallContext context = default);
    }
}
