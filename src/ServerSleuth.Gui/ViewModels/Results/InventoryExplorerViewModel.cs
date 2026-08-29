using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-6A: presentation/query layer over the raw entities an already-completed scan discovered —
/// "what did this server actually have on it," as opposed to the Risk/Migration summaries the
/// rest of the dashboard already shows. Built exactly once per completed
/// <see cref="ScanPipelineResult"/> (mirrors <see cref="ResultsDashboardViewModel"/>'s own
/// "never reconstructs, never re-touches the pipeline" contract) from data the pipeline already
/// produced — <see cref="ScanPipelineResult.Discovery"/> (Phase Discovery's own
/// <c>AggregateDiscoveryResult</c>), <see cref="ScanPipelineResult.ExternalDependencies"/>
/// (Phase 5C), and <see cref="ScanPipelineResult.Boundaries"/> (Phase 5B) — never a new
/// DiscoveryEngine/ApplicationBoundaryEngine/DependencyExpansionEngine call of its own.
///
/// Performance (skill.md GUI-6A §12): every index below (entity-by-id, boundary membership,
/// category counts) is built exactly once in the constructor as a Dictionary/lookup over the
/// entity/boundary lists — O(N + M), never an O(N×M) nested scan over a 34,000+ entity result.
///
/// Determinism (skill.md GUI-6A §13): <see cref="Items"/> is sorted Type → Name → Id (all
/// ordinal), a fixed order that depends on none of Dictionary/HashSet enumeration order or DI
/// registration order — the same result every time for the same <see cref="ScanPipelineResult"/>.
///
/// GUI-7A: also implements <see cref="IPageViewModel"/> so the SAME instance/type can be shown
/// as a first-class standalone "Inventory" navigation page (<see cref="MainViewModel"/> builds
/// one directly, exactly like this constructor is already called from
/// <see cref="Results.ResultsDashboardViewModel"/>) — no second inventory engine, no wrapper
/// ViewModel, no duplicated entity-parsing logic.
/// </summary>
public sealed class InventoryExplorerViewModel : ObservableObject, IPageViewModel
{
    public InventoryExplorerViewModel(ScanPipelineResult? pipeline, ScanExecutionStatus status)
    {
        HasPartialCoverage = status == ScanExecutionStatus.Partial;

        var entities = pipeline is null
            ? []
            : pipeline.Discovery.Entities.Concat<DiscoveryEntity>(pipeline.ExternalDependencies).ToList();

        var boundaryNamesByEntityId = BuildBoundaryIndex(pipeline?.Boundaries ?? []);

        Items = entities
            .Select(e => new InventoryItemViewModel(e, boundaryNamesByEntityId.GetValueOrDefault(e.Id, [])))
            .OrderBy(i => i.Type, StringComparer.Ordinal)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        Categories = entities
            .GroupBy(e => e.Type, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new InventoryCategoryViewModel(g.Key, g.Count()))
            .ToList();

        _filteredItems = Items;
    }

    /// <summary>GUI-7A: only meaningful when this instance is shown as MainViewModel's standalone
    /// Inventory page — when embedded as <see cref="Results.ResultsDashboardViewModel.Inventory"/>
    /// it is simply unused, exactly like every other <see cref="IPageViewModel"/> would be if
    /// held as a plain property rather than shown via <c>CurrentPageViewModel</c>.</summary>
    public NavigationPage Page => NavigationPage.Inventory;

    /// <summary>GUI-6A §14: surfaced so the view can show "Some scanners were partially
    /// supported" without fabricating a fully-successful scan — the exact same
    /// <see cref="ScanExecutionStatus"/> the rest of the dashboard already reflects, never
    /// recomputed from scanner statuses here.</summary>
    public bool HasPartialCoverage { get; }

    /// <summary>Every discovered entity (Discovery's own entities plus Phase 5C's derived
    /// ExternalDependency entities), Type→Name→Id ordinal — the master list. Filtering never
    /// mutates this.</summary>
    public IReadOnlyList<InventoryItemViewModel> Items { get; }

    public int TotalCount => Items.Count;

    public bool HasNoInventory => Items.Count == 0;

    /// <summary>One row per distinct <c>DiscoveryEntity.Type</c> actually observed — never a
    /// hard-coded vocabulary, and never a zero-count placeholder for a type this scan didn't
    /// produce (skill.md GUI-6A §4, §6).</summary>
    public IReadOnlyList<InventoryCategoryViewModel> Categories { get; }

    /// <summary>The category ComboBox's own item source — <c>null</c> (bound to "All") plus
    /// every <see cref="Categories"/> Type string, in the same ordinal order.</summary>
    public IReadOnlyList<string?> CategoryFilterOptions => new string?[] { null }.Concat(Categories.Select(c => c.Type)).ToList();

    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    private IReadOnlyList<InventoryItemViewModel> _filteredItems;

    /// <summary>Presentation-only filtered/sorted view over <see cref="Items"/> — a fresh list
    /// every time a filter changes, never a reorder/removal from the master collection
    /// (skill.md GUI-6A §7).</summary>
    public IReadOnlyList<InventoryItemViewModel> FilteredItems
    {
        get => _filteredItems;
        private set => SetProperty(ref _filteredItems, value);
    }

    private void ApplyFilter()
    {
        IEnumerable<InventoryItemViewModel> query = Items;

        if (SelectedCategory is { } category)
        {
            query = query.Where(i => i.Type == category);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(i =>
                i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                i.Type.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (i.Path?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredItems = query.ToList();
    }

    private InventoryItemViewModel? _selectedItem;
    public InventoryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(SelectedDetail));
            }
        }
    }

    public InventoryDetailViewModel? SelectedDetail => SelectedItem?.Detail;

    /// <summary>Builds EntityId → (sorted, deduplicated) boundary Names exactly once, from
    /// <see cref="ApplicationBoundary.MemberEntityIds"/> — the same membership Phase 5B's
    /// <c>ApplicationBoundaryEngine</c> already computed. A shared entity legitimately belonging
    /// to multiple boundaries keeps every one of them (skill.md GUI-6A §10: never picks only the
    /// first), and the Name list itself is ordinal-sorted so display order never depends on
    /// <see cref="Dictionary{TKey,TValue}"/> enumeration order.</summary>
    private static Dictionary<string, List<string>> BuildBoundaryIndex(IReadOnlyList<ApplicationBoundary> boundaries)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var boundary in boundaries)
        {
            foreach (var entityId in boundary.MemberEntityIds)
            {
                if (!index.TryGetValue(entityId, out var names))
                {
                    names = [];
                    index[entityId] = names;
                }

                if (!names.Contains(boundary.Name, StringComparer.Ordinal))
                {
                    names.Add(boundary.Name);
                }
            }
        }

        foreach (var names in index.Values)
        {
            names.Sort(StringComparer.Ordinal);
        }

        return index;
    }
}
