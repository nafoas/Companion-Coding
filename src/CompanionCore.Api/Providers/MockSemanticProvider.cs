using System.Security.Cryptography;
using System.Text.Json;

namespace CompanionCore.Api;

/// <summary>What the mock saw: the structured request text and an image digest, never image bytes.</summary>
public sealed record RecordedSemanticRequest(
    Guid OperationId,
    int Attempt,
    string RequestJson,
    int ImageLength,
    string ImageSha256);

/// <summary>
/// Deterministic scripted provider. Each call consumes the next scripted step; with no
/// step left it returns one neutral interpretation of the full-context region and no
/// memory proposals. It never touches a network, credential, or clock.
/// </summary>
public sealed class MockSemanticProvider : ISemanticProvider
{
    public delegate Task<ProviderReply> MockStep(SemanticRequest request, CancellationToken cancellationToken);

    private readonly object _gate = new();
    private readonly Queue<MockStep> _script = new();
    private readonly List<RecordedSemanticRequest> _requests = [];

    public MockSemanticProvider(IEnumerable<MockStep>? script = null)
    {
        foreach (var step in script ?? [])
        {
            Enqueue(step);
        }
    }

    public string ProviderName => "mock";

    public int CallCount
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count;
            }
        }
    }

    public IReadOnlyList<RecordedSemanticRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public void Enqueue(MockStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        lock (_gate)
        {
            _script.Enqueue(step);
        }
    }

    public static MockStep Reply(ProviderReply reply) =>
        (_, _) => Task.FromResult(reply);

    public static MockStep Respond(Func<SemanticRequest, string> responseJson) =>
        (request, _) => Task.FromResult(ProviderReply.Success(responseJson(request)));

    public static string DefaultResponseJson(SemanticRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(
            new
            {
                schemaVersion = SemanticSchema.Version,
                operationId = request.OperationId,
                interpretation = new
                {
                    summary = "Synthetic interpretation.",
                    observations = new[]
                    {
                        new { region = "fullContext", label = "synthetic", confidence = 0.5 },
                    },
                },
            });
    }

    public Task<ProviderReply> InterpretAsync(SemanticRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MockStep? step;
        lock (_gate)
        {
            _requests.Add(new RecordedSemanticRequest(
                request.OperationId,
                request.Attempt,
                request.RequestJson,
                request.EncodedImage.Length,
                Convert.ToHexStringLower(SHA256.HashData(request.EncodedImage.Span))));
            _script.TryDequeue(out step);
        }

        return step is null
            ? Task.FromResult(ProviderReply.Success(DefaultResponseJson(request)))
            : step(request, cancellationToken);
    }
}
