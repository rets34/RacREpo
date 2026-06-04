using System.Numerics;

public class Camera
{
    public Vector3 Position = new Vector3(0, 0, 3);

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(
            Position,
            Vector3.Zero,
            Vector3.UnitY
        );
    }

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4f,
            aspectRatio,
            0.1f,
            100f
        );
    }
}