using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace CubeGame.Avalonia.Scene;

public class Cube3x3
{
    private readonly Cubie[,,] _cubies = new Cubie[3, 3, 3];

    public Cubie this[int x, int y, int z] => _cubies[x + 1, y + 1, z + 1];

    public IEnumerable<Cubie> AllCubies
    {
        get
        {
            for (int ix = -1; ix <= 1; ix++)
                for (int iy = -1; iy <= 1; iy++)
                    for (int iz = -1; iz <= 1; iz++)
                        yield return _cubies[ix + 1, iy + 1, iz + 1];
        }
    }

    public Cube3x3()
    {
        Reset();
    }

    public void Reset()
    {
        var r = Color.FromRgb(200, 50, 50);
        var o = Color.FromRgb(255, 150, 0);
        var bl = Color.FromRgb(50, 100, 255);
        var g = Color.FromRgb(50, 200, 50);
        var w = Color.FromRgb(255, 255, 255);
        var yl = Color.FromRgb(255, 230, 0);

        for (int ix = -1; ix <= 1; ix++)
            for (int iy = -1; iy <= 1; iy++)
                for (int iz = -1; iz <= 1; iz++)
                {
                    var c = new Cubie(ix, iy, iz);
                    if (ix == 1) c.SetFace(FaceDir.Right, r);
                    if (ix == -1) c.SetFace(FaceDir.Left, o);
                    if (iy == 1) c.SetFace(FaceDir.Up, w);
                    if (iy == -1) c.SetFace(FaceDir.Down, yl);
                    if (iz == -1) c.SetFace(FaceDir.Front, g);
                    if (iz == 1) c.SetFace(FaceDir.Back, bl);
                    _cubies[ix + 1, iy + 1, iz + 1] = c;
                }
    }

    public void Restore(IEnumerable<Cubie> cubies)
    {
        var list = cubies.ToList();
        if (list.Count != 27)
        {
            Reset();
            return;
        }

        foreach (var cubie in list)
        {
            if (cubie.GX < -1 || cubie.GX > 1 ||
                cubie.GY < -1 || cubie.GY > 1 ||
                cubie.GZ < -1 || cubie.GZ > 1)
            {
                Reset();
                return;
            }
        }

        for (int ix = 0; ix < 3; ix++)
            for (int iy = 0; iy < 3; iy++)
                for (int iz = 0; iz < 3; iz++)
                    _cubies[ix, iy, iz] = null!;

        foreach (var cubie in list)
            _cubies[cubie.GX + 1, cubie.GY + 1, cubie.GZ + 1] = cubie;
    }

    public void RotateLayer(LayerAxis axis, int layer, bool clockwise)
    {
        var selected = AllCubies
            .Where(c => GetLayer(c, axis) == layer)
            .ToList();

        foreach (var cubie in selected)
            _cubies[cubie.GX + 1, cubie.GY + 1, cubie.GZ + 1] = null!;

        foreach (var cubie in selected)
        {
            RotateCubiePosition(cubie, axis, clockwise);
            cubie.RotateStickers(axis, clockwise);
            _cubies[cubie.GX + 1, cubie.GY + 1, cubie.GZ + 1] = cubie;
        }
    }

    private static void RotateCubiePosition(Cubie cubie, LayerAxis axis, bool clockwise)
    {
        int x = cubie.GX;
        int y = cubie.GY;
        int z = cubie.GZ;

        if (axis == LayerAxis.X)
        {
            cubie.GY = clockwise ? -z : z;
            cubie.GZ = clockwise ? y : -y;
            return;
        }

        if (axis == LayerAxis.Y)
        {
            cubie.GX = clockwise ? z : -z;
            cubie.GZ = clockwise ? -x : x;
            return;
        }

        cubie.GX = clockwise ? -y : y;
        cubie.GY = clockwise ? x : -x;
    }

    private static int GetLayer(Cubie cubie, LayerAxis axis) => axis switch
    {
        LayerAxis.X => cubie.GX,
        LayerAxis.Y => cubie.GY,
        LayerAxis.Z => cubie.GZ,
        _ => cubie.GY
    };
}
