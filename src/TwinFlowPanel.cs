using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FivePhaseMotorTwin
{
    public sealed class TwinFlowPanel : Panel
    {
        private readonly string[] _titles = new string[]
        {
            "实时电机电流",
            "模型观测器基准",
            "残差特征提取",
            "ELM故障分类",
            "容错策略匹配"
        };

        private readonly string[] _details = new string[]
        {
            "ia=-- A",
            "ia*=-- A",
            "residual=--",
            "无故障",
            "健康矢量控制"
        };

        public TwinFlowPanel()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.PanelBack;
            MinimumSize = new Size(220, 230);
        }

        public void UpdateStatus(SimulationFrame frame)
        {
            _details[0] = frame.TwinPhysicalText;
            _details[1] = frame.TwinReferenceText;
            _details[2] = frame.TwinResidualText;
            _details[3] = frame.TwinFaultText;
            _details[4] = frame.TwinStrategyText;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(AppTheme.PanelBack);

            int margin = 10;
            int gap = 8;
            int boxH = 38;
            int width = Width - margin * 2;

            using (Font titleFont = AppTheme.Font(8.5f, FontStyle.Bold))
            using (Font detailFont = AppTheme.Font(7.4f, FontStyle.Regular))
            using (Pen border = new Pen(AppTheme.Border))
            using (Pen arrow = new Pen(Color.FromArgb(120, 128, 136), 1.3f))
            using (Brush textBrush = new SolidBrush(AppTheme.Text))
            using (Brush mutedBrush = new SolidBrush(AppTheme.MutedText))
            using (Brush normalBack = new SolidBrush(Color.White))
            {
                arrow.CustomEndCap = new AdjustableArrowCap(3, 4);
                for (int i = 0; i < _titles.Length; i++)
                {
                    int y = margin + i * (boxH + gap);
                    Rectangle rect = new Rectangle(margin, y, width, boxH);
                    g.FillRectangle(normalBack, rect);
                    g.DrawRectangle(border, rect);
                    g.DrawString(_titles[i], titleFont, textBrush, new RectangleF(rect.Left + 8, rect.Top + 4, rect.Width - 16, 14));
                    g.DrawString(Shorten(_details[i], 31), detailFont, mutedBrush, new RectangleF(rect.Left + 8, rect.Top + 20, rect.Width - 16, 14));

                    if (i < _titles.Length - 1)
                    {
                        int x = rect.Left + rect.Width / 2;
                        g.DrawLine(arrow, x, rect.Bottom + 1, x, rect.Bottom + gap - 2);
                    }
                }
            }
        }

        private static string Shorten(string value, int max)
        {
            if (value == null) return string.Empty;
            if (value.Length <= max) return value;
            return value.Substring(0, max - 2) + "..";
        }
    }
}
