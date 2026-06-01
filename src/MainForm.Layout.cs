using System.Drawing;
using System.Windows.Forms;

namespace FivePhaseMotorTwin
{
    public partial class MainForm
    {
        private void InitializeUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = AppTheme.AppBack;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(232, 235, 238);
            header.Padding = new Padding(16, 8, 16, 6);
            Label title = new Label();
            title.Text = "基于数字孪生的五相电机故障诊断与容错控制上位机";
            title.Dock = DockStyle.Left;
            title.AutoSize = false;
            title.Width = 820;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Font = AppTheme.Font(16.0f, FontStyle.Bold);
            title.ForeColor = AppTheme.Text;
            Label subtitle = new Label();
            subtitle.Text = "上位机程序｜故障不停机 · 动力不中断";
            subtitle.Dock = DockStyle.Right;
            subtitle.AutoSize = false;
            subtitle.Width = 420;
            subtitle.TextAlign = ContentAlignment.MiddleRight;
            subtitle.Font = AppTheme.Font(10.0f, FontStyle.Regular);
            subtitle.ForeColor = AppTheme.MutedText;
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel middle = new TableLayoutPanel();
            middle.Dock = DockStyle.Fill;
            middle.Padding = new Padding(10, 8, 10, 4);
            middle.ColumnCount = 2;
            middle.RowCount = 1;
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286));
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(middle, 0, 1);

            FlowLayoutPanel left = BuildLeftPanel();
            middle.Controls.Add(left, 0, 0);
            middle.Controls.Add(BuildWavePanel(), 1, 0);
            root.Controls.Add(BuildBottomPanel(), 0, 2);
        }

        private FlowLayoutPanel BuildLeftPanel()
        {
            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.FlowDirection = FlowDirection.TopDown;
            left.WrapContents = false;
            left.AutoScroll = true;
            left.BackColor = AppTheme.AppBack;
            left.Padding = new Padding(0, 0, 8, 0);

            GroupBox control = CreateGroup("运行模式控制", 270, 350);
            control.Controls.Add(CreateCaption("电机对象", 14, 27));
            _topologyCombo = new ComboBox();
            _topologyCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _topologyCombo.Font = AppTheme.Font(9.0f, FontStyle.Regular);
            _topologyCombo.Items.Add("五相容错电机");
            _topologyCombo.Items.Add("三相小电机");
            _topologyCombo.SelectedIndex = 0;
            _topologyCombo.SelectedIndexChanged += OnTopologyChanged;
            control.Controls.Add(Place(_topologyCombo, 86, 24, 168, 28));

            control.Controls.Add(CreateCaption("故障类型", 14, 62));
            _faultCombo = new ComboBox();
            _faultCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _faultCombo.Font = AppTheme.Font(9.0f, FontStyle.Regular);
            _faultCombo.Items.Add("A 相绕组开路");
            _faultCombo.Items.Add("A 相上功率管开路");
            _faultCombo.Items.Add("A 相下功率管开路");
            _faultCombo.SelectedIndex = 0;
            _faultCombo.SelectedIndexChanged += OnFaultTypeChanged;
            control.Controls.Add(Place(_faultCombo, 86, 59, 168, 28));

            _autoToleranceCheck = new CheckBox();
            _autoToleranceCheck.Text = "诊断后自动投入容错";
            _autoToleranceCheck.Checked = true;
            _autoToleranceCheck.Location = new Point(14, 92);
            _autoToleranceCheck.Size = new Size(220, 22);
            _autoToleranceCheck.Font = AppTheme.Font(8.8f, FontStyle.Regular);
            _autoToleranceCheck.ForeColor = AppTheme.Text;
            _autoToleranceCheck.CheckedChanged += OnAutoToleranceChanged;
            control.Controls.Add(_autoToleranceCheck);

            TableLayoutPanel buttons = new TableLayoutPanel();
            buttons.ColumnCount = 2;
            buttons.RowCount = 4;
            buttons.Location = new Point(14, 120);
            buttons.Size = new Size(240, 158);
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int r = 0; r < 4; r++) buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            _startButton = CreateButton("开始运行", OnStartClicked);
            _pauseButton = CreateButton("暂停运行", OnPauseClicked);
            _resetButton = CreateButton("系统复位", OnResetClicked);
            _faultButton = CreateButton("注入故障", OnFaultClicked);
            _toleranceButton = CreateButton("投入容错", OnToleranceClicked);
            _autoButton = CreateButton("自动运行", OnAutoClicked);
            _exportButton = CreateButton("导出数据CSV", OnExportClicked);
            _screenshotButton = CreateButton("导出截图", OnScreenshotClicked);
            buttons.Controls.Add(_startButton, 0, 0);
            buttons.Controls.Add(_pauseButton, 1, 0);
            buttons.Controls.Add(_resetButton, 0, 1);
            buttons.Controls.Add(_faultButton, 1, 1);
            buttons.Controls.Add(_toleranceButton, 0, 2);
            buttons.Controls.Add(_autoButton, 1, 2);
            buttons.Controls.Add(_exportButton, 0, 3);
            buttons.Controls.Add(_screenshotButton, 1, 3);
            control.Controls.Add(buttons);
            Label hint = CreateSmallLabel("现场断开 A 相时保持自动容错开启；需要分步讲解时可关闭后手动投入。", 14, 288, 240, 44);
            control.Controls.Add(hint);
            left.Controls.Add(control);
            GroupBox metrics = CreateGroup("关键指标", 270, 168);
            metrics.Controls.Add(CreateCaption("系统状态", 14, 26));
            _stateValue = CreateValue("健康运行", 92, 26, 162, 18);
            metrics.Controls.Add(_stateValue);
            metrics.Controls.Add(CreateCaption("故障类型", 14, 56));
            _faultValue = CreateValue("无故障", 92, 56, 162, 18);
            metrics.Controls.Add(_faultValue);
            metrics.Controls.Add(CreateCaption("诊断响应", 14, 86));
            _diagTimeValue = CreateValue("--", 92, 86, 162, 18);
            metrics.Controls.Add(_diagTimeValue);
            metrics.Controls.Add(CreateCaption("容错恢复", 14, 116));
            _recoveryTimeValue = CreateValue("--", 92, 116, 162, 18);
            metrics.Controls.Add(_recoveryTimeValue);
            left.Controls.Add(metrics);

            GroupBox twin = CreateGroup("数字孪生模型状态", 270, 290);
            _twinPanel = new TwinFlowPanel();
            _twinPanel.Location = new Point(10, 24);
            _twinPanel.Size = new Size(248, 252);
            twin.Controls.Add(_twinPanel);
            left.Controls.Add(twin);

            return left;
        }

        private Control BuildWavePanel()
        {
            TableLayoutPanel waves = new TableLayoutPanel();
            waves.Dock = DockStyle.Fill;
            waves.BackColor = AppTheme.AppBack;
            waves.RowCount = 3;
            waves.ColumnCount = 1;
            waves.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            waves.RowStyles.Add(new RowStyle(SizeType.Percent, 29));
            waves.RowStyles.Add(new RowStyle(SizeType.Percent, 29));

            _currentView = new WaveformView("五相电流波形 ia / ib / ic / id / ie", new WaveSeries[]
            {
                new WaveSeries("ia", "ia", "A", Color.FromArgb(190, 44, 38), -18, 18),
                new WaveSeries("ib", "ib", "A", Color.FromArgb(32, 92, 170), -18, 18),
                new WaveSeries("ic", "ic", "A", Color.FromArgb(36, 130, 82), -18, 18),
                new WaveSeries("id", "id", "A", Color.FromArgb(214, 135, 32), -18, 18),
                new WaveSeries("ie", "ie", "A", Color.FromArgb(96, 82, 145), -18, 18)
            });

            _mechanicalView = new WaveformView("转速 Speed / 转矩 Torque / q 轴电流 iq", new WaveSeries[]
            {
                new WaveSeries("speed", "Speed", "rpm", Color.FromArgb(32, 92, 170), 1380, 1580),
                new WaveSeries("torque", "Torque", "Nm", Color.FromArgb(190, 44, 38), 24, 44),
                new WaveSeries("iq", "iq", "A", Color.FromArgb(36, 130, 82), 10, 28)
            });

            _faultView = new WaveformView("诊断残差 residual / 故障标志 fault_flag", new WaveSeries[]
            {
                new WaveSeries("residual", "residual", "", Color.FromArgb(214, 135, 32), 0, 1.2),
                new WaveSeries("flag", "fault_flag", "", Color.FromArgb(34, 40, 46), 0, 1.2)
            });

            waves.Controls.Add(_currentView, 0, 0);
            waves.Controls.Add(_mechanicalView, 0, 1);
            waves.Controls.Add(_faultView, 0, 2);
            return waves;
        }

        private Control BuildBottomPanel()
        {
            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.Padding = new Padding(10, 2, 10, 10);
            bottom.BackColor = AppTheme.AppBack;
            bottom.ColumnCount = 3;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            GroupBox diagnosis = CreateGroup("诊断结果", 100, 100);
            diagnosis.Dock = DockStyle.Fill;
            _diagnosisValue = new Label();
            _diagnosisValue.Dock = DockStyle.Fill;
            _diagnosisValue.Padding = new Padding(12, 20, 12, 8);
            _diagnosisValue.Font = AppTheme.Font(11.0f, FontStyle.Bold);
            _diagnosisValue.ForeColor = AppTheme.Text;
            diagnosis.Controls.Add(_diagnosisValue);
            bottom.Controls.Add(diagnosis, 0, 0);
            GroupBox strategy = CreateGroup("容错策略", 100, 100);
            strategy.Dock = DockStyle.Fill;
            _strategyValue = new Label();
            _strategyValue.Dock = DockStyle.Fill;
            _strategyValue.Padding = new Padding(12, 20, 12, 8);
            _strategyValue.Font = AppTheme.Font(10.5f, FontStyle.Bold);
            _strategyValue.ForeColor = AppTheme.Accent;
            strategy.Controls.Add(_strategyValue);
            bottom.Controls.Add(strategy, 1, 0);

            GroupBox log = CreateGroup("系统状态日志", 100, 100);
            log.Dock = DockStyle.Fill;
            _logText = new TextBox();
            _logText.Multiline = true;
            _logText.ReadOnly = true;
            _logText.ScrollBars = ScrollBars.Vertical;
            _logText.Dock = DockStyle.Fill;
            _logText.BorderStyle = BorderStyle.None;
            _logText.BackColor = Color.White;
            _logText.Font = new Font("Consolas", 9.0f, FontStyle.Regular);
            _logText.ForeColor = AppTheme.Text;
            _logText.Margin = new Padding(10);
            log.Controls.Add(_logText);
            bottom.Controls.Add(log, 2, 0);

            return bottom;
        }

        private GroupBox CreateGroup(string text, int width, int height)
        {
            GroupBox box = new GroupBox();
            box.Text = text;
            box.Width = width;
            box.Height = height;
            box.Margin = new Padding(0, 0, 8, 8);
            box.BackColor = AppTheme.PanelBack;
            box.ForeColor = AppTheme.Text;
            box.Font = AppTheme.Font(9.0f, FontStyle.Bold);
            return box;
        }

        private Button CreateButton(string text, System.EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(3);
            button.FlatStyle = FlatStyle.System;
            button.Font = AppTheme.Font(8.5f, FontStyle.Regular);
            button.Click += handler;
            return button;
        }

        private static Control Place(Control control, int x, int y, int width, int height)
        {
            control.Location = new Point(x, y);
            control.Size = new Size(width, height);
            return control;
        }

        private Label CreateCaption(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(76, 20);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = AppTheme.Font(8.5f, FontStyle.Regular);
            label.ForeColor = AppTheme.MutedText;
            return label;
        }

        private Label CreateValue(string text, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = AppTheme.Font(8.8f, FontStyle.Bold);
            label.ForeColor = AppTheme.Text;
            return label;
        }

        private Label CreateSmallLabel(string text, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.TextAlign = ContentAlignment.TopLeft;
            label.Font = AppTheme.Font(8.0f, FontStyle.Regular);
            label.ForeColor = AppTheme.MutedText;
            return label;
        }
    }
}


