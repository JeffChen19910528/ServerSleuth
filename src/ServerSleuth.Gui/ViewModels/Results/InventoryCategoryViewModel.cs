namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-6A: one row in the Discovery Inventory's category breakdown — the exact
/// <c>DiscoveryEntity.Type</c> string a scanner assigned, plus how many discovered entities
/// carry it. Never a hard-coded category: <see cref="InventoryExplorerViewModel"/> only ever
/// creates one of these per <c>Type</c> value it actually observed, so an entity type the
/// current scan didn't produce simply never appears here (no placeholder, no invented zero row).
/// </summary>
public sealed class InventoryCategoryViewModel
{
    public InventoryCategoryViewModel(string type, int count)
    {
        Type = type;
        Count = count;
    }

    public string Type { get; }

    public int Count { get; }
}
