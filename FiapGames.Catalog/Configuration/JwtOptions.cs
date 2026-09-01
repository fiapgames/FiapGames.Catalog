namespace FiapGames.Catalog.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "User.Games.Fiap";

    public string Audience { get; set; } = "Games.Fiap";

    public string SecretKey { get; set; } = string.Empty;
}
