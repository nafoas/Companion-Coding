namespace CompanionCore.App.IntegrationTests;

/// <summary>
/// Every test class that launches the real App joins this collection, so launches never run
/// in parallel and race each other for the single-instance guard.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppProcessCollection
{
    public const string Name = "Real App process";
}
