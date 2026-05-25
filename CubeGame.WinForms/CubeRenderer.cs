namespace CubeGame;

public struct Vector3
{
    public float X, Y, Z;
    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
}

public struct Face
{
    public int[] Indices;
    public Color Color;
    public string Name;
    public float Depth;

    public Face(int[] indices, Color color, string name)
    {
        Indices = indices;
        Color = color;
        Name = name;
        Depth = 0;
    }
}

public class CubeRenderer
{
    private readonly Vector3[] _baseVertices;
    private Vector3[] _transformedVertices;
    private readonly Face[] _faces;
    private readonly PointF[] _projected;
    private static readonly Random Rng = new();

    public float AngleX { get; set; } = 0.3f;
    public float AngleY { get; set; } = 0.5f;
    public float Scale { get; set; } = 120f;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }

    private static readonly Color[] FaceColors =
    [
        Color.Red,
        Color.Green,
        Color.Blue,
        Color.Yellow,
        Color.Purple,
        Color.Orange
    ];

    private static readonly string[] FaceNames =
    ["Front", "Right", "Back", "Left", "Top", "Bottom"];

    public CubeRenderer()
    {
        float s = 1f;
        _baseVertices =
        [
            new(-s, -s, -s), new( s, -s, -s), new( s,  s, -s), new(-s,  s, -s),
            new(-s, -s,  s), new( s, -s,  s), new( s,  s,  s), new(-s,  s,  s),
        ];

        _transformedVertices = new Vector3[8];
        _projected = new PointF[8];

        _faces =
        [
            new(new[] { 0, 1, 2, 3 }, FaceColors[0], FaceNames[0]),
            new(new[] { 1, 5, 6, 2 }, FaceColors[1], FaceNames[1]),
            new(new[] { 5, 4, 7, 6 }, FaceColors[2], FaceNames[2]),
            new(new[] { 4, 0, 3, 7 }, FaceColors[3], FaceNames[3]),
            new(new[] { 3, 2, 6, 7 }, FaceColors[4], FaceNames[4]),
            new(new[] { 4, 5, 1, 0 }, FaceColors[5], FaceNames[5]),
        ];
    }

    public void Rotate(float deltaX, float deltaY)
    {
        AngleX += deltaX;
        AngleY += deltaY;
    }

    private static float Cos(float a) => MathF.Cos(a);
    private static float Sin(float a) => MathF.Sin(a);

    public void Transform()
    {
        float cx = Cos(AngleX), sx = Sin(AngleX);
        float cy = Cos(AngleY), sy = Sin(AngleY);

        for (int i = 0; i < _baseVertices.Length; i++)
        {
            var v = _baseVertices[i];
            float y1 = v.Y * cx - v.Z * sx;
            float z1 = v.Y * sx + v.Z * cx;
            float x2 = v.X * cy + z1 * sy;
            float z2 = -v.X * sy + z1 * cy;
            _transformedVertices[i] = new Vector3(x2, y1, z2);
        }

        foreach (var face in _faces)
        {
            float d = 0;
            foreach (int idx in face.Indices)
                d += _transformedVertices[idx].Z;
            face.Depth = d / face.Indices.Length;
        }

        Array.Sort(_faces, (a, b) => a.Depth.CompareTo(b.Depth));
    }

    public void Project(int width, int height)
    {
        OffsetX = width / 2f;
        OffsetY = height / 2f;

        for (int i = 0; i < _transformedVertices.Length; i++)
        {
            var v = _transformedVertices[i];
            float perspective = 4f / (4f - v.Z);
            _projected[i] = new PointF(
                v.X * Scale * perspective + OffsetX,
                -v.Y * Scale * perspective + OffsetY
            );
        }
    }

    public void Render(Graphics g, int width, int height)
    {
        Transform();
        Project(width, height);

        foreach (var face in _faces)
        {
            var pts = new PointF[face.Indices.Length];
            for (int i = 0; i < face.Indices.Length; i++)
                pts[i] = _projected[face.Indices[i]];

            using var brush = new SolidBrush(Color.FromArgb(180, face.Color));
            g.FillPolygon(brush, pts);

            using var pen = new Pen(Color.Black, 2);
            g.DrawPolygon(pen, pts);
        }
    }

    public string GetFrontFaceName()
    {
        float maxZ = float.MinValue;
        int frontIdx = 0;
        for (int i = 0; i < _faces.Length; i++)
        {
            float avgZ = 0;
            foreach (int idx in _faces[i].Indices)
                avgZ += _transformedVertices[idx].Z;
            avgZ /= _faces[i].Indices.Length;
            if (avgZ > maxZ)
            {
                maxZ = avgZ;
                frontIdx = i;
            }
        }
        return _faces[frontIdx].Name;
    }

    public static string GetRandomFaceName()
    {
        return FaceNames[Rng.Next(FaceNames.Length)];
    }
}
