using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CompanionCore.Memory;
using static CompanionCore.Vault.Tests.VaultHarness;

namespace CompanionCore.Vault.Tests;

public sealed class CompanionArchiveTests
{
    public static TheoryData<string> Attacks => new()
    {
        "extra-entry", "missing-entry", "tampered-entry", "manifest-checksum", "non-canonical-manifest",
        "traversal-entry", "directory-entry", "invalid-state-envelope", "wrong-backup", "unsorted-manifest",
        "bad-entry-name", "length-mismatch", "missing-manifest", "not-a-zip", "empty-file", "format-version",
        "same-length-tamper",
    };

    [Theory]
    [MemberData(nameof(Attacks))]
    public async Task Scenario5_InvalidCompanionArchives_AreRefused(string attack)
    {
        await using var harness = await CreateAsync();
        var (_, action) = await harness.PhotographAsync(3);
        await harness.State.PutAsync("settings", Bytes("good"));
        var backupId = Guid.NewGuid();
        var path = Path.Combine(harness.BasePath, "candidate.zip");
        await CompanionArchive.BuildAsync(backupId, T0, harness.Keepsakes, harness.State, path, default);
        var validationParent = Path.Combine(harness.BasePath, "validation");
        using (var valid = await CompanionArchive.ValidateAsync(path, validationParent, backupId, default))
        {
            Assert.Equal(2, valid.Manifest.Entries.Count);
        }

        Guid? expected = backupId;
        switch (attack)
        {
            case "extra-entry":
                Mutate(path, archive => Write(archive, "keepsake-" + new string('b', 32) + ".png", [1]));
                break;
            case "missing-entry":
                Mutate(path, archive => archive.GetEntry("state-settings.state")!.Delete());
                break;
            case "tampered-entry":
                Mutate(path, archive => Replace(archive, CompanionArchive.KeepsakeEntryName(action), [1, 2, 3]));
                break;
            case "manifest-checksum":
                Mutate(path, archive => Replace(archive, CompanionArchive.ChecksumName, Encoding.ASCII.GetBytes(new string('0', 64))));
                break;
            case "non-canonical-manifest":
                Mutate(path, archive => RewriteManifest(archive, json => " " + json));
                break;
            case "traversal-entry":
                Mutate(path, archive => Write(archive, "../escape.png", [1]));
                break;
            case "directory-entry":
                Mutate(path, archive => Write(archive, "nested/keepsake.png", [1]));
                break;
            case "invalid-state-envelope":
                Mutate(path, archive =>
                {
                    Replace(archive, "state-settings.state", Bytes("not an envelope"));
                    RewriteManifest(archive, json => ReplaceEntryDigest(json, "state-settings.state", Bytes("not an envelope")));
                });
                break;
            case "wrong-backup":
                expected = Guid.NewGuid();
                break;
            case "unsorted-manifest":
                Mutate(path, archive => RewriteManifest(archive, json =>
                {
                    var manifest = CompanionArchive.Parse(Encoding.UTF8.GetBytes(json));
                    return Encoding.UTF8.GetString(CompanionArchive.Serialize(manifest with { Entries = manifest.Entries.Reverse().ToArray() }));
                }));
                break;
            case "bad-entry-name":
                Mutate(path, archive =>
                {
                    var bytes = Read(archive, "state-settings.state");
                    archive.GetEntry("state-settings.state")!.Delete();
                    Write(archive, "state-Settings.state", bytes);
                    RewriteManifest(archive, json => json.Replace("state-settings.state", "state-Settings.state", StringComparison.Ordinal));
                });
                break;
            case "length-mismatch":
                Mutate(path, archive => RewriteManifest(archive, json =>
                {
                    var manifest = CompanionArchive.Parse(Encoding.UTF8.GetBytes(json));
                    var entries = manifest.Entries.Select(entry => entry with { Length = entry.Length + 1 }).ToArray();
                    return Encoding.UTF8.GetString(CompanionArchive.Serialize(manifest with { Entries = entries }));
                }));
                break;
            case "missing-manifest":
                Mutate(path, archive => archive.GetEntry(CompanionArchive.ManifestName)!.Delete());
                break;
            case "not-a-zip":
                await File.WriteAllBytesAsync(path, Bytes("not a zip archive"));
                break;
            case "empty-file":
                await File.WriteAllBytesAsync(path, []);
                break;
            case "same-length-tamper":
                Mutate(path, archive =>
                {
                    var name = CompanionArchive.KeepsakeEntryName(action);
                    var bytes = Read(archive, name);
                    bytes[^20] ^= 0x01;
                    Replace(archive, name, bytes);
                });
                break;
            case "format-version":
                Mutate(path, archive => RewriteManifest(archive, json => json.Replace("\"formatVersion\":1", "\"formatVersion\":2", StringComparison.Ordinal)));
                break;
        }

        await Assert.ThrowsAsync<BackupValidationException>(() => CompanionArchive.ValidateAsync(path, validationParent, expected, default));
        Assert.Empty(Directory.Exists(validationParent) ? Directory.GetDirectories(validationParent) : []);
    }

