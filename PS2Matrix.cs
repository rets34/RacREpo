using System.Numerics;

public static class PS2Matrix
{
    public static Matrix4x4 Identity()
    {
        return Matrix4x4.Identity;
    }

    public static Matrix4x4 Translation(float x, float y, float z)
    {
        return Matrix4x4.CreateTranslation(x, y, z);
    }

    public static Matrix4x4 RotationY(float radians)
    {
        return Matrix4x4.CreateRotationY(radians);
    }

    public static Matrix4x4 Perspective(
        float fov,
        float aspect,
        float near,
        float far)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            fov,
            aspect,
            near,
            far
        );
    }

    public static Matrix4x4 Multiply(
        Matrix4x4 a,
        Matrix4x4 b)
    {
        return a * b;
    }
}