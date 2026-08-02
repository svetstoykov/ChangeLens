namespace ChangeLens.Core.Snapshots.Constants;

/// <summary>
///     Provides the stable identifiers that form the snapshot manifest hash preimage.
/// </summary>
public static class SnapshotManifestConstants
{
    /// <summary>The manifest schema version marker that opens every manifest-hash preimage.</summary>
    public const string ManifestVersionMarker = "snapshot.manifest.v1";

    /// <summary>The stable lowercase literal for an added entry.</summary>
    public const string AddedCategory = "added";

    /// <summary>The stable lowercase literal for a modified entry.</summary>
    public const string ModifiedCategory = "modified";

    /// <summary>The stable lowercase literal for a deleted entry.</summary>
    public const string DeletedCategory = "deleted";

    /// <summary>The stable lowercase literal for a renamed entry.</summary>
    public const string RenamedCategory = "renamed";

    /// <summary>The stable lowercase literal for a type-changed entry.</summary>
    public const string TypeChangedCategory = "typeChanged";
}
