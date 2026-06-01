using System.Drawing;

namespace FivePhaseMotorTwin
{
    internal static class AppTheme
    {
        public static readonly Color AppBack = Color.FromArgb(238, 240, 242);
        public static readonly Color PanelBack = Color.FromArgb(248, 249, 250);
        public static readonly Color Border = Color.FromArgb(178, 184, 190);
        public static readonly Color Text = Color.FromArgb(34, 40, 46);
        public static readonly Color MutedText = Color.FromArgb(92, 102, 112);
        public static readonly Color Accent = Color.FromArgb(35, 95, 170);
        public static readonly Color Alarm = Color.FromArgb(196, 59, 46);
        public static readonly Color Recover = Color.FromArgb(28, 132, 82);
        public static readonly Color Warning = Color.FromArgb(214, 139, 38);
        public static readonly Color Grid = Color.FromArgb(224, 228, 232);

        public static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style);
        }
    }
}
