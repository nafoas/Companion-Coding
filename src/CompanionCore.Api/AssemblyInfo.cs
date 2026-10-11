using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CompanionCore.Api.Tests")]

// The live Claude adapter's first-run check builds one synthetic request and validates the
// reply with the shared strict parser. It gains no memory, grant, or journal authority.
[assembly: InternalsVisibleTo("CompanionCore.Braincase.Anthropic")]
