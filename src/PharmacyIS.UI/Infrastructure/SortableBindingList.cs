using System.ComponentModel;

namespace PharmacyIS.UI.Infrastructure;

/// <summary>
/// Списък с поддръжка на подреждане по колона.
///
/// Таблицата подрежда редовете само когато източникът на данни съобщава,
/// че поддържа подреждане. При обикновен списък заглавията на колоните
/// изглеждат активни, но щракването върху тях не предизвиква нищо. Затова
/// данните за всички таблици се свързват през този клас.
/// </summary>
public class SortableBindingList<T> : BindingList<T>
{
    private bool _isSorted;
    private PropertyDescriptor? _sortProperty;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    public SortableBindingList(IEnumerable<T> items) : base(items.ToList())
    {
    }

    protected override bool SupportsSortingCore => true;

    protected override bool IsSortedCore => _isSorted;

    protected override PropertyDescriptor? SortPropertyCore => _sortProperty;

    protected override ListSortDirection SortDirectionCore => _sortDirection;

    protected override void ApplySortCore(PropertyDescriptor property, ListSortDirection direction)
    {
        if (Items is not List<T> items) return;

        items.Sort((left, right) =>
        {
            var result = Compare(property.GetValue(left), property.GetValue(right));
            return direction == ListSortDirection.Ascending ? result : -result;
        });

        _sortProperty = property;
        _sortDirection = direction;
        _isSorted = true;

        ResetBindings();
    }

    protected override void RemoveSortCore()
    {
        _isSorted = false;
        _sortProperty = null;
    }

    /// <summary>
    /// Сравнява две стойности от една колона. Празните стойности се нареждат
    /// най-отпред, а текстът се сравнява по правилата на текущата езикова
    /// среда, за да се подреди кирилицата правилно.
    /// </summary>
    private static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        if (left is string leftText && right is string rightText)
            return string.Compare(leftText, rightText, StringComparison.CurrentCulture);

        return left is IComparable comparable
            ? comparable.CompareTo(right)
            : string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCulture);
    }
}
