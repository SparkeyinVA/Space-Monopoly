using System.Drawing.Drawing2D;
using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>
/// Draws the grid: planets (circles), stars (diamonds), company squares and the five squares on offer,
/// one of which flashes in turn like the original. Scales to fill the space it is given.
/// </summary>
internal sealed class BoardView : Control
{
    private static readonly Color Space = Color.FromArgb(6, 8, 24);
    private static readonly Color CellA = Color.FromArgb(18, 24, 58);
    private static readonly Color CellB = Color.FromArgb(24, 32, 74);
    private static readonly Color ChoiceFill = Color.FromArgb(40, 70, 140);
    private static readonly Color ChoiceEdge = Color.FromArgb(0, 229, 255);
    private static readonly Color FlashEdge = Color.Gold;

    private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = 300 };
    private Board? _board;
    private IReadOnlyList<Pos> _choices = [];
    private Pos? _marked;
    private int _flashIndex;

    public BoardView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Space;
        _flashTimer.Tick += (_, _) =>
        {
            _flashIndex++;
            Invalidate();
        };
        AccessibleName = "Game board";
        Name = "board";
    }

    /// <summary>When true, clicking an offered square raises <see cref="SquareClicked"/>.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AcceptClicks { get; set; }

    public event EventHandler<Pos>? SquareClicked;

    public void ShowBoard(Board? board, IReadOnlyList<Pos> choices, Pos? marked = null)
    {
        _board = board;
        _choices = choices;
        _marked = marked;
        _flashTimer.Enabled = choices.Count > 0;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _flashTimer.Dispose();
        base.Dispose(disposing);
    }

    private (float Cell, float Left, float Top) Geometry()
    {
        if (_board is null) return (0, 0, 0);
        float cell = Math.Max(4f, Math.Min((float)ClientSize.Width / _board.Width, (float)ClientSize.Height / _board.Height));
        float left = (ClientSize.Width - cell * _board.Width) / 2f;
        float top = (ClientSize.Height - cell * _board.Height) / 2f;
        return (cell, left, top);
    }

    private Pos? HitTest(Point pt)
    {
        if (_board is null) return null;
        var (cell, left, top) = Geometry();
        int x = (int)Math.Floor((pt.X - left) / cell);
        int y = (int)Math.Floor((pt.Y - top) / cell);
        var p = new Pos(x, y);
        return _board.InBounds(p) ? p : null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool overChoice = AcceptClicks && HitTest(e.Location) is Pos p && _choices.Contains(p);
        Cursor = overChoice ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && AcceptClicks && HitTest(e.Location) is Pos p && _choices.Contains(p))
            SquareClicked?.Invoke(this, p);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Space);
        if (_board is null) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var (cell, left, top) = Geometry();
        using var letterFont = new Font("Segoe UI", Math.Max(6f, cell * 0.38f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var numberFont = new Font("Segoe UI", Math.Max(6f, cell * 0.42f), FontStyle.Bold, GraphicsUnit.Pixel);

        for (int y = 0; y < _board.Height; y++)
        {
            for (int x = 0; x < _board.Width; x++)
            {
                var r = new RectangleF(left + x * cell, top + y * cell, cell, cell);
                using (var bg = new SolidBrush((x + y) % 2 == 0 ? CellA : CellB))
                    g.FillRectangle(bg, r);

                int value = _board[x, y];
                if (value == Board.Circle) DrawPlanet(g, r);
                else if (value == Board.Diamond) DrawStar(g, r);
                else if (value >= Board.FirstCompany) DrawCompany(g, r, Board.CompanyOf(value), letterFont);
            }
        }

        int flashing = _choices.Count == 0 ? -1 : _flashIndex % _choices.Count;
        for (int i = 0; i < _choices.Count; i++)
        {
            var p = _choices[i];
            var r = new RectangleF(left + p.X * cell, top + p.Y * cell, cell, cell);
            DrawChoice(g, r, i == flashing, i + 1, numberFont);
        }

        if (_marked is Pos m)
        {
            var r = new RectangleF(left + m.X * cell, top + m.Y * cell, cell, cell);
            using var pen = new Pen(Color.White, Math.Max(2f, cell / 10f)) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        }
    }

    private static void DrawPlanet(Graphics g, RectangleF r)
    {
        var body = RectangleF.Inflate(r, -r.Width * 0.18f, -r.Height * 0.18f);
        using var path = new GraphicsPath();
        path.AddEllipse(body);
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(body.X + body.Width * 0.35f, body.Y + body.Height * 0.35f),
            CenterColor = Color.FromArgb(255, 220, 140),
            SurroundColors = [Color.FromArgb(170, 80, 20)],
        };
        g.FillEllipse(brush, body);
    }

    private static void DrawStar(Graphics g, RectangleF r)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        float outer = r.Width * 0.36f, inner = r.Width * 0.10f;
        var points = new PointF[8];
        for (int i = 0; i < 8; i++)
        {
            double angle = Math.PI / 4 * i - Math.PI / 2;
            float radius = i % 2 == 0 ? outer : inner;
            points[i] = new PointF(cx + (float)Math.Cos(angle) * radius, cy + (float)Math.Sin(angle) * radius);
        }
        using var glow = new SolidBrush(Color.FromArgb(60, 200, 220, 255));
        g.FillEllipse(glow, cx - outer * 0.6f, cy - outer * 0.6f, outer * 1.2f, outer * 1.2f);
        g.FillPolygon(Brushes.White, points);
    }

    private static void DrawCompany(Graphics g, RectangleF r, int company, Font font)
    {
        var color = Color.FromArgb(Company.Colors[company]);
        var box = RectangleF.Inflate(r, -1f, -1f);
        using var fill = new SolidBrush(color);
        g.FillRectangle(fill, box);
        using var edge = new Pen(ControlPaint.Dark(color, 0.2f));
        g.DrawRectangle(edge, box.X, box.Y, box.Width - 1, box.Height - 1);

        // The letter helps tell companies apart regardless of colour vision.
        var text = Company.LetterFor(company).ToString();
        var textColor = Company.UsesDarkText(company) ? Color.FromArgb(160, 0, 0, 0) : Color.FromArgb(170, 255, 255, 255);
        using var textBrush = new SolidBrush(textColor);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, r, format);
    }

    private static void DrawChoice(Graphics g, RectangleF r, bool flashing, int number, Font font)
    {
        var box = RectangleF.Inflate(r, -1.5f, -1.5f);
        using var fill = new SolidBrush(ChoiceFill);
        g.FillRectangle(fill, box);
        using var pen = new Pen(flashing ? FlashEdge : ChoiceEdge, Math.Max(2f, r.Width / 9f));
        g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var textBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
        g.DrawString(number.ToString(), font, textBrush, r, format);
    }
}
