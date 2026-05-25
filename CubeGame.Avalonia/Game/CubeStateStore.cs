using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media;
using CubeGame.Avalonia.Render;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.Game;

public static class CubeStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string SavePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CubeGame");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cube-state.json");
        }
    }

    public static void Save(Cube3x3 cube, CubeRenderer renderer)
    {
        var data = new CubeSaveData
        {
            View = renderer.CaptureView(),
            Cubies = cube.AllCubies.Select(CubieSaveData.FromCubie).ToList()
        };

        File.WriteAllText(SavePath, JsonSerializer.Serialize(data, Options));
    }

    public static bool Load(Cube3x3 cube, CubeRenderer renderer)
    {
        if (!File.Exists(SavePath)) return false;

        try
        {
            var json = File.ReadAllText(SavePath);
            var data = JsonSerializer.Deserialize<CubeSaveData>(json, Options);
            if (data?.Cubies is null || data.Cubies.Count != 27) return false;

            cube.Restore(data.Cubies.Select(c => c.ToCubie()));
            renderer.RestoreView(data.View);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class CubeSaveData
    {
        public int Version { get; set; } = 1;
        public ViewState? View { get; set; }
        public List<CubieSaveData> Cubies { get; set; } = [];
    }

    private sealed class CubieSaveData
    {
        public int GX { get; set; }
        public int GY { get; set; }
        public int GZ { get; set; }
        public uint[] FaceColors { get; set; } = [];

        public static CubieSaveData FromCubie(Cubie cubie)
        {
            return new CubieSaveData
            {
                GX = cubie.GX,
                GY = cubie.GY,
                GZ = cubie.GZ,
                FaceColors = cubie.FaceColors.Select(c => c.ToUInt32()).ToArray()
            };
        }

        public Cubie ToCubie()
        {
            var cubie = new Cubie(GX, GY, GZ);
            if (FaceColors.Length == cubie.FaceColors.Length)
            {
                for (int i = 0; i < FaceColors.Length; i++)
                    cubie.FaceColors[i] = Color.FromUInt32(FaceColors[i]);
            }

            return cubie;
        }
    }
}
