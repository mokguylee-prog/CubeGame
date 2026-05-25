using System;
using System.Collections.Generic;

namespace CubeGame.Avalonia.Render;

public class Camera
{
    public Vector3 Position { get; set; } = new(0, 0, -6);
    public Vector3 Target { get; set; } = Vector3.Zero;
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Fov { get; set; } = 60f;
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 100f;

    public Vector3 Forward => (Target - Position).Normalized();
    public Vector3 Right => Vector3.Cross(new Vector3(0, 1, 0), Forward).Normalized();
    public Vector3 Up => Vector3.Cross(Right, Forward).Normalized();

    public (PointF screen, float depth) Project(Vector3 world, float screenW, float screenH)
    {
        var dir = world - Position;
        float fwd = Vector3.Dot(dir, Forward);
        if (fwd <= 0) return (new PointF(0, 0), -1);

        float right = Vector3.Dot(dir, Right);
        float up = Vector3.Dot(dir, Up);
        float scale = MathF.Min(screenW, screenH) * 0.1232f;
        float cameraDistance = (Target - Position).Length;
        float perspective = cameraDistance / fwd;

        float sx = CenterX + right * scale * perspective;
        float sy = CenterY - up * scale * perspective;

        return (new PointF(sx, sy), fwd);
    }
}

public struct PointF
{
    public float X, Y;
    public PointF(float x, float y) { X = x; Y = y; }
}
