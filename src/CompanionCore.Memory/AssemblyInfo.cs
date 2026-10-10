using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CompanionCore.Memory.Tests")]
[assembly: InternalsVisibleTo("CompanionCore.Api.Tests")]
[assembly: InternalsVisibleTo("CompanionCore.Transcript.Tests")]

// The Da Bun Vault is the single composition point of backup and repair authority.
[assembly: InternalsVisibleTo("CompanionCore.Vault")]
[assembly: InternalsVisibleTo("CompanionCore.Vault.Tests")]
[assembly: InternalsVisibleTo("CompanionCore.Orchestration.Tests")]
