using System;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.Render;

public class LayerTurnAnimation
{
    public LayerAxis Axis { get; }
    public int Layer { get; }
    public bool Clockwise { get; }
    public float Progress { get; private set; }

    public float Angle => EaseInOut(Progress) * MathF.PI / 2f * (Clockwise ? 1f : -1f);
    public bool IsComplete => Progress >= 1f;

    public LayerTurnAnimation(LayerAxis axis, int layer, bool clockwise)
    {
        Axis = axis;
        Layer = layer;
        Clockwise = clockwise;
    }

    public void Advance(float amount)
    {
        Progress = MathF.Min(1f, Progress + amount);
    }

    private static float EaseInOut(float value)
    {
        return value * value * (3f - 2f * value);
    }

    public bool Includes(Cubie cubie)
    {
        return Axis switch
        {
            LayerAxis.X => cubie.GX == Layer,
            LayerAxis.Y => cubie.GY == Layer,
            LayerAxis.Z => cubie.GZ == Layer,
            _ => false
        };
    }
}
