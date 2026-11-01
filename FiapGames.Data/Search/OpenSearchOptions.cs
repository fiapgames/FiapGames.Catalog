namespace FiapGames.Data.Search;

public sealed class OpenSearchOptions
{
    public const string SectionName = "OpenSearch";
    public string Url { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "games-v1";
    public int NumberOfReplicas { get; set; }
    public int RetryIntervalSeconds { get; set; } = 5;
    public bool RebuildOnly { get; set; }
}
