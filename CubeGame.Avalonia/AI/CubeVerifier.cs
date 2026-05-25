using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// AI가 제안한 이동 수식이 큐브를 실제로 완성시키는지 시뮬레이션으로 검증합니다.
/// 실제 렌더링 없이 Cube3x3 로직만 사용합니다.
/// </summary>
public static class CubeVerifier
{
    // ── 퍼블릭 API ──────────────────────────────────────────────────

    /// <summary>
    /// 현재 큐브에 moves를 적용했을 때 완성 상태인지 검증합니다.
    /// </summary>
    public static bool VerifySolution(
        Cube3x3 currentCube,
        IEnumerable<RubikNotation.Move> moves)
    {
        var sim = Clone(currentCube);
        foreach (var m in moves)
            sim.RotateLayer(m.Axis, m.Layer, m.Clockwise);
        return IsSolved(sim);
    }

    /// <summary>큐브가 완성 상태(6면 단색)인지 확인합니다.</summary>
    public static bool IsSolved(Cube3x3 cube)
    {
        return IsFaceUniform(cube, FaceDir.Up,    c => c.GY ==  1) &&
               IsFaceUniform(cube, FaceDir.Down,  c => c.GY == -1) &&
               IsFaceUniform(cube, FaceDir.Front, c => c.GZ == -1) &&
               IsFaceUniform(cube, FaceDir.Back,  c => c.GZ ==  1) &&
               IsFaceUniform(cube, FaceDir.Left,  c => c.GX == -1) &&
               IsFaceUniform(cube, FaceDir.Right, c => c.GX ==  1);
    }

    // ── 내부 헬퍼 ───────────────────────────────────────────────────

    /// <summary>Cube3x3 의 깊은 복사본을 반환합니다.</summary>
    private static Cube3x3 Clone(Cube3x3 source)
    {
        // Cubie 딥카피 (FaceColors는 Color 값 타입 배열이므로 Clone() 이면 됨)
        var clonedCubies = source.AllCubies.Select(c =>
        {
            var copy = new Cubie(c.GX, c.GY, c.GZ)
            {
                FaceColors = (Color[])c.FaceColors.Clone()
            };
            return copy;
        });

        var sim = new Cube3x3();
        sim.Restore(clonedCubies);
        return sim;
    }

    /// <summary>특정 면(dir)의 스티커가 모두 같은 색인지 확인합니다.</summary>
    private static bool IsFaceUniform(
        Cube3x3 cube, FaceDir dir, Func<Cubie, bool> posFilter)
    {
        Color? reference = null;

        foreach (var cubie in cube.AllCubies.Where(posFilter))
        {
            var c = cubie.FaceColors[(int)dir];
            if (c.A == 0) continue;                    // 투명 스티커 = 해당 면 없음

            if (reference is null)
            {
                reference = c;
                continue;
            }

            // 색상 비교 (A 채널 무시, R/G/B 만 비교)
            if (c.R != reference.Value.R ||
                c.G != reference.Value.G ||
                c.B != reference.Value.B)
                return false;
        }

        return reference is not null; // 스티커가 하나도 없으면 false
    }
}
