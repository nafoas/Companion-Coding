using System.Reflection;
using System.Runtime.CompilerServices;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class CaptureIsolationTests
{
    private static readonly string[] ForbiddenCapabilityNames =
    [
        "EnumWindows",
        "GetForegroundWindow",
        "BitBlt",
        "PrintWindow",
        "CreateForMonitor",
        "DesktopDuplication",
        "DisplayName",
        "MemoryStore",
        "CompanionRuntime",
    ];

    [Fact]
    public void WorkerAssembly_ReferencesNoRuntimeMemoryTargetDiscoveryOrPresentationAuthority()
    {
        var references = typeof(CaptureWorkerEngine).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name =>
            name.Contains("CompanionCore.Runtime", StringComparison.Ordinal)
            || name.Contains("CompanionCore.Memory", StringComparison.Ordinal)
            || name.Contains("CompanionCore.TargetAuth", StringComparison.Ordinal)
            || name.Contains("CompanionCore.Presentation", StringComparison.Ordinal));
        Assert.Contains("CompanionCore.Capture.Contracts", references);
    }

    [Fact]
    public void WorkerAssembly_ContainsNoFallbackEnumerationMonitorOrDurableImageCapability()
    {
        var memberNames = typeof(CaptureWorkerEngine).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMembers(
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.Static))
            .Select(member => member.Name)
            .ToArray();

        foreach (var forbidden in ForbiddenCapabilityNames)
        {
            Assert.DoesNotContain(memberNames, name =>
                name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ClientConstruction_DoesNotLaunchWorker()
    {
        using var worker = new OutOfProcessCaptureWorker();

        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
        Assert.Equal(0, worker.WorkerProcessId);
    }

    [Fact]
    public void WorkerAndClientAssemblies_CannotIssueSealedAuthorizationGrants()
    {
        var contracts = typeof(CaptureAuthorizationGrant).Assembly;
        var friendAssemblies = contracts
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();
        var issuer = typeof(CaptureAuthorizationGrant).GetMethod(
            "Issue",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(issuer);
        Assert.True(issuer.IsAssembly);
        Assert.DoesNotContain("CompanionCore.Capture.Client", friendAssemblies);
        Assert.DoesNotContain("CompanionCore.Capture.Worker", friendAssemblies);
    }
}
