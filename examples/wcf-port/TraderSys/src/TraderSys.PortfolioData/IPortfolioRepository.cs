using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TraderSys.Portfolios.Models;

namespace TraderSys.PortfolioData
{
    public interface IPortfolioRepository
    {
        Task<Portfolio> GetAsync(Guid traderId, int portfolioId);
        Task<List<Portfolio>> GetAllAsync(Guid traderId);
    }
}