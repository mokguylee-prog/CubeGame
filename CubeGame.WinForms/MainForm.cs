using System.Runtime.InteropServices;

namespace CubeGame;

public class MainForm : Form
{
    private readonly CubeRenderer _cube = new();
    private string _targetFace = CubeRenderer.GetRandomFaceName();
    private int _score;
    private int _combo;
    private int _totalAttempts;
    private int _correctAttempts;
    private bool _isRunning = true;
    private bool _showHelp;

    private readonly Label _scoreLabel;
    private readonly Label _targetLabel;
    private readonly Label _hintLabel;
    private readonly Label _accuracyLabel;
    private readonly Label _comboLabel;
    private readonly Label _helpLabel;

    private const float RotSpeed = 0.05f;
    private bool _keyLeft, _keyRight, _keyUp, _keyDown;

    [DllImport("gdi32.dll")]
    private static extern int CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    public MainForm()
    {
        Text = "3D Cube Game Simulator";
        Size = new Size(800, 640);
        MinimumSize = new Size(600, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 28, 36);
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 11, FontStyle.Bold);

        _scoreLabel = new Label
        {
            Location = new Point(20, 15),
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Text = "Score: 0"
        };

        _targetLabel = new Label
        {
            Location = new Point(20, 45),
            AutoSize = true,
            ForeColor = Color.Gold,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Text = ""
        };

