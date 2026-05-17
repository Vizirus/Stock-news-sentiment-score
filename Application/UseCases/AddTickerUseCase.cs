using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class AddTickerUseCase
{
    private readonly IAppDBContext _dbContext;

    public AddTickerUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExecuteAsync(string symbol, string companyName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(companyName))
            return false;

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        // Check if already exists
        var exists = await _dbContext.Ticker
            .AnyAsync(t => t.Symbol == normalizedSymbol, ct);

        if (exists)
            return false;

        var ticker = new Ticker
        {
            Symbol = normalizedSymbol,
            CompanyName = companyName.Trim()
        };

        _dbContext.Ticker.Add(ticker);
        await _dbContext.SaveChangesAsync(ct);

        return true;
    }
}
