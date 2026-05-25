using System.Collections.Generic;
using System.Text.RegularExpressions;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// 루빅스 큐브 표준 표기법(Rubik's notation) 파서.
///
/// 지원 기호:
///   면(Face): U D R L F B
///   수식(Suffix): ' = 역방향, 2 = 두 번
///   슬라이스: M(L방향), E(D방향), S(F방향)
///
/// 게임 내부 좌표 규칙 (Cube3x3 기준):
///   U  → (Y, +1, cw=true)   U' → (Y, +1, cw=false)
///   D  → (Y, -1, cw=false)  D' → (Y, -1, cw=true)
///   R  → (X, +1, cw=true)   R' → (X, +1, cw=false)
///   L  → (X, -1, cw=false)  L' → (X, -1, cw=true)
///   F  → (Z, -1, cw=false)  F' → (Z, -1, cw=true)
///   B  → (Z, +1, cw=true)   B' → (Z, +1, cw=false)
///   M  → (X,  0, cw=false)  E  → (Y,  0, cw=false)  S → (Z, 0, cw=false)
/// </summary>
public static class RubikNotation
{
    public record Move(LayerAxis Axis, int Layer, bool Clockwise);

    // 기본 이동 테이블 (prime 없는 단일 이동)
    private static readonly Dictionary<char, Move> BaseMap = new()
    {
        ['U'] = new(LayerAxis.Y,  1, true),
        ['D'] = new(LayerAxis.Y, -1, false),
        ['R'] = new(LayerAxis.X,  1, true),
        ['L'] = new(LayerAxis.X, -1, false),
        ['F'] = new(LayerAxis.Z, -1, false),
        ['B'] = new(LayerAxis.Z,  1, true),
        ['M'] = new(LayerAxis.X,  0, false),   // L과 같은 방향
        ['E'] = new(LayerAxis.Y,  0, false),   // D와 같은 방향
        ['S'] = new(LayerAxis.Z,  0, false),   // F와 같은 방향
    };

    // [UDRLFBMES] 뒤에 선택적으로 ' 또는 2 가 붙는 패턴
    private static readonly Regex TokenRe =
        new(@"[UDRLFBMES][w]?[2']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 문자열에서 이동 수식을 파싱해 게임 이동 목록을 반환합니다.
    /// AI 응답에 설명 텍스트가 섞여 있어도 안전하게 추출합니다.
    /// </summary>
    public static List<Move> Parse(string text)
    {
        var result = new List<Move>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // 2' 나 '2 같은 이상한 조합 정규화
        text = text.Replace("2'", "2").Replace("'2", "2");

        foreach (System.Text.RegularExpressions.Match m in TokenRe.Matches(text))
        {
            var raw = m.Value.ToUpperInvariant().Replace("W", ""); // Wide move 'w' 무시
            if (raw.Length == 0) continue;

            var face   = raw[0];
            bool prime  = raw.Contains('\'');
            bool doubleTurn = raw.Contains('2');

            if (!BaseMap.TryGetValue(face, out var baseMove)) continue;

            // prime이면 clockwise 반전
            var move = prime
                ? baseMove with { Clockwise = !baseMove.Clockwise }
                : baseMove;

            result.Add(move);
            if (doubleTurn) result.Add(move); // 2회전 = 같은 방향으로 한 번 더
        }

        return result;
    }

    /// <summary>게임 이동을 표준 표기법 문자열로 변환 (디버그/로그용)</summary>
    public static string ToNotation(Move move)
    {
        foreach (var (face, baseMove) in BaseMap)
        {
            if (baseMove.Axis == move.Axis && baseMove.Layer == move.Layer)
            {
                bool needPrime = (baseMove.Clockwise != move.Clockwise);
                return needPrime ? $"{face}'" : face.ToString();
            }
        }
        return "?";
    }

    /// <summary>이동 목록을 표기법 문자열로 변환 (디버그/로그용)</summary>
    public static string ToNotationString(IEnumerable<Move> moves)
    {
        var parts = new List<string>();
        foreach (var m in moves) parts.Add(ToNotation(m));
        return string.Join(" ", parts);
    }
}
