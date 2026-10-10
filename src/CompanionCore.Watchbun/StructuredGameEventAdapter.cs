using System.Text;
using System.Text.Json;

namespace CompanionCore.Watchbun;

public enum AdapterVerdict
{
    Accepted = 1,
    Oversized = 2,
    Malformed = 3,
    UnknownField = 4,
    InvalidValue = 5,
    WrongTarget = 6,
}

/// <summary>
/// Synthetic structured-event adapter for one authorized target session. It proves the
/// integration interface without a game mod or process injection: one strict, bounded JSON
/// object per line, <c>{"session":"…","kind":"change|meaningful|urgent","key":"…"}</c>.
/// </summary>
public sealed class StructuredGameEventAdapter
{
    public const int MaximumLineBytes = 1024;

    private readonly Guid _targetSessionId;

    public StructuredGameEventAdapter(Guid targetSessionId)
    {
        if (targetSessionId == Guid.Empty)
        {
            throw new ArgumentException("A target session is required.", nameof(targetSessionId));
        }

        _targetSessionId = targetSessionId;
    }

    public AdapterVerdict Parse(string? line, out StructuredGameEvent? gameEvent)
    {
        gameEvent = null;
        if (line is null || Encoding.UTF8.GetByteCount(line) > MaximumLineBytes)
        {
            return line is null ? AdapterVerdict.Malformed : AdapterVerdict.Oversized;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 2 });
        }
        catch (JsonException)
        {
            return AdapterVerdict.Malformed;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return AdapterVerdict.Malformed;
            }

            string? session = null, kind = null, key = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return property.Name is "session" or "kind" or "key" ? AdapterVerdict.InvalidValue : AdapterVerdict.UnknownField;
                }

                switch (property.Name)
                {
                    case "session" when session is null:
                        session = property.Value.GetString();
                        break;
                    case "kind" when kind is null:
                        kind = property.Value.GetString();
                        break;
                    case "key" when key is null:
                        key = property.Value.GetString();
                        break;
                    case "session" or "kind" or "key":
                        return AdapterVerdict.Malformed;
                    default:
                        return AdapterVerdict.UnknownField;
                }
            }

            if (session is null || kind is null || key is null)
            {
                return AdapterVerdict.Malformed;
            }

            if (!Guid.TryParseExact(session, "D", out var sessionId)
                || !WatchbunEngine.ValidEventKey(key)
                || ParseKind(kind) is not { } eventKind)
            {
                return AdapterVerdict.InvalidValue;
            }

            if (sessionId != _targetSessionId)
            {
                return AdapterVerdict.WrongTarget;
            }

            gameEvent = new StructuredGameEvent(sessionId, eventKind, key);
            return AdapterVerdict.Accepted;
        }
    }

    private static GameEventKind? ParseKind(string kind) => kind switch
    {
        "change" => GameEventKind.Change,
        "meaningful" => GameEventKind.Meaningful,
        "urgent" => GameEventKind.Urgent,
        _ => null,
    };
}
