using FiapGames.Core.Dtos;
using FiapGames.Core.Models;
using FiapGames.Core.Services;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;

namespace FiapGames.Data.Search;

public sealed class OpenSearchGameIndex(IOpenSearchClient client, IOptions<OpenSearchOptions> options) : IGameSearchIndex
{
    private string IndexName => options.Value.IndexName;

    public async Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        var exists = await client.Indices.ExistsAsync(IndexName, ct: cancellationToken);
        if (exists.Exists) return false;
        if (exists.ApiCall?.HttpStatusCode != 404) EnsureSuccess(exists);

        var created = await client.Indices.CreateAsync(IndexName + "-000001", c => c
            .Aliases(a => a.Alias(IndexName))
            .Settings(s => s.NumberOfShards(1).NumberOfReplicas(options.Value.NumberOfReplicas)
                .Analysis(a => a.Analyzers(an => an.Custom("game_text", ca => ca
                    .Tokenizer("standard").Filters("lowercase", "asciifolding")))))
            .Map<GameDocument>(m => m.Dynamic(false).Properties(p => p
                .Keyword(k => k.Name(d => d.Id))
                .Text(t => t.Name(d => d.Title).Analyzer("game_text"))
                .Text(t => t.Name(d => d.Description).Analyzer("game_text"))
                .Text(t => t.Name(d => d.Genre).Analyzer("game_text"))
                .Number(n => n.Name(d => d.Price).Type(NumberType.ScaledFloat).ScalingFactor(100))
                .Boolean(b => b.Name(d => d.Active))
                .Date(d => d.Name(g => g.CreatedAt)))), cancellationToken);

        // Another replica can create the index between HEAD and PUT.
        if (!created.IsValid && created.ServerError?.Error.Type != "resource_already_exists_exception")
            EnsureSuccess(created);
        if (!created.IsValid)
        {
            var alias = await client.Indices.PutAliasAsync(IndexName + "-000001", IndexName, ct: cancellationToken);
            EnsureSuccess(alias);
        }
        return true;
    }

    public async Task IndexAsync(Game game, CancellationToken cancellationToken)
    {
        var result = await client.IndexAsync(GameDocument.From(game), i => i.Index(IndexName)
            .Id(game.Id).Version(Math.Max(1, game.SearchVersion)).VersionType(VersionType.ExternalGte)
            .RequireAlias(true).Refresh(Refresh.WaitFor), cancellationToken);
        // A newer write already won. It is safe to acknowledge the older message.
        if (result.ApiCall?.HttpStatusCode != 409) EnsureSuccess(result);
    }

    public async Task IndexBatchAsync(IReadOnlyCollection<Game> games, CancellationToken cancellationToken)
    {
        if (games.Count == 0) return;
        var bulk = new BulkDescriptor().Index(IndexName).RequireAlias(true).Refresh(Refresh.WaitFor);
        foreach (var game in games)
            bulk.Index<GameDocument>(i => i.Id(game.Id).Document(GameDocument.From(game))
                .Version(Math.Max(1, game.SearchVersion)).VersionType(VersionType.ExternalGte));
        var result = await client.BulkAsync(bulk, cancellationToken);
        if (result.ApiCall?.HttpStatusCode is not >= 200 or >= 300 ||
            result.Items.Any(i => i.Error is not null && i.Status != 409))
            throw new SearchUnavailableException("OpenSearch bulk indexing failed.");
    }

    public async Task<GameSearchResult> SearchAsync(GameSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await client.SearchAsync<GameDocument>(s => s.Index(IndexName)
            .From((request.Page - 1) * request.PageSize).Size(request.PageSize).TrackTotalHits(true)
            .Query(q => q.Bool(b => b
                .Filter(f => f.Term(t => t.Field(g => g.Active).Value(true)))
                .Must(m => m.MultiMatch(mm => mm.Query(request.Q.Trim())
                    .Fields(f => f.Field(g => g.Title, 3).Field(g => g.Genre, 2).Field(g => g.Description))
                    .Fuzziness(Fuzziness.Auto)))))
            .Sort(sort => sort.Descending(SortSpecialField.Score).Ascending(g => g.Id)), cancellationToken);
        EnsureSuccess(result);
        return new GameSearchResult(result.Hits.Select(h => new GameSearchHit(h.Source.ToDto(), h.Score ?? 0)).ToList(),
            result.Total, request.Page, request.PageSize);
    }

    private static void EnsureSuccess(IResponse response)
    {
        if (!response.IsValid)
            throw new SearchUnavailableException(
                $"OpenSearch request failed ({response.ApiCall?.HttpStatusCode}): {response.ServerError?.Error.Type}.",
                response.OriginalException);
    }

    public sealed class GameDocument
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        public static GameDocument From(Game game) => new()
        {
            Id = game.Id, Title = game.Title, Description = game.Description, Genre = game.Genre,
            Price = game.Price, Active = game.Active, CreatedAt = game.CreatedAt
        };

        public GameDto ToDto() => new(Id, Title, Description, Price, Genre, Active, CreatedAt);
    }
}
