namespace CompanionCore.Capture.Contracts;

/// <summary>
/// A target-content-relative rectangle. Coordinates are finite fractions in [0, 1]
/// and never contain desktop or monitor position information.
/// </summary>
public readonly record struct NormalizedRegion(double X, double Y, double Width, double Height)
{
    public bool IsValid =>
        double.IsFinite(X)
        && double.IsFinite(Y)
        && double.IsFinite(Width)
        && double.IsFinite(Height)
        && X >= 0
        && Y >= 0
        && Width > 0
        && Height > 0
        && X <= 1
        && Y <= 1
        && Width <= 1 - X
        && Height <= 1 - Y;

    public void Validate(string? parameterName = null)
    {
        if (!IsValid)
        {
            throw new ArgumentOutOfRangeException(
                parameterName ?? nameof(NormalizedRegion),
                "A normalized region must be finite, positive, and contained in [0, 1].");
        }
    }
}
