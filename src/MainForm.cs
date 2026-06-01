using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FivePhaseMotorTwin
{
    public partial class MainForm : Form
    {
        private readonly SimulationEngine _engine = new SimulationEngine();
        private readonly Timer _timer = new Timer();
        private bool _internalModeChange;

        private ComboBox _modeCombo;
        private Button _startButton;
        private Button _pauseButton;
        private Button _resetButton;
        private Button _faultButton;
        private Button _autoButton;
        private Button _screenshotButton;
        private Label _stateValue;
        private Label _faultValue;
        private Label _diagTimeValue;
        private Label _recoveryTimeValue;
        private Label _diagnosisValue;
        private Label _strategyValue;
        private TextBox _logText;
        private WaveformView _currentView;
        private WaveformView _mechanicalView;
        private WaveformView _faultView;
        private TwinFlowPanel _twinPanel;

        public MainForm()
        {
            Text = "基于数字孪生的五相电机故障诊断与容错控制上位机";
            BackColor = AppTheme.AppBack;
            Font = AppTheme.Font(9.0f, FontStyle.Regular);
            MinimumSize = new Size(1360, 820);
            Size = new Size(1600, 950);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            InitializeUi();
            _timer.Interval = 20;
            _timer.Tick += OnTimerTick;
            ResetSimulation(false);
        }

        private void OnStartClicked(object sender, EventArgs e)
        {
            if (!_timer.Enabled)
            {
                _timer.Start();
                Log("系统开始运行：" + _engine.Scenario.DisplayName + "。");
            }
        }

        private void OnPauseClicked(object sender, EventArgs e)
        {
            if (_timer.Enabled)
            {
                _timer.Stop();
                _pauseButton.Text = "继续运行";
                Log("运行暂停。");
            }
            else
            {
                _timer.Start();
                _pauseButton.Text = "暂停运行";
                Log("运行继续。");
            }
        }

        private void OnResetClicked(object sender, EventArgs e)
        {
            ResetSimulation(true);
        }

        private void OnFaultClicked(object sender, EventArgs e)
        {
            bool ok = _engine.InjectFaultNow();
            SyncComboToEngine();
            if (ok)
            {
                Log("故障注入完成：" + _engine.Scenario.FaultText + "。");
                Log("数字孪生残差异常：实测电流与基准电流偏差突增。");
                if (!_timer.Enabled) _timer.Start();
            }
            else
            {
                Log("当前故障已经注入，保持现有场景运行。");
            }
        }

        private void OnAutoClicked(object sender, EventArgs e)
        {
            ScenarioInfo current = ScenarioInfo.All[_modeCombo.SelectedIndex];
            ScenarioMode target = current.HasFault ? (current.HasTolerance ? current.Mode : ScenarioInfo.WithTolerance(current.Fault)) : ScenarioMode.WindingOpenTolerance;
            SetScenario(target);
            _engine.StartAutoDemo();
            ClearWaveforms();
            _timer.Start();
            Log("自动运行启动：正常运行 3 s -> 故障注入 2 s -> 容错控制投入 -> 稳定运行。");
        }

        private void OnScreenshotClicked(object sender, EventArgs e)
        {
            try
            {
                string root = GetProjectRoot();
                string folder = Path.Combine(root, "screenshots");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "run_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                using (Bitmap bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(path, ImageFormat.Png);
                }
                Log("已导出运行截图：" + path);
            }
            catch (Exception ex)
            {
                Log("截图导出失败：" + ex.Message);
            }
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            if (_internalModeChange || _modeCombo.SelectedIndex < 0) return;
            ScenarioInfo info = ScenarioInfo.All[_modeCombo.SelectedIndex];
            _engine.SetScenario(info.Mode);
            ResetSimulation(false);
            Log("场景切换：" + info.DisplayName + "。");
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            SimulationFrame frame = _engine.Step(_timer.Interval / 1000.0);
            AppendWaveforms(frame);
            UpdateStatus(frame);
            for (int i = 0; i < frame.Events.Count; i++) Log(frame.Events[i]);
        }

        private void ResetSimulation(bool log)
        {
            _timer.Stop();
            _pauseButton.Text = "暂停运行";
            _engine.Reset();
            ClearWaveforms();
            SimulationFrame frame = _engine.Step(0.0);
            AppendWaveforms(frame);
            UpdateStatus(frame);
            if (log) Log("系统复位，等待开始运行。");
        }

        private void SetScenario(ScenarioMode mode)
        {
            _engine.SetScenario(mode);
            SyncComboToEngine();
        }

        private void SyncComboToEngine()
        {
            _internalModeChange = true;
            for (int i = 0; i < ScenarioInfo.All.Length; i++)
            {
                if (ScenarioInfo.All[i].Mode == _engine.Scenario.Mode)
                {
                    _modeCombo.SelectedIndex = i;
                    break;
                }
            }
            _internalModeChange = false;
        }
        private void AppendWaveforms(SimulationFrame frame)
        {
            SignalSnapshot s = frame.Sample;
            _currentView.SetMarkers(frame.FaultTime, frame.ToleranceTime);
            _mechanicalView.SetMarkers(frame.FaultTime, frame.ToleranceTime);
            _faultView.SetMarkers(frame.FaultTime, frame.ToleranceTime);

            Dictionary<string, double> currents = new Dictionary<string, double>();
            currents["ia"] = s.Ia;
            currents["ib"] = s.Ib;
            currents["ic"] = s.Ic;
            currents["id"] = s.Id;
            currents["ie"] = s.Ie;
            _currentView.Append(s.Time, currents);

            Dictionary<string, double> mech = new Dictionary<string, double>();
            mech["speed"] = s.Speed;
            mech["torque"] = s.Torque;
            mech["iq"] = s.Iq;
            _mechanicalView.Append(s.Time, mech);

            Dictionary<string, double> fault = new Dictionary<string, double>();
            fault["residual"] = s.Residual;
            fault["flag"] = s.FaultFlag;
            _faultView.Append(s.Time, fault);
        }

        private void UpdateStatus(SimulationFrame frame)
        {
            _stateValue.Text = frame.RunningState;
            _faultValue.Text = frame.FaultType;
            _diagnosisValue.Text = frame.DiagnosisResult;
            _strategyValue.Text = frame.ControlStrategy;
            _diagTimeValue.Text = frame.DiagnosisTimeMs > 0.0 ? frame.DiagnosisTimeMs.ToString("0.00") + " ms  (指标 <= 1 ms)" : "--";
            _recoveryTimeValue.Text = frame.RecoveryTimeMs > 0.0 ? frame.RecoveryTimeMs.ToString("0.0") + " ms  (约 50 ms)" : "--";
            _twinPanel.UpdateStatus(frame);
        }

        private void ClearWaveforms()
        {
            _currentView.ClearData();
            _mechanicalView.ClearData();
            _faultView.ClearData();
        }

        private void Log(string message)
        {
            if (_logText.TextLength > 16000) _logText.Clear();
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine;
            _logText.AppendText(line);
            _logText.SelectionStart = _logText.TextLength;
            _logText.ScrollToCaret();
        }

        private static string GetProjectRoot()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            if (dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) && dir.Parent != null)
            {
                return dir.Parent.FullName;
            }
            return baseDir;
        }
    }
}

