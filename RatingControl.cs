using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    // A clickable 5-star rating control, half-star precision (matching MusicBee's
    // own 0/0.5/1/.../5 rating scale). Geometry and colors replicate MusicBee's
    // native Track Information panel (decompiled from MusicBee.exe v3.6): each
    // star is a 10-point polygon (outer radius 7.3, inner 3.1, centered at
    // 6.5,6) clipped to a 13x12 cell; cells sit at x = 1, 14, 27, 40, 53 in a
    // 68x12 strip; filled = foreground, empty = 25% foreground over background;
    // a half star is the left 7px of a filled star drawn over an empty one. All
    // sizes are at 96 DPI, scaled like MusicBee does (only above 100 DPI).
    internal class RatingControl : Control
    {
        private const int StarCount = 5;

        private double _rating;
        private double _hoverRating = -1;

        public event Action<double> RatingChanged;

        public double Rating
        {
            get => _rating;
            set { _rating = value; Invalidate(); }
        }

        public RatingControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Size = PreferredStripSize;
        }

        private float DpiScale => DeviceDpi > 100 ? DeviceDpi / 96f : 1f;

        private int Dpi(int n) => (int)Math.Round(n * DpiScale);

        private int CellWidth => Dpi(13);
        private int CellHeight => Dpi(12);
        private int FirstCellX => (Dpi(68) - StarCount * CellWidth) / 2;

        public Size PreferredStripSize => new Size(Dpi(68), Dpi(12));

        private double DisplayRating => _hoverRating >= 0 ? _hoverRating : _rating;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            Color empty = Blend(ForeColor, BackColor, 0.25);
            double display = DisplayRating;
            int top = (Height - CellHeight) / 2;
            int halfWidth = (CellWidth + 1) / 2;

            for (int i = 0; i < StarCount; i++)
            {
                var cell = new Rectangle(FirstCellX + i * CellWidth, top, CellWidth, CellHeight);

                if (display >= i + 1)
                {
                    DrawStar(g, cell, ForeColor, cell.Width);
                }
                else
                {
                    DrawStar(g, cell, empty, cell.Width);
                    if (display >= i + 0.5)
                        DrawStar(g, cell, ForeColor, halfWidth);
                }
            }
        }

        private void DrawStar(Graphics g, Rectangle cell, Color color, int clipWidth)
        {
            var state = g.Save();
            g.SetClip(new Rectangle(cell.X, cell.Y, clipWidth, cell.Height), CombineMode.Intersect);

            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, CreateStarPoints(cell.X + 6.5f * DpiScale, cell.Y + 6f * DpiScale));

            g.Restore(state);
        }

        private PointF[] CreateStarPoints(float cx, float cy)
        {
            float outerR = 7.3f * DpiScale;
            float innerR = 3.1f * DpiScale;
            var points = new PointF[10];

            for (int i = 0; i < 10; i++)
            {
                double angle = Math.PI / 2 + i * Math.PI / 5;
                float r = (i % 2 == 0) ? outerR : innerR;
                points[i] = new PointF(cx - (float)(r * Math.Cos(angle)), cy - (float)(r * Math.Sin(angle)));
            }

            return points;
        }

        private static Color Blend(Color fore, Color back, double foreAmount)
        {
            return Color.FromArgb(
                (int)Math.Round(fore.R * foreAmount + back.R * (1 - foreAmount)),
                (int)Math.Round(fore.G * foreAmount + back.G * (1 - foreAmount)),
                (int)Math.Round(fore.B * foreAmount + back.B * (1 - foreAmount)));
        }

        private double PositionToRating(int x)
        {
            int local = x - FirstCellX;

            // Like the native widget: the far left edge (before/at the start of
            // the first star) means no rating.
            if (local < CellWidth / 4)
                return 0;

            int starIndex = Math.Max(0, Math.Min(StarCount - 1, local / CellWidth));
            double fraction = (local - starIndex * CellWidth) / (double)CellWidth;
            double value = starIndex + (fraction < 0.5 ? 0.5 : 1.0);
            return Math.Max(0, Math.Min(StarCount, value));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            double newHover = PositionToRating(e.X);
            if (Math.Abs(newHover - _hoverRating) > 0.001)
            {
                _hoverRating = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverRating = -1;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            double newRating = PositionToRating(e.X);
            _rating = newRating;
            Invalidate();
            RatingChanged?.Invoke(newRating);
        }
    }
}
