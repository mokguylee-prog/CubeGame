using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using CubeGame.Avalonia.Game;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.Render;

public class CubeRenderer
{
    private readonly Camera _cam = new();
    private Vector3 _viewX = Vector3.UnitX.RotateX(-0.35f).RotateY(0.55f);
    private Vector3 _viewY = Vector3.UnitY.RotateX(-0.35f).RotateY(0.55f);
    private Vector3 _viewZ = Vector3.UnitZ.RotateX(-0.35f).RotateY(0.55f);

    private struct FaceData
    {
        public List<global::Avalonia.Point> Points;
        public Color Color;
        public float Depth;
    }

    public void Rotate(float dx, float dy)
    {
        _viewX = _viewX.RotateX(dx).RotateY(dy);
        _viewY = _viewY.RotateX(dx).RotateY(dy);
        _viewZ = _viewZ.RotateX(dx).RotateY(dy);
    }

    public void ResetView()
    {
        _viewX = Vector3.UnitX.RotateX(-0.35f).RotateY(0.55f);
        _viewY = Vector3.UnitY.RotateX(-0.35f).RotateY(0.55f);
        _viewZ = Vector3.UnitZ.RotateX(-0.35f).RotateY(0.55f);
    }

    public ViewState CaptureView()
    {
        return new ViewState
        {
            Xx = _viewX.X, Xy = _viewX.Y, Xz = _viewX.Z,
            Yx = _viewY.X, Yy = _viewY.Y, Yz = _viewY.Z,
            Zx = _viewZ.X, Zy = _viewZ.Y, Zz = _viewZ.Z
        };
    }

    public void RestoreView(ViewState? state)
    {
        if (state is null)
        {
            ResetView();
            return;
        }

        _viewX = new Vector3(state.Xx, state.Xy, state.Xz);
        _viewY = new Vector3(state.Yx, state.Yy, state.Yz);
        _viewZ = new Vector3(state.Zx, state.Zy, state.Zz);
    }

    public void Render(DrawingContext dc, int w, int h, Cube3x3 cube, LayerTurnAnimation? turn = null)
    {
        _cam.Position = new Vector3(0, 0, -6);
        _cam.Target = Vector3.Zero;
        _cam.CenterX = GetCubeCenterX(w);
        _cam.CenterY = GetCubeCenterY(h);

        List<FaceData> allFaces = new();
        foreach (var cubie in cube.AllCubies)
            RenderCubie(cubie, allFaces, w, h, turn);

        allFaces.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        var outline = new Pen(new SolidColorBrush(Color.FromRgb(220, 220, 220)), 1.2);
        foreach (var face in allFaces)
        {
            var geo = CreateRoundedPolygon(face.Points);
            var fill = new SolidColorBrush(Color.FromArgb(245, face.Color.R, face.Color.G, face.Color.B));
            dc.DrawGeometry(fill, outline, geo);
        }
    }

    private void RenderCubie(Cubie cubie, List<FaceData> faces, int w, int h, LayerTurnAnimation? turn)
    {
        float s = 0.45f;
        bool isTurning = turn?.Includes(cubie) == true;
        var pos = RotateView(ApplyLayerTurn(cubie.WorldPos, turn, isTurning));

        var verts = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float x = (i & 1) == 0 ? -s : s;
            float y = (i & 2) == 0 ? -s : s;
            float z = (i & 4) == 0 ? -s : s;
            var v = RotateView(ApplyLayerTurn(new Vector3(x, y, z), turn, isTurning));
            verts[i] = pos + v;
        }

        var dirs = new[] {
            new { Dir = FaceDir.Right, Indices = new[] { 1, 3, 7, 5 } },
            new { Dir = FaceDir.Left, Indices = new[] { 4, 6, 2, 0 } },
            new { Dir = FaceDir.Up, Indices = new[] { 2, 3, 7, 6 } },
            new { Dir = FaceDir.Down, Indices = new[] { 4, 5, 1, 0 } },
            new { Dir = FaceDir.Front, Indices = new[] { 0, 1, 3, 2 } },
            new { Dir = FaceDir.Back, Indices = new[] { 5, 4, 6, 7 } },
        };

