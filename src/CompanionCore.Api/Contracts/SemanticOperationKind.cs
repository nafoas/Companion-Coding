namespace CompanionCore.Api;

public enum SemanticOperationKind
{
    InterpretAttentionSheet = 1,

    /// <summary>A quick, peripheral look (VIS-02): attention evidence only, never memory.</summary>
    Glance = 2,
}
