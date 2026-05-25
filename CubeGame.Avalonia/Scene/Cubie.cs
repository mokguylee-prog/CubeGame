using System;
using Avalonia.Media;

namespace CubeGame.Avalonia.Scene;

public enum FaceDir { Right, Left, Up, Down, Front, Back }

public class Cubie
{
    public int GX, GY, GZ;
    public Color[] FaceColors = new Color[6];

    public Cubie(int gx, int gy, int gz)
    {
        GX = gx; GY = gy; GZ = gz;
        Array.Fill(FaceColors, Colors.Transparent);
    }

    public Vector3 WorldPos => new(GX, GY, GZ);

    public void SetFace(FaceDir dir, Color color) => FaceColors[(int)dir] = color;

    public void RotateStickers(LayerAxis axis, bool clockwise)
    {
        var rotated = new Color[FaceColors.Length];
        Array.Fill(rotated, Colors.Transparent);

        for (int i = 0; i < FaceColors.Length; i++)
        {
            var color = FaceColors[i];
            if (color.A == 0) continue;

            var normal = GetNormal((FaceDir)i);
            var next = RotateNormal(normal, axis, clockwise);
            rotated[(int)GetFaceDir(next)] = color;
        }

        FaceColors = rotated;
    }

    private static (int X, int Y, int Z) RotateNormal((int X, int Y, int Z) normal, LayerAxis axis, bool clockwise)
    {
        var (x, y, z) = normal;
        if (axis == LayerAxis.X)
            return clockwise ? (x, -z, y) : (x, z, -y);

        if (axis == LayerAxis.Y)
            return clockwise ? (z, y, -x) : (-z, y, x);

        return clockwise ? (-y, x, z) : (y, -x, z);
    }

    private static (int X, int Y, int Z) GetNormal(FaceDir dir) => dir switch
    {
        FaceDir.Right => (1, 0, 0),
        FaceDir.Left => (-1, 0, 0),
        FaceDir.Up => (0, 1, 0),
        FaceDir.Down => (0, -1, 0),
        FaceDir.Front => (0, 0, -1),
        FaceDir.Back => (0, 0, 1),
        _ => (0, 0, -1)
    };

    private static FaceDir GetFaceDir((int X, int Y, int Z) normal) => normal switch
    {
        (1, 0, 0) => FaceDir.Right,
        (-1, 0, 0) => FaceDir.Left,
        (0, 1, 0) => FaceDir.Up,
        (0, -1, 0) => FaceDir.Down,
        (0, 0, -1) => FaceDir.Front,
        (0, 0, 1) => FaceDir.Back,
        _ => FaceDir.Front
    };
}
