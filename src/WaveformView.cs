using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
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
        private const double MinWindowSeconds = 0.5;
        private const double MaxWindowSeconds = 30.0;
        private const double HistorySeconds = 120.0;

        private readonly List<WaveSeries> _series = new List<WaveSeries>();
        private readonly List<WavePoint> _points = new List<WavePoint>();
        private readonly HashSet<string> _visibleKeys = new HashSet<string>();
        private string _title;
        private double? _faultTime;
        private double? _toleranceTime;
        private double _viewEndTime;
        private bool _followLive = true;
        private bool _dragging;
        private Point _dragStart;
        private Point _hoverPoint;
        private bool _hasHover;
        private double _dragStartViewEnd;

        public double WindowSeconds = 6.5;

        public WaveformView(string title, WaveSeries[] series)
        {
            _title = title;
            for (int i = 0; i < series.Length; i++)
            {
                _series.Add(series[i]);
                _visibleKeys.Add(series[i].Key);
            }
            BackColor = Color.White;
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            MinimumSize = new Size(520, 200);
            Margin = new Padding(0, 0, 0, 8);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public void SetVisibleSeries(string[] keys, string title)
        {
            _visibleKeys.Clear();
            for (int i = 0; i < keys.Length; i++) _visibleKeys.Add(keys[i]);
            if (!string.IsNullOrEmpty(title)) _title = title;
            Invalidate();
        }

        public void SetSeriesLabel(string key, string label)
        {
            for (int i = 0; i < _series.Count; i++)
            {
                if (_series[i].Key == key)
                {
                    _series[i].Label = label;
                    break;
                }
            }
            Invalidate();
        }

        public void ClearData()
        {
            _points.Clear();
            _faultTime = null;
            _toleranceTime = null;
            _viewEndTime = WindowSeconds;
            _followLive = true;
            _hasHover = false;
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
            while (_points.Count > 0 && _points[0].Time < time - HistorySeconds)
            {
                _points.RemoveAt(0);
            }
            if (_followLive) _viewEndTime = GetLatestTime();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle plot = GetPlotRectangle();
            if (e.Button == MouseButtons.Left && plot.Contains(e.Location))
            {
                Focus();
                _dragging = true;
                _dragStart = e.Location;
                _dragStartViewEnd = _viewEndTime;
                _followLive = false;
                Cursor = Cursors.Hand;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle plot = GetPlotRectangle();
            _hoverPoint = e.Location;
            _hasHover = plot.Contains(e.Location);

            if (_dragging)
            {
                double dx = e.X - _dragStart.X;
                double deltaSeconds = dx / Math.Max(1.0, plot.Width) * WindowSeconds;
                SetViewEnd(_dragStartViewEnd - deltaSeconds);
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hasHover = false;
            _dragging = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                _followLive = true;
                _viewEndTime = GetLatestTime();
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Rectangle plot = GetPlotRectangle();
            if (!plot.Contains(e.Location)) return;

            double minTime;
            double maxTime;
            GetViewRange(out minTime, out maxTime);
            double oldWindow = WindowSeconds;
            double ratio = (e.X - plot.Left) / Math.Max(1.0, (double)plot.Width);
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            double cursorTime = minTime + ratio * oldWindow;

            WindowSeconds = e.Delta > 0 ? WindowSeconds * 0.78 : WindowSeconds * 1.28;
            WindowSeconds = Math.Max(MinWindowSeconds, Math.Min(MaxWindowSeconds, WindowSeconds));

            if (_followLive)
            {
                _viewEndTime = GetLatestTime();
            }
            else
            {
                SetViewEnd(cursorTime + (1.0 - ratio) * WindowSeconds);
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

            Rectangle plot = GetPlotRectangle();
            Rectangle legend = new Rectangle(plot.Left, Height - 36, plot.Width, 28);
            double minTime;
            double maxTime;
            GetViewRange(out minTime, out maxTime);
            DrawGrid(g, plot, minTime, maxTime);
            DrawMarkers(g, plot, minTime, maxTime);
            DrawSeries(g, plot, minTime, maxTime);
            DrawCursorReadout(g, plot, minTime, maxTime);
            DrawLegend(g, legend);
        }

        private Rectangle GetPlotRectangle()
        {
            return new Rectangle(58, 36, Math.Max(80, Width - 76), Math.Max(70, Height - 82));
        }

        private void DrawGrid(Graphics g, Rectangle plot, double minTime, double maxTime)
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
                WaveSeries first = GetFirstVisibleSeries();
                if (commonUnit.Length > 0 && first != null)
                {
                    g.DrawString(first.Max.ToString("0.#") + commonUnit, small, muted, new PointF(4, plot.Top - 2));
                    g.DrawString(first.Min.ToString("0.#") + commonUnit, small, muted, new PointF(4, plot.Bottom - 12));
                }
                else
                {
                    g.DrawString("通道", small, muted, new PointF(18, plot.Top - 2));
                    g.DrawString("比例", small, muted, new PointF(18, plot.Bottom - 12));
                }

                double mid = (minTime + maxTime) / 2.0;
                g.DrawString(minTime.ToString("0.00") + " s", small, muted, new PointF(plot.Left - 2, plot.Bottom + 3));
                g.DrawString(mid.ToString("0.00") + " s", small, muted, new PointF(plot.Left + plot.Width / 2 - 24, plot.Bottom + 3));
                g.DrawString(maxTime.ToString("0.00") + " s", small, muted, new PointF(plot.Right - 48, plot.Bottom + 3));
                g.DrawString(_followLive ? "实时" : "历史", small, muted, new PointF(plot.Right - 34, plot.Top - 22));
            }
        }

        private void DrawMarkers(Graphics g, Rectangle plot, double minTime, double maxTime)
        {
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

        private void DrawSeries(Graphics g, Rectangle plot, double minTime, double maxTime)
        {
            if (_points.Count == 0) return;
            double span = Math.Max(0.001, maxTime - minTime);

            for (int s = 0; s < _series.Count; s++)
            {
                WaveSeries spec = _series[s];
                if (!_visibleKeys.Contains(spec.Key)) continue;
                List<PointF> line = new List<PointF>();
                for (int i = 0; i < _points.Count; i++)
                {
                    WavePoint p = _points[i];
                    if (p.Time < minTime || p.Time > maxTime) continue;
                    if (!p.Values.ContainsKey(spec.Key)) continue;
                    PointF point = ToPoint(plot, p.Time, p.Values[spec.Key], spec, minTime, span);
                    line.Add(point);
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

        private void DrawCursorReadout(Graphics g, Rectangle plot, double minTime, double maxTime)
        {
            if (!_hasHover || _points.Count == 0 || !plot.Contains(_hoverPoint)) return;

            double span = Math.Max(0.001, maxTime - minTime);
            double targetTime = minTime + (_hoverPoint.X - plot.Left) / Math.Max(1.0, (double)plot.Width) * span;
            WavePoint nearest = FindNearestPoint(targetTime, minTime, maxTime);
            if (nearest == null) return;

            int x = plot.Left + (int)((nearest.Time - minTime) / span * plot.Width);
            using (Pen cursor = new Pen(Color.FromArgb(95, 105, 115), 1.0f))
            {
                cursor.DashStyle = DashStyle.Dot;
                g.DrawLine(cursor, x, plot.Top, x, plot.Bottom);
            }

            StringBuilder text = new StringBuilder();
            text.Append("t=").Append(nearest.Time.ToString("0.000")).Append(" s");
            int valueCount = 0;
            for (int i = 0; i < _series.Count; i++)
            {
                WaveSeries spec = _series[i];
                if (!_visibleKeys.Contains(spec.Key)) continue;
                if (!nearest.Values.ContainsKey(spec.Key)) continue;
                PointF dot = ToPoint(plot, nearest.Time, nearest.Values[spec.Key], spec, minTime, span);
                using (Brush brush = new SolidBrush(spec.Color))
                {
                    g.FillEllipse(brush, dot.X - 3.5f, dot.Y - 3.5f, 7.0f, 7.0f);
                }
                text.Append(Environment.NewLine)
                    .Append(spec.Label)
                    .Append("=")
                    .Append(nearest.Values[spec.Key].ToString("0.000"))
                    .Append(spec.Unit);
                valueCount++;
                if (valueCount >= 6) break;
            }

            DrawReadoutBox(g, plot, text.ToString(), x);
        }

        private void DrawReadoutBox(Graphics g, Rectangle plot, string text, int cursorX)
        {
            using (Font font = AppTheme.Font(8.3f, FontStyle.Regular))
            using (Brush back = new SolidBrush(Color.FromArgb(248, 252, 255)))
            using (Brush brush = new SolidBrush(AppTheme.Text))
            using (Pen border = new Pen(Color.FromArgb(122, 145, 168)))
            {
                SizeF size = g.MeasureString(text, font);
                float x = cursorX + 10;
                float y = plot.Top + 10;
                if (x + size.Width + 14 > plot.Right) x = cursorX - size.Width - 18;
                RectangleF box = new RectangleF(x, y, size.Width + 10, size.Height + 8);
                g.FillRectangle(back, box);
                g.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
                g.DrawString(text, font, brush, box.X + 5, box.Y + 4);
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
                    if (!_visibleKeys.Contains(spec.Key)) continue;
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

        private PointF ToPoint(Rectangle plot, double time, double value, WaveSeries spec, double minTime, double span)
        {
            double norm = (value - spec.Min) / Math.Max(0.0001, spec.Max - spec.Min);
            norm = Math.Max(0.0, Math.Min(1.0, norm));
            float x = plot.Left + (float)((time - minTime) / span * plot.Width);
            float y = plot.Bottom - (float)(norm * plot.Height);
            return new PointF(x, y);
        }

        private WavePoint FindNearestPoint(double targetTime, double minTime, double maxTime)
        {
            WavePoint best = null;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < _points.Count; i++)
            {
                WavePoint p = _points[i];
                if (p.Time < minTime || p.Time > maxTime) continue;
                double distance = Math.Abs(p.Time - targetTime);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = p;
                }
            }
            return best;
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
            WaveSeries first = GetFirstVisibleSeries();
            if (first == null) return string.Empty;
            string unit = first.Unit == null ? string.Empty : first.Unit;
            for (int i = 0; i < _series.Count; i++)
            {
                WaveSeries spec = _series[i];
                if (!_visibleKeys.Contains(spec.Key)) continue;
                string other = spec.Unit == null ? string.Empty : spec.Unit;
                if (other != unit) return string.Empty;
            }
            return unit;
        }

        private WaveSeries GetFirstVisibleSeries()
        {
            for (int i = 0; i < _series.Count; i++)
            {
                if (_visibleKeys.Contains(_series[i].Key)) return _series[i];
            }
            return null;
        }

        private void GetViewRange(out double minTime, out double maxTime)
        {
            double latest = GetLatestTime();
            if (_followLive) _viewEndTime = latest;
            SetViewEnd(_viewEndTime);
            maxTime = Math.Max(WindowSeconds, _viewEndTime);
            minTime = maxTime - WindowSeconds;
            if (minTime < 0.0)
            {
                minTime = 0.0;
                maxTime = WindowSeconds;
            }
        }

        private void SetViewEnd(double value)
        {
            double latest = Math.Max(WindowSeconds, GetLatestTime());
            double oldest = GetOldestTime();
            double minEnd = oldest + WindowSeconds;
            if (minEnd > latest) minEnd = latest;
            _viewEndTime = Math.Max(minEnd, Math.Min(latest, value));
        }

        private double GetLatestTime()
        {
            if (_points.Count == 0) return WindowSeconds;
            return Math.Max(WindowSeconds, _points[_points.Count - 1].Time);
        }

        private double GetOldestTime()
        {
            if (_points.Count == 0) return 0.0;
            return Math.Max(0.0, _points[0].Time);
        }
    }
}
