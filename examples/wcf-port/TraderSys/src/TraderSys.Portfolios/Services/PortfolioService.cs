using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using TraderSys.PortfolioData;
using TraderSys.Portfolios.Models;

namespace TraderSys.Portfolios.Services
{
    public class PortfolioService : IPortfolioService
    {
        private readonly IPortfolioRepository _repository;

        public PortfolioService(IPortfolioRepository repository)
        {
            _repository = repository;
        }

        public async Task<Portfolio> Get(GetPortfolioRequest request)
        {
            var portfolio = await _repository.GetAsync(request.TraderId, request.PortfolioId);

            return portfolio;
        }

        public async Task<PortfolioCollection> GetAll(GetAllPortfoliosRequest request)
        {
            var portfolios = await _repository.GetAllAsync(request.TraderId);

            var response = new PortfolioCollection
            {
                Items = portfolios
            };

            return response;
        }
    }
}
