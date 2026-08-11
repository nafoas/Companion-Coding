using System.Reflection;

namespace CompanionCore.App.IntegrationTests;

public sealed class CaptureCompositionTests
{
    [Fact]
    public void NormalApplicationReferencesOutOfProcessClientAndNotSyntheticFake()
    {
        var references = typeof(App).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("CompanionCore.Capture.Client", references);
        Assert.DoesNotContain("CompanionCore.Capture.Fake", references);
    }
}
