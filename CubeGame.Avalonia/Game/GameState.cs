using System;

namespace CubeGame.Avalonia.Game;

public class GameState
{
    private readonly Random _rng = new();
    private static readonly string[] FaceNames = ["Front", "Right", "Back", "Left", "Top", "Bottom"];

    public CubeMode Mode { get; private set; } = CubeMode.Standard;
    public bool AutoRotate { get; private set; } = true;
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int TotalAttempts { get; private set; }
    public int CorrectAttempts { get; private set; }
    public string TargetFace { get; private set; }
    public string LastResult { get; private set; } = "";

    public double Accuracy => TotalAttempts > 0 ? (double)CorrectAttempts / TotalAttempts * 100 : 0;
    public string ComboText => Combo >= 2 ? $"Combo x{Combo}!" : "";
    public string AccuracyText => $"{Accuracy:F1}% ({CorrectAttempts}/{TotalAttempts})";
    public string ModeText => Mode == CubeMode.Standard ? "Standard cube" : "Current cube";

    public GameState()
    {
        TargetFace = NextTarget();
    }

    public void Reset()
    {
        Score = Combo = TotalAttempts = CorrectAttempts = 0;
        LastResult = "";
        TargetFace = NextTarget();
    }

    public void SetMode(CubeMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        Reset();
    }

    public void ToggleAutoRotate()
    {
        AutoRotate = !AutoRotate;
    }

    public bool CheckMatch(string currentFace)
    {
        TotalAttempts++;
        if (currentFace == TargetFace)
        {
            Combo++;
            CorrectAttempts++;
            int bonus = Combo >= 5 ? 50 : Combo >= 3 ? 20 : 0;
            Score += 100 + bonus;
            LastResult = "Match!";
            TargetFace = NextTarget();
            return true;
        }
        Combo = 0;
        LastResult = "Miss!";
        return false;
    }

    private string NextTarget() => FaceNames[_rng.Next(FaceNames.Length)];
}
