namespace CompanionCore.TargetAuth;

internal sealed class AuthorizationPolicyLocation
{
    private const string PolicyFileName = "target-authorization-v1.json";

    private AuthorizationPolicyLocation(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        PolicyPath = Path.Combine(RootPath, PolicyFileName);
    }

    internal string RootPath { get; }

    internal string PolicyPath { get; }

    internal static AuthorizationPolicyLocation CreateDevelopment()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The fixed development settings root is unavailable.");
        }

        return new AuthorizationPolicyLocation(
            Path.Combine(localApplicationData, "CompanionCore.Dev", "TargetAuthorization"));
    }

    internal static AuthorizationPolicyLocation CreateTest(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return new AuthorizationPolicyLocation(rootPath);
    }
}
