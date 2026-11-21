using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class DeleteTickerUseCase
{
    private readonly IAppDBContext _dbContext;

    public DeleteTickerUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExecuteAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        var ticker = await _dbContext.Ticker
            .FirstOrDefaultAsync(t => t.Symbol == normalizedSymbol, ct);

        if (ticker == null)
            return false;

        _dbContext.Ticker.Remove(ticker);
        await _dbContext.SaveChangesAsync(ct);

        return true;
    }
}
