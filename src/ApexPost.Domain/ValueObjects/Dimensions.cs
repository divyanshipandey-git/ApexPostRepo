namespace ApexPost.Domain.ValueObjects;

public sealed record Dimensions
{
    public decimal LengthCm { get; }
    public decimal WidthCm { get; }
    public decimal HeightCm { get; }

    public Dimensions(decimal lengthCm, decimal widthCm, decimal heightCm)
    {
        if (lengthCm <= 0 || widthCm <= 0 || heightCm <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthCm),
                "All dimensions must be positive.");
        }

        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
    }

    public decimal VolumeCubicCm => LengthCm * WidthCm * HeightCm;

    public override string ToString() => $"{LengthCm}×{WidthCm}×{HeightCm} cm";
}