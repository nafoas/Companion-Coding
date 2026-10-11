using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanionCore.Braincase;

public enum BuilderBraincaseIssue
{
    None = 0,
    Unreadable = 1,
    Invalid = 2,
    UnknownModel = 3,
}

/// <summary>
/// Builder Prince's Braincase settings, one small JSON file in his own Builder data root
/// (<see cref="FileName"/>). It never holds a key: sign-in comes from <c>ant auth login</c> or
/// ANTHROPIC_API_KEY. Live calls are off until <see cref="LiveCalls"/> is set, and a damaged or
/// unknown setting always resolves to off.
/// </summary>
public sealed record BuilderBraincaseSettings
{
    public const string FileName = "braincase-settings.json";
    public const decimal DefaultDollarsPerDay = 5m;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public bool LiveCalls { get; init; }

    public string Model { get; init; } = AnthropicProviderOptions.DefaultModel;

    public string Effort { get; init; } = "low";

    /// <summary>The Glance model (VIS-02), Prince's quick peripheral looks; "" turns the Glance off.</summary>
    public string GlanceModel { get; init; } = AnthropicProviderOptions.DefaultGlanceModel;

    /// <summary>The Bun Budget: Builder Prince sleeps for the rest of the (UTC) day when it is spent.</summary>
    public decimal DollarsPerDay { get; init; } = DefaultDollarsPerDay;

    public static BuilderBraincaseSettings Off { get; } = new();

    /// <summary>
    /// Reads the settings from <paramref name="rootPath"/>, writing the default (live calls off)
    /// file when none exists so it is easy to find and edit.
    /// </summary>
    public static (BuilderBraincaseSettings Settings, BuilderBraincaseIssue Issue) LoadOrCreate(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var path = Path.Combine(rootPath, FileName);
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(path, JsonSerializer.Serialize(Off, Json));
                return (Off, BuilderBraincaseIssue.None);
            }

            var settings = JsonSerializer.Deserialize<BuilderBraincaseSettings>(File.ReadAllText(path), Json);
            if (settings is null
                || AnthropicProviderOptions.ParseEffort(settings.Effort) is null
                || settings.DollarsPerDay is <= 0 or > 1000
                || string.IsNullOrWhiteSpace(settings.Model)
                || settings.GlanceModel is null)
            {
                return (Off, BuilderBraincaseIssue.Invalid);
            }

            return AnthropicPricing.IsKnown(settings.Model)
                && (settings.GlanceModel.Length == 0 || AnthropicPricing.IsKnown(settings.GlanceModel))
                ? (settings, BuilderBraincaseIssue.None)
                : (Off, BuilderBraincaseIssue.UnknownModel);
        }
        catch (JsonException)
        {
            return (Off, BuilderBraincaseIssue.Invalid);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (Off, BuilderBraincaseIssue.Unreadable);
        }
    }

    /// <summary>Writes these settings (used by the first-run check's <c>--turn-on</c>).</summary>
    public void Save(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        Directory.CreateDirectory(rootPath);
        var path = Path.Combine(rootPath, FileName);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    public AnthropicProviderOptions ToProviderOptions() => new()
    {
        LiveCallsEnabled = LiveCalls,
        AllowAmbientCredentials = true,
        Model = Model,
        Effort = Effort,
        GlanceModel = GlanceModel.Length == 0 ? null : GlanceModel,
    };

    public bool GlanceEnabled => GlanceModel.Length > 0;
}
