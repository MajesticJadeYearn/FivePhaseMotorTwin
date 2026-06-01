using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FivePhaseMotorTwin
{
    public sealed class WaveSeries
    {
        public string Key;
        public string Label;
        public string Unit;
        public Color Color;
        public double Min;
        public double Max;
        public float Width;

        public WaveSeries(string key, string label, string unit, Color color, double min, double max)
        {
            Key = key;
            Label = label;
            Unit = unit;
            Color = color;
            Min = min;
            Max = max;
            Width = 2.6f;
        }
    }

    internal sealed class WavePoint
    {
        public double Time;
        public Dictionary<string, double> Values;

        public WavePoint(double time, Dictionary<string, double> values)
        {
            Time = time;
            Values = values;
        }
    }

    public sealed class WaveformView : Panel
    {
        private readonly List<WaveSeries> _series = new List<WaveSeries>();
        private readonly List<WavePoint> _points = new List<WavePoint>();
        private readonly string _title;
        private double? _faultTime;
        private double? _toleranceTime;

        public double WindowSeconds = 6.5;

        public WaveformView(string title, WaveSeries[] series)
        {
            _title = title;
            for (int i = 0; i < series.Length; i++) _series.Add(series[i]);
            BackColor = Color.White;
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            MinimumSize = new Size(520, 200);
            Margin = new Padding(0, 0, 0, 8);
        }

        public void ClearData()
        {
            _points.Clear();
            _faultTime = null;
            _toleranceTime = null;
            Invalidate();
        }

        public void SetMarkers(double? faultTime, double? toleranceTime)
        {
            _faultTime = faultTime;
            _toleranceTime = toleranceTime;
        }

        public void Append(double time, Dictionary<string, double> values)
        {
            _points.Add(new WavePoint(time, values));
            while (_points.Count > 0 && _points[0].Time < time - WindowSeconds - 1.0)
            {
                _points.RemoveAt(0);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (Pen border = new Pen(AppTheme.Border)) g.DrawRectangle(border, outer);

            using (Font titleFont = AppTheme.Font(10.8f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(AppTheme.Text))
            {
                g.DrawString(_title, titleFont, textBrush, new PointF(12, 8));
            }

            Rectangle plot = new Rectangle(58, 36, Math.Max(80, Width - 76), Math.Max(70, Height - 82));
            Rectangle legend = new Rectangle(plot.Left, Height - 36, plot.Width, 28);
            DrawGrid(g, plot);
            DrawMarkers(g, plot);
            DrawSeries(g, plot);
            DrawLegend(g, legend);
        }

        private void DrawGrid(Graphics g, Rectangle plot)
        {
            using (Pen grid = new Pen(AppTheme.Grid))
            using (Pen axis = new Pen(Color.FromArgb(142, 150, 158), 1.2f))
            using (Font small = AppTheme.Font(8.2f, FontStyle.Regular))
            using (Brush muted = new SolidBrush(AppTheme.MutedText))
            {
                for (int i = 0; i <= 8; i++)
                {
                    int x = plot.Left + (int)(plot.Width * i / 8.0);
                    g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                }
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Top + (int)(plot.Height * i / 4.0);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                }
                g.DrawRectangle(axis, plot);

                string commonUnit = GetCommonUnit();
                if (commonUnit.Length > 0 && _series.Count > 0)
                {
                    g.DrawString(_series[0].Max.ToString("0.#") + commonUnit, small, muted, new PointF(4, plot.Top - 2));
                    g.DrawString(_series[0].Min.ToString("0.#") + commonUnit, small, muted, new PointF(4, plot.Bottom - 12));
                }
                else
                {
                    g.DrawString("通道", small, muted, new PointF(18, plot.Top - 2));
                    g.DrawString("比例", small, muted, new PointF(18, plot.Bottom - 12));
                }
                g.DrawString("-" + WindowSeconds.ToString("0.0") + " s", small, muted, new PointF(plot.Left - 2, plot.Bottom + 3));
                g.DrawString("实时 / s", small, muted, new PointF(plot.Right - 54, plot.Bottom + 3));
            }
        }

        private void DrawMarkers(Graphics g, Rectangle plot)
        {
            double maxTime = GetMaxTime();
            double minTime = Math.Max(0.0, maxTime - WindowSeconds);
            DrawMarker(g, plot, _faultTime, minTime, maxTime, AppTheme.Alarm, "故障注入");
            DrawMarker(g, plot, _toleranceTime, minTime, maxTime, AppTheme.Recover, "容错投入");
        }

        private void DrawMarker(Graphics g, Rectangle plot, double? marker, double minTime, double maxTime, Color color, string text)
        {
            if (!marker.HasValue) return;
            if (marker.Value < minTime || marker.Value > maxTime) return;
            double span = Math.Max(0.001, maxTime - minTime);
            int x = plot.Left + (int)((marker.Value - minTime) / span * plot.Width);
            using (Pen pen = new Pen(color, 2.2f))
            using (SolidBrush back = new SolidBrush(Color.FromArgb(245, color)))
            using (SolidBrush brush = new SolidBrush(color))
            using (Font font = AppTheme.Font(8.5f, FontStyle.Bold))
            using (Pen boxPen = new Pen(color, 1.0f))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
                SizeF size = g.MeasureString(text, font);
                RectangleF box = new RectangleF(x + 6, plot.Top + 6, size.Width + 10, size.Height + 5);
                if (box.Right > plot.Right) box.X = x - box.Width - 6;
                g.FillRectangle(back, box);
                g.DrawRectangle(boxPen, box.X, box.Y, box.Width, box.Height);
                g.DrawString(text, font, brush, box.X + 5, box.Y + 2);
            }
        }

        private void DrawSeries(Graphics g, Rectangle plot)
        {
            if (_points.Count == 0) return;
            double maxTime = GetMaxTime();
            double minTime = Math.Max(0.0, maxTime - WindowSeconds);
            double span = Math.Max(0.001, maxTime - minTime);

            for (int s = 0; s < _series.Count; s++)
            {
                WaveSeries spec = _series[s];
                List<PointF> line = new List<PointF>();
                for (int i = 0; i < _points.Count; i++)
                {
                    WavePoint p = _points[i];
                    if (p.Time < minTime) continue;
                    if (!p.Values.ContainsKey(spec.Key)) continue;
                    double value = p.Values[spec.Key];
                    double norm = (value - spec.Min) / Math.Max(0.0001, spec.Max - spec.Min);
                    norm = Math.Max(0.0, Math.Min(1.0, norm));
                    float x = plot.Left + (float)((p.Time - minTime) / span * plot.Width);
                    float y = plot.Bottom - (float)(norm * plot.Height);
                    line.Add(new PointF(x, y));
                }
                if (line.Count > 1)
                {
                    using (Pen pen = new Pen(spec.Color, spec.Width))
                    {
                        pen.LineJoin = LineJoin.Round;
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLines(pen, line.ToArray());
                    }
                }
            }
        }

        private void DrawLegend(Graphics g, Rectangle legend)
        {
            using (Font font = AppTheme.Font(8.7f, FontStyle.Regular))
            using (Brush text = new SolidBrush(AppTheme.Text))
            {
                int x = legend.Left;
                int y = legend.Top + 4;
                for (int i = 0; i < _series.Count; i++)
                {
                    WaveSeries spec = _series[i];
                    string label = spec.Label + "  " + GetLatestValue(spec);
                    SizeF size = g.MeasureString(label, font);
                    int itemWidth = (int)size.Width + 34;
                    if (x + itemWidth > legend.Right && x > legend.Left)
                    {
                        x = legend.Left;
                        y += 16;
                    }
                    using (Pen pen = new Pen(spec.Color, 3.0f))
                    {
                        g.DrawLine(pen, x, y + 8, x + 20, y + 8);
                    }
                    g.DrawString(label, font, text, new PointF(x + 25, y));
                    x += itemWidth + 14;
                }
            }
        }

        private string GetLatestValue(WaveSeries spec)
        {
            if (_points.Count == 0) return "--";
            WavePoint p = _points[_points.Count - 1];
            if (!p.Values.ContainsKey(spec.Key)) return "--";
            return p.Values[spec.Key].ToString("0.00") + spec.Unit;
        }

        private string GetCommonUnit()
        {
            if (_series.Count == 0) return string.Empty;
            string unit = _series[0].Unit == null ? string.Empty : _series[0].Unit;
            for (int i = 1; i < _series.Count; i++)
            {
                string other = _series[i].Unit == null ? string.Empty : _series[i].Unit;
                if (other != unit) return string.Empty;
            }
            return unit;
        }

        private double GetMaxTime()
        {
            if (_points.Count == 0) return WindowSeconds;
            return Math.Max(WindowSeconds, _points[_points.Count - 1].Time);
        }
    }
}
