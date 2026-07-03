namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A 2-D vector in the horizontal plane, used for the nested-frame kinematics and
/// the G-force superposition. Uses <c>double</c> precision throughout (the physics
/// needs it), so it is a small purpose-built type rather than
/// <see cref="System.Numerics.Vector2"/>.
/// </summary>
/// <param name="X">The x component.</param>
/// <param name="Y">The y component.</param>
public readonly record struct PlanarVector(double X, double Y)
{
    /// <summary>The zero vector.</summary>
    public static readonly PlanarVector Zero = new(0d, 0d);

    /// <summary>Euclidean length.</summary>
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    /// <summary>The angle of the vector, in radians, from the +x axis.</summary>
    public double Angle => Math.Atan2(Y, X);

    public static PlanarVector operator +(PlanarVector a, PlanarVector b) => new(a.X + b.X, a.Y + b.Y);

    public static PlanarVector operator -(PlanarVector a, PlanarVector b) => new(a.X - b.X, a.Y - b.Y);

    public static PlanarVector operator *(PlanarVector v, double scalar) => new(v.X * scalar, v.Y * scalar);

    public static PlanarVector operator *(double scalar, PlanarVector v) => v * scalar;

    /// <summary>Dot product with <paramref name="other"/>.</summary>
    public double Dot(PlanarVector other) => (X * other.X) + (Y * other.Y);

    /// <summary>A unit vector pointing at <paramref name="angle"/> radians.</summary>
    public static PlanarVector FromAngle(double angle) => new(Math.Cos(angle), Math.Sin(angle));

    /// <summary>This vector rotated by <paramref name="angle"/> radians about the origin.</summary>
    public PlanarVector Rotate(double angle)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        return new PlanarVector((X * cos) - (Y * sin), (X * sin) + (Y * cos));
    }
}
