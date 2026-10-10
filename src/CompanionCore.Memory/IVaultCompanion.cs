namespace CompanionCore.Memory;

/// <summary>
/// The Da Bun Vault composition point's companion archive (photographs and state). It builds
/// and independently validates its candidate before either archive is promoted, so a failing
/// companion fails the whole backup and the previous Vault stays untouched.
/// </summary>
internal interface IVaultCompanion
{
    Task BuildCandidateAsync(
        Guid backupId,
        DateTimeOffset createdAtUtc,
        string candidatePath,
        CancellationToken cancellationToken);
}
