namespace CompanionCore.Memory;

internal static class MemoryPathGuard
{
    internal static string RequireImmediateChild(string expectedParent, string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedParent);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);

        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedParent));
        var candidate = Path.GetFullPath(candidatePath);
        var candidateParent = Path.GetDirectoryName(candidate)
            ?? throw new DataRootViolationException("The task-owned path has no parent directory.");
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(
                parent,
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidateParent)),
                comparison))
        {
            throw new DataRootViolationException(
                "Backup and repair files must remain immediate children of their fixed non-production root.");
        }

        return candidate;
    }

    internal static void TryDeleteTaskOwnedDirectory(
        string fixedParent,
        string candidateDirectory)
    {
        var candidate = RequireImmediateChild(fixedParent, candidateDirectory);
        try
        {
            if (Directory.Exists(candidate))
            {
                Directory.Delete(candidate, recursive: true);
            }
        }
        catch (IOException)
        {
            // A uniquely named staging orphan is non-authoritative and can be
            // retried or cleaned later without affecting committed data.
        }
        catch (UnauthorizedAccessException)
        {
            // Same safe orphan outcome as the IOException case above.
        }
    }

    internal static void TryDeleteTaskOwnedFile(string fixedParent, string candidatePath)
    {
        var candidate = RequireImmediateChild(fixedParent, candidatePath);
        try
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
        catch (IOException)
        {
            // A task-owned candidate cannot become authoritative by its unique name.
        }
        catch (UnauthorizedAccessException)
        {
            // Same safe orphan outcome as the IOException case above.
        }
    }
}
