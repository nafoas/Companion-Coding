using CompanionCore.Api;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using CompanionCore.Runtime.Diagnostics;
using CompanionCore.TargetAuth;
using CompanionCore.Transcript;
using CompanionCore.Vault;

namespace CompanionCore.Orchestration;

public sealed record CompanionHostOptions(
    MemoryStoreLocation Location,
    RuntimePrivacyState Privacy,
    TargetSessionController Controller,
    ISemanticProvider Provider,
    ICredentialStore Credentials)
{
    public IPlatformSignals? Platform { get; init; }

    public TimeProvider? Time { get; init; }

    public OrchestratorOptions? Orchestrator { get; init; }

    public BridgeOptions? Bridge { get; init; }

    public IDiagnosticsSink? Diagnostics { get; init; }

    /// <summary>Attached before every start (including after repairs), so no startup notice is missed.</summary>
    public EventHandler<CompanionNotice>? Notice { get; init; }
}

/// <summary>
/// Owns the one memory repository, Braincase bridge, and orchestrator for a validated
/// development or test data root. Bnuy Repairs stops everything, restores the whole Vault,
/// and reopens; it is refused while a target session is active.
/// </summary>
public sealed class CompanionHost : IAsyncDisposable
{
    private readonly CompanionHostOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private bool _disposed;

    private CompanionHost(CompanionHostOptions options)
    {
        _options = options;
        Keepsakes = KeepsakeLocation.For(options.Location);
        Transcripts = TranscriptLocation.For(options.Location);
        State = new VaultStateStore(VaultStateLocation.For(options.Location));
        Braincase = BraincaseStateLocation.For(options.Location);
    }

    public CompanionOrchestrator Orchestrator { get; private set; } = null!;

    public MemoryRepository Repository { get; private set; } = null!;

    public ApiBridge Bridge { get; private set; } = null!;

    public KeepsakeLocation Keepsakes { get; }

    public TranscriptLocation Transcripts { get; }

    public VaultStateStore State { get; }

    public BraincaseStateLocation Braincase { get; }

    public MemoryStoreLocation Location => _options.Location;

    public static async Task<CompanionHost> OpenAsync(CompanionHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var host = new CompanionHost(options);
        await host.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }

    /// <summary>
    /// Bnuy Repairs when the host cannot even open (for example a damaged BunDex). Nothing may
    /// hold the data root; afterwards open a host normally.
    /// </summary>
    public static Task<VaultRestoreReport> RepairOfflineAsync(CompanionHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Controller.CurrentSession.Phase != TargetSessionPhase.None)
        {
            throw new InvalidOperationException("Repairs run only while no target session is active.");
        }

        return DaBunVault.RestoreAsync(
            options.Location,
            options.Privacy,
            KeepsakeLocation.For(options.Location),
            new VaultStateStore(VaultStateLocation.For(options.Location)),
            cancellationToken);
    }

    /// <summary>Bnuy Repairs for the whole Vault. Only when no target session is active.</summary>
    public async Task<VaultRestoreReport> RepairAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_options.Controller.CurrentSession.Phase != TargetSessionPhase.None)
            {
                throw new InvalidOperationException("Repairs run only while no target session is active.");
            }

            await StopCoreAsync().ConfigureAwait(false);
            try
            {
                return await DaBunVault.RestoreAsync(_options.Location, _options.Privacy, Keepsakes, State, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                await StartCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var time = _options.Time ?? TimeProvider.System;
        Repository = await MemoryRepository.OpenAsync(_options.Location, _options.Privacy, cancellationToken).ConfigureAwait(false);
        try
        {
            Bridge = ApiBridge.Open(
                _options.Provider,
                Repository.WriteGate,
                new MemoryRepositoryReader(Repository),
                _options.Privacy,
                Braincase,
                _options.Credentials,
                _options.Bridge,
                time,
                _options.Diagnostics);
            Orchestrator = new CompanionOrchestrator(
                new CompanionComponents(
                    Repository,
                    Bridge,
                    _options.Controller,
                    _options.Privacy,
                    Keepsakes,
                    Transcripts,
                    State,
                    _options.Platform,
                    time),
                _options.Orchestrator);
            if (_options.Notice is { } notice)
            {
                Orchestrator.Notice += notice;
            }

            await Orchestrator.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await StopCoreAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        try
        {
            if (Orchestrator is not null)
            {
                await Orchestrator.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                if (Bridge is not null)
                {
                    await Bridge.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (Repository is not null)
                {
                    await Repository.DisposeAsync().ConfigureAwait(false);
                }

                Orchestrator = null!;
                Bridge = null!;
                Repository = null!;
            }
        }
    }
}
