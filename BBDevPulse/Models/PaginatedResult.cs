namespace BBDevPulse.Models;

/// <summary>
/// Pagination result with items and the next page URI.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    Uri? NextPage)
{
    /// <summary>Snapshot of items in response order. A missing values array represents an empty page.</summary>
    public IReadOnlyList<T> Items { get; } = Array.AsReadOnly(
        (Items ?? []).ToArray());

    /// <inheritdoc />
    public bool Equals(PaginatedResult<T>? other) =>
        other is not null && NextPage == other.NextPage && Items.SequenceEqual(other.Items);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NextPage);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
