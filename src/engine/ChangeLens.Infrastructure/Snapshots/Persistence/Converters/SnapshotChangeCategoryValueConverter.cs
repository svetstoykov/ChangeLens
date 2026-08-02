using System.Collections.Frozen;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChangeLens.Infrastructure.Snapshots.Persistence.Converters;

/// <summary>
///     Converts <see cref="SnapshotChangeCategory" /> to and from the stable literal enforced by the
///     <c>CK_snapshot_manifest_entries_category</c> check constraint.
/// </summary>
internal sealed class SnapshotChangeCategoryValueConverter() : ValueConverter<SnapshotChangeCategory, string>(
    category => ToLiteral(category),
    literal => FromLiteral(literal))
{
    private static readonly FrozenDictionary<SnapshotChangeCategory, string> LiteralsByCategory =
        new Dictionary<SnapshotChangeCategory, string>
        {
            [SnapshotChangeCategory.Added] = SnapshotManifestConstants.AddedCategory,
            [SnapshotChangeCategory.Modified] = SnapshotManifestConstants.ModifiedCategory,
            [SnapshotChangeCategory.Deleted] = SnapshotManifestConstants.DeletedCategory,
            [SnapshotChangeCategory.Renamed] = SnapshotManifestConstants.RenamedCategory,
            [SnapshotChangeCategory.TypeChanged] = SnapshotManifestConstants.TypeChangedCategory,
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, SnapshotChangeCategory> CategoriesByLiteral =
        LiteralsByCategory.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    private static string ToLiteral(SnapshotChangeCategory category) => LiteralsByCategory[category];

    private static SnapshotChangeCategory FromLiteral(string literal) => CategoriesByLiteral[literal];
}