        foreach (var d in dirs)
        {
            var normal = RotateView(ApplyLayerTurn(GetFaceNormal(d.Dir), turn, isTurning));
            if (normal.Z >= -0.02f) continue;

            var color = cubie.FaceColors[(int)d.Dir];
            if (color.A == 0) continue;

            var pts = new List<global::Avalonia.Point>();
            float avgZ = 0;
            for (int i = 0; i < 4; i++)
            {
                var v = verts[d.Indices[i]];
                var (sp, depth) = _cam.Project(v, w, h);
                pts.Add(new global::Avalonia.Point(sp.X, sp.Y));
                avgZ += depth;
            }
            avgZ /= 4;

            faces.Add(new FaceData { Points = Inset(pts, 0.88), Color = color, Depth = avgZ });
        }
    }

    public string GetFrontFaceName()
    {
        FaceDir front = FaceDir.Front;
        float minZ = float.MaxValue;

        foreach (var dir in Enum.GetValues<FaceDir>())
        {
            var normal = RotateView(GetFaceNormal(dir));
            if (normal.Z < minZ)
            {
                minZ = normal.Z;
                front = dir;
            }
        }

        return GetFaceName(front);
    }

    public string GetFrontCubieLabel(Cube3x3 cube)
    {
        float minZ = float.MaxValue;
        string label = "";
        foreach (var cubie in cube.AllCubies)
        {
            var pos = RotateView(cubie.WorldPos);
            if (pos.Z < minZ)
            {
                minZ = pos.Z;
                label = $"({cubie.GX},{cubie.GY},{cubie.GZ})";
            }
        }
        return label;
    }

    public string GetDisplayLabel(Cube3x3 cube, CubeMode mode)
    {
        var face = GetFrontFaceName();
        return mode == CubeMode.Standard
            ? face
            : $"{face} / {GetFrontCubieLabel(cube)}";
    }

    public (LayerAxis Axis, int Layer) ResolveViewLayer(LayerAxis viewGroup, int visualLayer)
    {
        var (axis, sign) = viewGroup == LayerAxis.X
            ? GetBestScreenAxis(horizontal: true)
            : GetBestScreenAxis(horizontal: false);

        return (axis, visualLayer * sign);
    }

    public static float GetCubeCenterX(int w) => w < 760 ? w / 2f + 30 : w / 2f + 85;
    public static float GetCubeCenterY(int h) => h / 2f + 18;

    private Vector3 RotateView(Vector3 vector)
    {
        return _viewX * vector.X + _viewY * vector.Y + _viewZ * vector.Z;
    }

    private (LayerAxis Axis, int Sign) GetBestScreenAxis(bool horizontal)
    {
        var candidates = new[]
        {
            (Axis: LayerAxis.X, View: RotateView(Vector3.UnitX)),
            (Axis: LayerAxis.Y, View: RotateView(Vector3.UnitY)),
            (Axis: LayerAxis.Z, View: RotateView(Vector3.UnitZ))
        };

        var best = candidates[0];
        var bestScore = horizontal ? Math.Abs(best.View.X) : Math.Abs(best.View.Y);

        for (int i = 1; i < candidates.Length; i++)
        {
            var score = horizontal ? Math.Abs(candidates[i].View.X) : Math.Abs(candidates[i].View.Y);
            if (score > bestScore)
            {
                best = candidates[i];
                bestScore = score;
            }
        }

        if (horizontal)
            return (best.Axis, best.View.X >= 0 ? 1 : -1);

        return (best.Axis, best.View.Y < 0 ? 1 : -1);
    }

    private static Vector3 ApplyLayerTurn(Vector3 vector, LayerTurnAnimation? turn, bool isTurning)
    {
        if (!isTurning || turn is null) return vector;

        return turn.Axis switch
        {
            LayerAxis.X => vector.RotateX(turn.Angle),
            LayerAxis.Y => vector.RotateY(turn.Angle),
            LayerAxis.Z => vector.RotateZ(turn.Angle),
            _ => vector
        };
    }

    private static Vector3 GetFaceNormal(FaceDir dir) => dir switch
    {
        FaceDir.Right => Vector3.UnitX,
        FaceDir.Left => -Vector3.UnitX,
        FaceDir.Up => Vector3.UnitY,
        FaceDir.Down => -Vector3.UnitY,
        FaceDir.Front => -Vector3.UnitZ,
        FaceDir.Back => Vector3.UnitZ,
        _ => -Vector3.UnitZ
    };

    private static string GetFaceName(FaceDir dir) => dir switch
    {
        FaceDir.Right => "Right",
        FaceDir.Left => "Left",
        FaceDir.Up => "Top",
        FaceDir.Down => "Bottom",
        FaceDir.Front => "Front",
        FaceDir.Back => "Back",
        _ => "Front"
    };

    private static StreamGeometry CreatePolygon(List<global::Avalonia.Point> pts)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();
        ctx.BeginFigure(pts[0], true);
        for (int i = 1; i < pts.Count; i++)
            ctx.LineTo(pts[i]);
        ctx.EndFigure(true);
        return geo;
    }

    private static StreamGeometry CreateRoundedPolygon(List<global::Avalonia.Point> pts)
    {
        var geo = new StreamGeometry();
        if (pts.Count < 3) return geo;

        var entries = new global::Avalonia.Point[pts.Count];
        var exits = new global::Avalonia.Point[pts.Count];

        for (int i = 0; i < pts.Count; i++)
        {
            var prev = pts[(i + pts.Count - 1) % pts.Count];
            var current = pts[i];
            var next = pts[(i + 1) % pts.Count];
            var prevLength = Distance(current, prev);
            var nextLength = Distance(current, next);
            var radius = Math.Min(prevLength, nextLength) * 0.10;

            entries[i] = MoveToward(current, prev, radius);
            exits[i] = MoveToward(current, next, radius);
        }

        using var ctx = geo.Open();
        ctx.BeginFigure(exits[0], true);

        for (int i = 1; i < pts.Count; i++)
        {
            ctx.LineTo(entries[i]);
            ctx.QuadraticBezierTo(pts[i], exits[i]);
        }

        ctx.LineTo(entries[0]);
        ctx.QuadraticBezierTo(pts[0], exits[0]);
        ctx.EndFigure(true);
        return geo;
    }

    private static List<global::Avalonia.Point> Inset(List<global::Avalonia.Point> pts, double scale)
    {
        double cx = 0;
        double cy = 0;
        foreach (var pt in pts)
        {
            cx += pt.X;
            cy += pt.Y;
        }

        cx /= pts.Count;
        cy /= pts.Count;

        var inset = new List<global::Avalonia.Point>(pts.Count);
        foreach (var pt in pts)
            inset.Add(new global::Avalonia.Point(cx + (pt.X - cx) * scale, cy + (pt.Y - cy) * scale));

        return inset;
    }

    private static double Distance(global::Avalonia.Point a, global::Avalonia.Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static global::Avalonia.Point MoveToward(global::Avalonia.Point from, global::Avalonia.Point to, double distance)
    {
        var length = Distance(from, to);
        if (length <= 0.001) return from;

        var t = Math.Min(1.0, distance / length);
        return new global::Avalonia.Point(
            from.X + (to.X - from.X) * t,
            from.Y + (to.Y - from.Y) * t);
    }
}
