using FiapGames.Core.Services;
using FiapGames.Data.Search;
using FiapGames.Services;
using Microsoft.Extensions.Options;

namespace FiapGames.Catalog.Search;

public sealed class SearchIndexWorker(IServiceScopeFactory scopes, SearchIndexState state,
    IOptions<OpenSearchOptions> options, ILogger<SearchIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapped = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var index = scope.ServiceProvider.GetRequiredService<IGameSearchIndex>();
                var synchronizer = scope.ServiceProvider.GetRequiredService<SearchIndexSynchronizer>();
                var created = await index.EnsureCreatedAsync(stoppingToken);
                if (created || !bootstrapped)
                {
                    state.SetReady(false);
                    await synchronizer.RebuildAsync(stoppingToken);
                    bootstrapped = true;
                }
                else await synchronizer.DrainAsync(stoppingToken);
                state.SetReady(true);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                state.SetReady(false);
                logger.LogWarning(ex, "Search synchronization failed; retrying in {Seconds}s", options.Value.RetryIntervalSeconds);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
