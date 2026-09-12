namespace FiapGames.Data.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string Database { get; set; } = "fiapgames-catalog-history";
}
