namespace CompanionCore.TargetAuth;

/// <summary>
/// Internal deterministic fault seam. It is reachable only from the target-auth test
/// assembly and executes after full staged validation but before atomic promotion.
/// </summary>
internal interface IAuthorizationPolicyTestHook
{
    Task BeforePromotionAsync(CancellationToken cancellationToken);
}
