using ProtoBuf.Grpc;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace TraderSys.SimpleStockTickerServer.Shared
{
    [ServiceContract]
    public interface IStockTickerService
    {
        [OperationContract]
        IAsyncEnumerable<StockTickerUpdate> Subscribe(SubscribeRequest request, CallContext context = default);
    }
}
