using Avalonia.Input;

namespace CubeGame.Avalonia.Game;

public class Input
{
    public bool Left { get; private set; }
    public bool Right { get; private set; }
    public bool Up { get; private set; }
    public bool Down { get; private set; }
    public bool Space { get; private set; }
    public bool ResetPressed { get; private set; }
    public bool HelpPressed { get; private set; }

    public void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left: Left = true; break;
            case Key.Right: Right = true; break;
            case Key.Up: Up = true; break;
            case Key.Down: Down = true; break;
            case Key.Space: Space = true; break;
            case Key.R: ResetPressed = true; break;
            case Key.H: HelpPressed = true; break;
        }
    }

    public void OnKeyUp(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left: Left = false; break;
            case Key.Right: Right = false; break;
            case Key.Up: Up = false; break;
            case Key.Down: Down = false; break;
            case Key.Space: Space = false; break;
            case Key.R: ResetPressed = false; break;
            case Key.H: HelpPressed = false; break;
        }
    }
}