    [Fact]
    public async Task BuildIncludesOnlyCanonicalKeepsakes_AndEmptyCompanionsAreValid()
    {
        await using var harness = await CreateAsync();
        var path = Path.Combine(harness.BasePath, "empty.zip");
        var empty = await CompanionArchive.BuildAsync(Guid.NewGuid(), T0, harness.Keepsakes, harness.State, path, default);
        Assert.Empty(empty.Entries);
        using (await CompanionArchive.ValidateAsync(path, Path.Combine(harness.BasePath, "v"), empty.BackupId, default))
        {
        }

        var (_, action) = await harness.PhotographAsync(3);
        await File.WriteAllBytesAsync(Path.Combine(harness.Keepsakes.RootPath, ".stray.tmp"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(harness.Keepsakes.RootPath, "notes.txt"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(harness.Keepsakes.RootPath, $"{Guid.NewGuid().ToString("N").ToUpperInvariant()}.png"), [1]);
        var second = Path.Combine(harness.BasePath, "second.zip");

        var manifest = await CompanionArchive.BuildAsync(Guid.NewGuid(), T0, harness.Keepsakes, harness.State, second, default);

        var entry = Assert.Single(manifest.Entries);
        Assert.Equal(CompanionArchive.KeepsakeEntryName(action), entry.Name);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(harness.PhotographPath(action)))), entry.Sha256);
    }

    private static void Mutate(string path, Action<ZipArchive> mutate)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        mutate(archive);
    }

    private static byte[] Read(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string name, byte[] bytes)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    private static void Replace(ZipArchive archive, string name, byte[] bytes)
    {
        archive.GetEntry(name)!.Delete();
        Write(archive, name, bytes);
    }

    /// <summary>Rewrites the manifest and keeps its checksum consistent, so only the targeted rule can fail.</summary>
    private static void RewriteManifest(ZipArchive archive, Func<string, string> rewrite)
    {
        var json = rewrite(Encoding.UTF8.GetString(Read(archive, CompanionArchive.ManifestName)));
        var bytes = Encoding.UTF8.GetBytes(json);
        Replace(archive, CompanionArchive.ManifestName, bytes);
        Replace(archive, CompanionArchive.ChecksumName, Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }

    private static string ReplaceEntryDigest(string json, string name, byte[] content)
    {
        var manifest = CompanionArchive.Parse(Encoding.UTF8.GetBytes(json));
        var entries = manifest.Entries.Select(entry => entry.Name == name
            ? entry with { Length = content.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)) }
            : entry).ToArray();
        return Encoding.UTF8.GetString(CompanionArchive.Serialize(manifest with { Entries = entries }));
    }
}

public sealed class VaultStateStoreTests
{
    [Fact]
    public async Task Scenario7_StateEntriesAreAtomicChecksummedAndBounded()
    {
        await using var harness = await CreateAsync();
        var store = harness.State;
        Assert.Equal(StateStatus.Missing, (await store.GetAsync("settings")).Status);

        await store.PutAsync("settings", Bytes("one"));
        await store.PutAsync("settings", Bytes("two"));
        await store.PutAsync("empty", ReadOnlyMemory<byte>.Empty);
        var read = await store.GetAsync("settings");
        Assert.Equal(StateStatus.Verified, read.Status);
        Assert.Equal(Bytes("two"), read.Payload.ToArray());
        Assert.Equal(StateStatus.Verified, (await store.GetAsync("empty")).Status);
        Assert.Equal(["empty", "settings"], store.Names());
        Assert.Empty(Directory.GetFiles(store.Location.RootPath, "*.tmp"));

        var path = store.PathFor("settings");
        var bytes = await File.ReadAllBytesAsync(path);
        var flipped = bytes.ToArray();
        flipped[^1] ^= 0x01;
        await File.WriteAllBytesAsync(path, flipped);
        Assert.Equal(StateStatus.Damaged, (await store.GetAsync("settings")).Status);
        await File.WriteAllBytesAsync(path, bytes[..10]);
        Assert.Equal(StateStatus.Damaged, (await store.GetAsync("settings")).Status);
        await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("companion-state-v2\n").Concat(bytes[19..]).ToArray());
        Assert.Equal(StateStatus.Damaged, (await store.GetAsync("settings")).Status);

        await Assert.ThrowsAsync<ArgumentException>(() => store.PutAsync("Settings", Bytes("x")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.PutAsync("../escape", Bytes("x")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.PutAsync(new string('a', 65), Bytes("x")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.PutAsync("big", new byte[VaultStateStore.MaximumPayloadBytes + 1]));
        await store.PutAsync("biggest", new byte[VaultStateStore.MaximumPayloadBytes]);
        Assert.Equal(StateStatus.Verified, (await store.GetAsync("biggest")).Status);

        for (var index = store.Names().Count; index < VaultStateStore.MaximumEntries; index++)
        {
            await store.PutAsync($"entry-{index}", Bytes("x"));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PutAsync("one-too-many", Bytes("x")));
        await store.PutAsync("settings", Bytes("replacing an existing entry is still allowed"));

        Directory.CreateDirectory(store.Location.DamagedDirectoryPath);
        await File.WriteAllBytesAsync(Path.Combine(store.Location.RootPath, ".x.tmp"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(store.Location.RootPath, "Upper.state"), [1]);
        Assert.Equal(VaultStateStore.MaximumEntries, store.Names().Count);
    }

    [Fact]
    public void EnvelopesDetectEveryCorruption()
    {
        var envelope = VaultStateStore.Encode(Bytes("payload"));

        Assert.True(VaultStateStore.TryDecode(envelope, out var payload));
        Assert.Equal(Bytes("payload"), payload);
        for (var index = 0; index < envelope.Length; index++)
        {
            var copy = envelope.ToArray();
            copy[index] ^= 0x20;
            Assert.False(VaultStateStore.TryDecode(copy, out _));
        }

        Assert.False(VaultStateStore.TryDecode(envelope[..^1], out _));
        Assert.False(VaultStateStore.TryDecode([], out _));
    }
}
