using System;

public struct PS2Vector3
{
    public float X;
    public float Y;
    public float Z;

    public PS2Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static PS2Vector3 Zero =>
        new PS2Vector3(0, 0, 0);

    public static PS2Vector3 operator +(PS2Vector3 a, PS2Vector3 b)
    {
        return new PS2Vector3(
            a.X + b.X,
            a.Y + b.Y,
            a.Z + b.Z
        );
    }

    public static PS2Vector3 operator -(PS2Vector3 a, PS2Vector3 b)
    {
        return new PS2Vector3(
            a.X - b.X,
            a.Y - b.Y,
            a.Z - b.Z
        );
    }

    public static PS2Vector3 operator *(PS2Vector3 a, float scalar)
    {
        return new PS2Vector3(
            a.X * scalar,
            a.Y * scalar,
            a.Z * scalar
        );
    }

    public float Length()
    {
        return MathF.Sqrt(X * X + Y * Y + Z * Z);
    }

    public PS2Vector3 Normalize()
    {
        float length = Length();

        if (length <= 0.00001f)
            return Zero;

        return new PS2Vector3(
            X / length,
            Y / length,
            Z / length
        );
    }

    public static float Dot(PS2Vector3 a, PS2Vector3 b)
    {
        return
            a.X * b.X +
            a.Y * b.Y +
            a.Z * b.Z;
    }

    public static PS2Vector3 Cross(PS2Vector3 a, PS2Vector3 b)
    {
        return new PS2Vector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }
}