        _accuracyLabel = new Label
        {
            Location = new Point(20, 78),
            AutoSize = true,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 10),
            Text = ""
        };

        _comboLabel = new Label
        {
            Location = new Point(20, 100),
            AutoSize = true,
            ForeColor = Color.Cyan,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Text = ""
        };

        _hintLabel = new Label
        {
            Location = new Point(20, 130),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9),
            Text = "Press SPACE to confirm current face"
        };

        _helpLabel = new Label
        {
            Location = new Point(20, Height - 120),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9),
            Text = "",
            MaximumSize = new Size(400, 200)
        };

        Controls.AddRange([_scoreLabel, _targetLabel, _accuracyLabel, _comboLabel, _hintLabel, _helpLabel]);

        KeyPreview = true;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Resize += (_, _) => UpdateHelpPosition();

        var timer = new Timer { Interval = 16 };
        timer.Tick += (_, _) => GameTick();
        timer.Start();

        NewTarget();

        Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16));
        Resize += (_, _) =>
        {
            Region?.Dispose();
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16));
        };
    }

    private void UpdateHelpPosition()
    {
        _helpLabel.Location = new Point(20, Height - 140);
    }

    private void NewTarget()
    {
        _targetFace = CubeRenderer.GetRandomFaceName();
        _targetLabel.Text = $"Match: {_targetFace}";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left: _keyLeft = true; break;
            case Keys.Right: _keyRight = true; break;
            case Keys.Up: _keyUp = true; break;
            case Keys.Down: _keyDown = true; break;
            case Keys.Space:
                CheckMatch();
                break;
            case Keys.R:
                _score = 0; _combo = 0; _totalAttempts = 0; _correctAttempts = 0;
                NewTarget();
                break;
            case Keys.H:
                _showHelp = !_showHelp;
                break;
            case Keys.Escape:
                Close();
                break;
        }
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left: _keyLeft = false; break;
            case Keys.Right: _keyRight = false; break;
            case Keys.Up: _keyUp = false; break;
            case Keys.Down: _keyDown = false; break;
        }
        e.Handled = true;
    }

    private void CheckMatch()
    {
        _totalAttempts++;
        string current = _cube.GetFrontFaceName();
        if (current == _targetFace)
        {
            _combo++;
            _correctAttempts++;
            int bonus = _combo >= 5 ? 50 : _combo >= 3 ? 20 : 0;
            int points = 100 + bonus;
            _score += points;
            _comboLabel.Text = _combo >= 2 ? $"Combo x{_combo}!" : "";
            NewTarget();
        }
        else
        {
            _combo = 0;
            _comboLabel.Text = "Miss!";
        }
        UpdateStats();
    }

    private void UpdateStats()
    {
        _scoreLabel.Text = $"Score: {_score}";
        double acc = _totalAttempts > 0 ? (double)_correctAttempts / _totalAttempts * 100 : 0;
        _accuracyLabel.Text = $"Accuracy: {acc:F1}% ({_correctAttempts}/{_totalAttempts})";
    }

    private void GameTick()
    {
        if (!_isRunning) return;

        if (_keyLeft) _cube.Rotate(0, -RotSpeed);
        if (_keyRight) _cube.Rotate(0, RotSpeed);
        if (_keyUp) _cube.Rotate(-RotSpeed, 0);
        if (_keyDown) _cube.Rotate(RotSpeed, 0);

        if (!_keyLeft && !_keyRight && !_keyUp && !_keyDown)
        {
            _cube.Rotate(0.003f, 0.005f);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.Clear(BackColor);

        var client = ClientRectangle;
        int size = Math.Min(client.Width, client.Height) - 60;
        int x = (client.Width - size) / 2 + 40;
        int y = (client.Height - size) / 2;

        using var outerBrush = new SolidBrush(Color.FromArgb(35, 35, 45));
        g.FillRoundedRect(outerBrush, x - 10, y - 10, size + 20, size + 20, 12);

        g.SetClip(new Rectangle(x - 10, y - 10, size + 20, size + 20));
        _cube.Render(g, x + size / 2, y + size / 2);
        g.ResetClip();

        string currentFace = _cube.GetFrontFaceName();
        using var faceBrush = new SolidBrush(Color.White);
        using var faceFont = new Font("Segoe UI", 10);
        g.DrawString($"Front: {currentFace}", faceFont, Brushes.LightGray, x + size / 2 - 40, y + size + 10);

        DrawSidePanel(g);

        if (_showHelp)
            DrawHelp(g);
    }

    private void DrawSidePanel(Graphics g)
    {
        var right = ClientRectangle.Right - 10;
        int y = 160;
        using var font = new Font("Segoe UI", 9);
        using var titleFont = new Font("Segoe UI", 10, FontStyle.Bold);

        g.DrawString("Controls", titleFont, Brushes.White, right - 140, y);
        y += 25;
        g.DrawString("Arrow Keys - Rotate", font, Brushes.LightGray, right - 140, y);
        y += 18;
        g.DrawString("SPACE - Match face", font, Brushes.LightGray, right - 140, y);
        y += 18;
        g.DrawString("R - Reset score", font, Brushes.LightGray, right - 140, y);
        y += 18;
        g.DrawString("H - Toggle help", font, Brushes.LightGray, right - 140, y);
        y += 18;
        g.DrawString("ESC - Exit", font, Brushes.LightGray, right - 140, y);
    }

    private void DrawHelp(Graphics g)
    {
        string help =
            "HOW TO PLAY\n\n" +
            "Rotate the 3D cube using Arrow Keys.\n" +
            "Match the target face shown at top-left.\n" +
            "Press SPACE to confirm the front face.\n\n" +
            "Build combos for bonus points!\n" +
            "3+ combo = +20 bonus\n" +
            "5+ combo = +50 bonus";

        using var font = new Font("Segoe UI", 10);
        using var bgBrush = new SolidBrush(Color.FromArgb(200, 20, 20, 30));
        var rect = new Rectangle(15, Height - 200, 350, 180);
        g.FillRoundedRect(bgBrush, rect, 8);
        g.DrawString(help, font, Brushes.White, rect.X + 10, rect.Y + 8);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}

public static class GraphicsExtensions
{
    public static void FillRoundedRect(this Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        float r = radius;
        float x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    public static void FillRoundedRect(this Graphics g, Brush brush, int x, int y, int w, int h, float r)
    {
        FillRoundedRect(g, brush, new RectangleF(x, y, w, h), r);
    }
}
