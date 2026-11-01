namespace FiapGames.Catalog.Search;

public sealed class SearchIndexState
{
    private volatile bool ready;
    public bool IsReady => ready;
    public void SetReady(bool value) => ready = value;
}
