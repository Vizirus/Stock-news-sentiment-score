using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class GetSummariesUseCase
{
    private readonly IAppDBContext _dbContext;

    public GetSummariesUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TickerDailySummary>> ExecuteAsync(string tickerSymbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1); // Include end date

        var query = _dbContext.TickerDailySummaries
            .AsNoTracking()
            .Include(s => s.Ticker)
            .Where(s => s.SummaryDate >= start && s.SummaryDate < end);

        if (!string.IsNullOrEmpty(tickerSymbol) && tickerSymbol != "All Tickers")
        {
            query = query.Where(s => s.Ticker != null && s.Ticker.Symbol == tickerSymbol);
        }

        return await query
            .OrderByDescending(s => s.SummaryDate)
            .ThenBy(s => s.Ticker != null ? s.Ticker.Symbol : string.Empty)
            .ToListAsync(cancellationToken);
    }
}
