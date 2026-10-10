using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanionCore.Api;

/// <summary>Version and strict serializer settings shared by every provider.</summary>
public static class SemanticSchema
{
    public const int Version = 1;

    /// <summary>The only remote memory operation the local allowlist recognizes.</summary>
    public const string AppendOperationName = "memory.append.v1";

    internal const int MaximumResponseBytes = 64 * 1024;
    internal const int MaximumSummaryCharacters = 2000;
    internal const int MaximumObservations = 16;
    internal const int MaximumLabelCharacters = 200;
    internal const int MaximumProposals = 8;
    internal const long MaximumUsageUnits = 1_000_000_000;

    internal static readonly JsonSerializerOptions StrictOptions = CreateStrictOptions();

    private static JsonSerializerOptions CreateStrictOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 16,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
