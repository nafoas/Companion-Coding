using System.Text;

namespace CompanionCore.Api;

/// <summary>
/// Builds the bounded, deterministic Resume Packet for one request from local state
/// only: caller focus subjects, then the journal's most recent committed subjects, each
/// resolved through committed local memory with current understanding first.
/// </summary>
internal sealed class ResumePacketBuilder
{
    internal const int MaximumSubjects = 8;
    internal const int MaximumRecordsPerSubject = 6;
    internal const int MaximumRecords = 32;
    internal const int MaximumRecollectionBytes = 24 * 1024;
    internal const int MaximumSubjectCharacters = 512;

    private readonly ILocalMemoryReader _memory;

    internal ResumePacketBuilder(ILocalMemoryReader memory)
    {
        _memory = memory;
    }

    internal static IReadOnlyList<string> SelectSubjects(
        IReadOnlyList<string> focusSubjects,
        IReadOnlyList<string> recentSubjects)
    {
        var selected = new List<string>(MaximumSubjects);
        foreach (var subject in focusSubjects.Concat(recentSubjects))
        {
            if (selected.Count == MaximumSubjects)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(subject)
                && subject.Length <= MaximumSubjectCharacters
                && !selected.Contains(subject, StringComparer.Ordinal))
            {
                selected.Add(subject);
            }
        }

        return selected.AsReadOnly();
    }

    internal async Task<ResumePacket> BuildAsync(
        ResumeContext context,
        IReadOnlyList<string> recentSubjects,
        string? sessionReference,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var subjects = SelectSubjects(context.FocusSubjects ?? [], recentSubjects);
        var items = new List<ResumePacketItem>();
        var recollectionBytes = 0;
        long basis = 0;
        var truncated = false;
        foreach (var subject in subjects)
        {
            var retrieved = await _memory.RetrieveBySubjectAsync(subject, cancellationToken).ConfigureAwait(false);
            var taken = 0;
            foreach (var memory in retrieved)
            {
                if (taken == MaximumRecordsPerSubject || items.Count == MaximumRecords)
                {
                    truncated = true;
                    break;
                }

                var size = Encoding.UTF8.GetByteCount(memory.Record.VisibleRecollection);
                if (recollectionBytes + size > MaximumRecollectionBytes)
                {
                    truncated = true;
                    break;
                }

                recollectionBytes += size;
                basis = Math.Max(basis, memory.JournalSequence);
                taken++;
                items.Add(new ResumePacketItem(
                    memory.Record.RecordId,
                    memory.Record.SubjectKey,
                    memory.Record.Scope,
                    memory.Record.SourceKind,
                    memory.Record.Confidence,
                    memory.IsCurrent,
                    memory.Record.CreatedAtUtc,
                    memory.Record.VisibleRecollection));
            }
        }

        return new ResumePacket(
            Guid.CreateVersion7(nowUtc),
            nowUtc,
            sessionReference,
            context.ApplicationReference,
            subjects,
            items.AsReadOnly(),
            basis,
            truncated);
    }
}
