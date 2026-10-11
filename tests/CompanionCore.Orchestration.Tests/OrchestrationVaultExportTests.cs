using System.IO.Compression;
using System.Security.Cryptography;
using CompanionCore.Api;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Vault;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>Single-file Da Bun Vault export (deferred KEEP-02 D1).</summary>
public sealed class OrchestrationVaultExportTests : IDisposable
{
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "CompanionCore.Orchestration.Tests.Exports", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_outside))
        {
            Directory.Delete(_outside, recursive: true);
        }
    }

    private static async Task<OrchestrationHarness> WithMemoryAndPhotographAsync()
    {
        var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "gem", "synthetic.gem", "[neutral memory] a gem.")));
        await harness.SheetAsync();
        await harness.Orchestrator.TakePhotographAsync();
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync(shade: 150);
        return harness;
    }

    [Fact]
    public async Task AnExport_IsOneVerifiedFile_HoldingTheCurrentArchivesUnchanged()
    {
        await using var harness = await WithMemoryAndPhotographAsync();
        var destination = Path.Combine(_outside, "Da Bun Vault.zip");

        var report = await harness.Host.ExportVaultAsync(destination);

        Assert.Equal(destination, report.Path);
        await CompanionHost.VerifyVaultExportAsync(destination);
        var bytes = await File.ReadAllBytesAsync(destination);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), report.Sha256);
        Assert.Equal(bytes.Length, report.Bytes);
        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(await File.ReadAllBytesAsync(harness.Location.BackupArchivePath), Read(archive, VaultExport.MemoryEntryName));
        Assert.Equal(await File.ReadAllBytesAsync(harness.Location.CompanionArchivePath), Read(archive, VaultExport.CompanionEntryName));
        Assert.True(harness.Has(CompanionNoticeKind.VaultBackedUp));
        Assert.Empty(Directory.EnumerateFiles(_outside, "*.tmp", SearchOption.AllDirectories));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task AnExport_RestoresThisPrince_AfterHisLocalVaultIsLost()
    {
        await using var harness = await WithMemoryAndPhotographAsync();
        var destination = Path.Combine(_outside, "vault.zip");
        await harness.Host.ExportVaultAsync(destination);
        var photograph = Assert.Single(await harness.Orchestrator.Keepsakes.ListAsync());

        // Lose the local Vault and damage the BunDex and the photograph; only the export survives.
        // (Prince is stopped first, so ending the target session makes no newer backup.)
        await harness.Host.DisposeAsync();
        await harness.Controller.EndSessionAsync();
        File.Delete(harness.Location.BackupArchivePath);
        File.Delete(harness.Location.CompanionArchivePath);
        await File.WriteAllBytesAsync(harness.Location.DatabasePath, "damaged"u8.ToArray());
        var photographFiles = Directory.GetFiles(harness.Host.Keepsakes.RootPath, "*.png");
        Assert.Single(photographFiles);
        File.Delete(photographFiles[0]);
        using (var archive = ZipFile.OpenRead(destination))
        {
            await File.WriteAllBytesAsync(harness.Location.BackupArchivePath, Read(archive, VaultExport.MemoryEntryName));
            await File.WriteAllBytesAsync(harness.Location.CompanionArchivePath, Read(archive, VaultExport.CompanionEntryName));
        }

        var restored = await CompanionHost.RepairOfflineAsync(new CompanionHostOptions(harness.Location, harness.Privacy, harness.Controller, harness.Provider, new InMemoryCredentialStore()));
        await harness.OpenHostAsync();

        Assert.Equal(CompanionStatus.Valid, restored.Companion);
        Assert.Equal(1, restored.PhotographsRestored);
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.gem" }));
        Assert.Equal(InspectionStatus.Verified, (await harness.Orchestrator.Keepsakes.InspectAsync(photograph.PhotographId)).Status);
    }

    [Fact]
    public async Task AnExportOlderThanTheLatestBackup_IsRefusedByRepairs_RatherThanDroppingLaterMemories()
    {
        await using var harness = await WithMemoryAndPhotographAsync();
        var destination = Path.Combine(_outside, "older.zip");
        await harness.Host.ExportVaultAsync(destination);

        // A later memory and a later backup: the export is now an older point in time.
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "later", "synthetic.later", "[neutral memory] a later moment.")));
        harness.Time.Advance(TimeSpan.FromSeconds(2));
        await harness.SheetAsync(shade: 170);
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));
        await harness.Host.DisposeAsync();
        using (var archive = ZipFile.OpenRead(destination))
        {
            await File.WriteAllBytesAsync(harness.Location.BackupArchivePath, Read(archive, VaultExport.MemoryEntryName));
            await File.WriteAllBytesAsync(harness.Location.CompanionArchivePath, Read(archive, VaultExport.CompanionEntryName));
        }

        await File.WriteAllBytesAsync(harness.Location.DatabasePath, "damaged"u8.ToArray());

        // Bnuy Repairs will not graft an older cut onto a newer journal (that would drop "later").
        await Assert.ThrowsAnyAsync<Exception>(() => CompanionHost.RepairOfflineAsync(
            new CompanionHostOptions(harness.Location, harness.Privacy, harness.Controller, harness.Provider, new InMemoryCredentialStore())));
    }

    [Fact]
    public async Task UnsafeDestinations_AreRefused_AndAnExistingFileIsNeverOverwritten()
    {
        await using var harness = await WithMemoryAndPhotographAsync();
        Directory.CreateDirectory(_outside);
        var existing = Path.Combine(_outside, "existing.zip");
        await File.WriteAllBytesAsync(existing, "keep me"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Host.ExportVaultAsync("relative/vault.zip"));
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Host.ExportVaultAsync(Path.Combine(_outside, "vault.txt")));
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Host.ExportVaultAsync(Path.Combine(harness.Location.RootPath, "inside.zip")));
        await Assert.ThrowsAsync<IOException>(() => harness.Host.ExportVaultAsync(existing));

        Assert.Equal("keep me"u8.ToArray(), await File.ReadAllBytesAsync(existing));
        Assert.False(File.Exists(Path.Combine(harness.Location.RootPath, "inside.zip")));
    }

    [Fact]
    public async Task ATamperedOrForeignExport_FailsVerification()
    {
        await using var harness = await WithMemoryAndPhotographAsync();
        var genuine = Path.Combine(_outside, "genuine.zip");
        await harness.Host.ExportVaultAsync(genuine);

        // Same manifest, one altered byte in the memory archive.
        var tampered = Path.Combine(_outside, "tampered.zip");
        Rewrite(genuine, tampered, (name, bytes) =>
        {
            if (name == VaultExport.MemoryEntryName)
            {
                bytes[^1] ^= 0xFF;
            }

            return bytes;
        });

        // Every genuine entry intact, plus one extra entry.
        var extra = Path.Combine(_outside, "extra.zip");
        Rewrite(genuine, extra, (_, bytes) => bytes, ("notes.txt", "an extra entry"u8.ToArray()));

        // A manifest from an unknown format version.
        var future = Path.Combine(_outside, "future.zip");
        Rewrite(genuine, future, (name, bytes) =>
        {
            if (name != VaultExport.ManifestName)
            {
                return bytes;
            }

            var manifest = System.Text.Json.Nodes.JsonNode.Parse(bytes)!.AsObject();
            manifest["FormatVersion"] = VaultExport.FormatVersion + 1;
            return System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString());
        });

        var foreign = Path.Combine(_outside, "foreign.zip");
        using (var target = ZipFile.Open(foreign, ZipArchiveMode.Create))
        {
            using var stream = target.CreateEntry("something-else.txt").Open();
            stream.Write("not a vault"u8);
        }

        var garbage = Path.Combine(_outside, "garbage.zip");
        await File.WriteAllBytesAsync(garbage, "not a zip"u8.ToArray());

        foreach (var refused in new[] { tampered, extra, future, foreign, garbage, Path.Combine(_outside, "missing.zip") })
        {
            await Assert.ThrowsAsync<BackupValidationException>(() => CompanionHost.VerifyVaultExportAsync(refused));
        }

        await CompanionHost.VerifyVaultExportAsync(genuine);
    }

    private static void Rewrite(string source, string destination, Func<string, byte[], byte[]> transform, (string Name, byte[] Bytes)? extra = null)
    {
        using var input = ZipFile.OpenRead(source);
        using var output = ZipFile.Open(destination, ZipArchiveMode.Create);
        foreach (var entry in input.Entries)
        {
            using var stream = output.CreateEntry(entry.FullName).Open();
            stream.Write(transform(entry.FullName, Read(input, entry.FullName)));
        }

        if (extra is { } added)
        {
            using var stream = output.CreateEntry(added.Name).Open();
            stream.Write(added.Bytes);
        }
    }

    private static byte[] Read(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
