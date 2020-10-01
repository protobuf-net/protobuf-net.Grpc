using System;
using System.ServiceModel;
using System.Threading.Tasks;
using TraderSys.Portfolios.Models;

namespace TraderSys.Portfolios
{
    [ServiceContract]
    public interface IPortfolioService
    {
        [OperationContract]
        Task<Portfolio> Get(GetPortfolioRequest request);

        [OperationContract]
        Task<PortfolioCollection> GetAll(GetAllPortfoliosRequest request);
    }
}
