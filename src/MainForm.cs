using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace FivePhaseMotorTwin
{
    public partial class MainForm : Form
    {
        private readonly SimulationEngine _engine = new SimulationEngine();
        private readonly Timer _timer = new Timer();
        private readonly List<SimulationFrame> _history = new List<SimulationFrame>();
        private readonly object _serialLock = new object();
        private readonly Queue<SignalSnapshot> _serialSamples = new Queue<SignalSnapshot>();
        private readonly StringBuilder _serialBuffer = new StringBuilder();
        private SerialPort _serialPort;

        private ComboBox _topologyCombo;
        private ComboBox _faultCombo;
        private ComboBox _serialPortCombo;
        private ComboBox _baudCombo;
        private CheckBox _autoToleranceCheck;
        private Button _refreshPortsButton;
        private Button _connectSerialButton;
        private Label _serialStatusValue;
        private Button _startButton;
        private Button _pauseButton;
        private Button _resetButton;
        private Button _faultButton;
        private Button _toleranceButton;
        private Button _autoButton;
        private Button _exportButton;
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
            ConfigureCurrentWaveView();
            RefreshSerialPorts();
            _timer.Interval = 20;
            _timer.Tick += OnTimerTick;
            FormClosing += OnMainFormClosing;
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
            _engine.SetAutoToleranceEnabled(IsAutoToleranceEnabled());
            _engine.SelectFault(GetSelectedFault());
            bool ok = _engine.InjectFaultNow();
            if (ok)
            {
                Log("故障注入完成：" + _engine.Scenario.FaultText + "。");
                Log("数字孪生残差异常：实测电流与基准电流偏差突增。");
                if (_engine.AutoToleranceEnabled) Log("诊断完成后将自动投入容错控制。");
                if (!_timer.Enabled) _timer.Start();
            }
            else
            {
                Log("当前故障已经注入，保持现有场景运行。");
            }
        }

        private void OnToleranceClicked(object sender, EventArgs e)
        {
            _engine.SetAutoToleranceEnabled(IsAutoToleranceEnabled());
            _engine.SelectFault(GetSelectedFault());
            bool ok = _engine.ActivateToleranceNow();
            if (ok)
            {
                Log("容错控制投入：" + _engine.Scenario.StrategyText + "。");
                if (!_timer.Enabled) _timer.Start();
            }
            else
            {
                Log("容错控制已经投入，保持当前策略运行。");
            }
        }

        private void OnAutoClicked(object sender, EventArgs e)
        {
            _engine.SetAutoToleranceEnabled(true);
            if (_autoToleranceCheck != null) _autoToleranceCheck.Checked = true;
            _engine.SelectFault(GetSelectedFault());
            _engine.StartAutoDemo();
            _history.Clear();
            ClearWaveforms();
            _timer.Start();
            Log("自动运行启动：" + GetTopologyText() + "，" + _engine.Scenario.FaultText + "，正常运行 3 s -> 故障注入 2 s -> 容错控制投入 -> 稳定运行。");
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

        private void OnExportClicked(object sender, EventArgs e)
        {
            try
            {
                if (_history.Count == 0)
                {
                    Log("暂无可导出的运行数据。");
                    return;
                }

                string root = GetProjectRoot();
                string folder = Path.Combine(root, "exports");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "waveform_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)))
                {
                    writer.WriteLine("time_s,source,topology,ia_A,ib_A,ic_A,id_A,ie_A,speed_rpm,torque_Nm,iq_A,residual,fault_flag,running_state,fault_type,diagnosis,control_strategy");
                    for (int i = 0; i < _history.Count; i++)
                    {
                        SimulationFrame frame = _history[i];
                        SignalSnapshot s = frame.Sample;
                        writer.Write(s.Time.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(Csv(frame.SourceText));
                        writer.Write(",");
                        writer.Write(Csv(frame.TopologyText));
                        writer.Write(",");
                        writer.Write(s.Ia.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Ib.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Ic.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Id.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Ie.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Speed.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Torque.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Iq.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.Residual.ToString("0.0000"));
                        writer.Write(",");
                        writer.Write(s.FaultFlag.ToString("0"));
                        writer.Write(",");
                        writer.Write(Csv(frame.RunningState));
                        writer.Write(",");
                        writer.Write(Csv(frame.FaultType));
                        writer.Write(",");
                        writer.Write(Csv(frame.DiagnosisResult));
                        writer.Write(",");
                        writer.WriteLine(Csv(frame.ControlStrategy));
                    }
                }
                Log("已导出运行数据：" + path);
            }
            catch (Exception ex)
            {
                Log("数据导出失败：" + ex.Message);
            }
        }

        private void OnTopologyChanged(object sender, EventArgs e)
        {
            _engine.SetTopology(GetSelectedTopology());
            ConfigureCurrentWaveView();
            ResetSimulation(false);
            if (IsSerialConnected()) _timer.Start();
            Log("电机对象切换：" + GetTopologyText() + "。");
        }

        private void OnFaultTypeChanged(object sender, EventArgs e)
        {
            _engine.SelectFault(GetSelectedFault());
            ResetSimulation(false);
            if (IsSerialConnected()) _timer.Start();
            Log("故障类型切换：" + GetFaultText(GetSelectedFault()) + "。");
        }

        private void OnAutoToleranceChanged(object sender, EventArgs e)
        {
            _engine.SetAutoToleranceEnabled(IsAutoToleranceEnabled());
            Log(IsAutoToleranceEnabled() ? "诊断后自动投入容错：开启。" : "诊断后自动投入容错：关闭，可使用单独按钮投入容错。");
        }

        private void OnRefreshPortsClicked(object sender, EventArgs e)
        {
            RefreshSerialPorts();
            Log("串口列表已刷新。");
        }

        private void OnSerialConnectClicked(object sender, EventArgs e)
        {
            if (IsSerialConnected())
            {
                CloseSerialPort();
                Log("实时串口数据源已断开，回到仿真数据模式。");
                return;
            }

            if (_serialPortCombo == null || _serialPortCombo.SelectedItem == null)
            {
                Log("没有可连接的串口，请先刷新串口列表。");
                return;
            }

            try
            {
                ResetSimulation(false);
                ClearSerialQueue();
                int baud = 115200;
                int.TryParse(_baudCombo.Text, out baud);
                _serialPort = new SerialPort(_serialPortCombo.SelectedItem.ToString(), baud);
                _serialPort.NewLine = "\n";
                _serialPort.Encoding = Encoding.ASCII;
                _serialPort.ReadTimeout = 50;
                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.Open();
                _connectSerialButton.Text = "断开串口";
                SetSerialStatus("已连接 " + _serialPort.PortName + " @ " + baud.ToString());
                _timer.Start();
                Log("实时串口数据源已连接：" + _serialPort.PortName + "，等待控制器数据。");
            }
            catch (Exception ex)
            {
                CloseSerialPort();
                Log("串口连接失败：" + ex.Message);
            }
        }

        private void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            CloseSerialPort();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (IsSerialConnected())
            {
                ProcessSerialSamples();
                return;
            }

            SimulationFrame frame = _engine.Step(_timer.Interval / 1000.0);
            RecordFrame(frame);
            AppendWaveforms(frame);
            UpdateStatus(frame);
            for (int i = 0; i < frame.Events.Count; i++) Log(frame.Events[i]);
        }

        private void ProcessSerialSamples()
        {
            int processed = 0;
            SignalSnapshot sample;
            while (processed < 40 && TryDequeueSerialSample(out sample))
            {
                SimulationFrame frame = _engine.StepExternal(sample, _timer.Interval / 1000.0);
                RecordFrame(frame);
                AppendWaveforms(frame);
                UpdateStatus(frame);
                for (int i = 0; i < frame.Events.Count; i++) Log(frame.Events[i]);
                processed++;
            }

            if (processed > 0)
            {
                SetSerialStatus("接收中 " + _serialPort.PortName + "，缓存 " + GetSerialQueueCount().ToString());
            }
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string chunk = _serialPort.ReadExisting();
                if (string.IsNullOrEmpty(chunk)) return;
                lock (_serialLock)
                {
                    _serialBuffer.Append(chunk);
                    ExtractSerialLinesLocked();
                }
            }
            catch
            {
            }
        }

        private void ExtractSerialLinesLocked()
        {
            while (true)
            {
                string buffer = _serialBuffer.ToString();
                int indexN = buffer.IndexOf('\n');
                int indexR = buffer.IndexOf('\r');
                int index;
                if (indexN < 0) index = indexR;
                else if (indexR < 0) index = indexN;
                else index = Math.Min(indexN, indexR);
                if (index < 0) break;

                string line = buffer.Substring(0, index).Trim();
                _serialBuffer.Remove(0, index + 1);
                if (line.Length == 0) continue;

                SignalSnapshot sample;
                if (SerialTelemetryParser.TryParse(line, out sample))
                {
                    _serialSamples.Enqueue(sample);
                    while (_serialSamples.Count > 5000) _serialSamples.Dequeue();
                }
            }
        }

        private bool TryDequeueSerialSample(out SignalSnapshot sample)
        {
            lock (_serialLock)
            {
                if (_serialSamples.Count > 0)
                {
                    sample = _serialSamples.Dequeue();
                    return true;
                }
            }
            sample = null;
            return false;
        }

        private int GetSerialQueueCount()
        {
            lock (_serialLock)
            {
                return _serialSamples.Count;
            }
        }

        private void ClearSerialQueue()
        {
            lock (_serialLock)
            {
                _serialSamples.Clear();
                _serialBuffer.Length = 0;
            }
        }

        private void ResetSimulation(bool log)
        {
            _timer.Stop();
            _pauseButton.Text = "暂停运行";
            _engine.Reset();
            _history.Clear();
            ClearWaveforms();
            SimulationFrame frame = _engine.Step(0.0);
            RecordFrame(frame);
            AppendWaveforms(frame);
            UpdateStatus(frame);
            if (log) Log("系统复位，等待开始运行。");
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

        private void RecordFrame(SimulationFrame frame)
        {
            _history.Add(frame);
            if (_history.Count > 60000)
            {
                _history.RemoveRange(0, _history.Count - 60000);
            }
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

        private void ConfigureCurrentWaveView()
        {
            if (_currentView == null) return;
            if (_engine.Topology == MotorTopology.ThreePhase)
            {
                _currentView.SetVisibleSeries(new string[] { "ia", "ib", "ic" }, "三相电流波形 ia / ib / ic");
            }
            else
            {
                _currentView.SetVisibleSeries(new string[] { "ia", "ib", "ic", "id", "ie" }, "五相电流波形 ia / ib / ic / id / ie");
            }
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

        private void RefreshSerialPorts()
        {
            if (_serialPortCombo == null) return;
            string selected = _serialPortCombo.SelectedItem == null ? null : _serialPortCombo.SelectedItem.ToString();
            _serialPortCombo.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            for (int i = 0; i < ports.Length; i++) _serialPortCombo.Items.Add(ports[i]);
            if (!string.IsNullOrEmpty(selected) && _serialPortCombo.Items.Contains(selected))
            {
                _serialPortCombo.SelectedItem = selected;
            }
            else if (_serialPortCombo.Items.Count > 0)
            {
                _serialPortCombo.SelectedIndex = 0;
            }
            SetSerialStatus(_serialPortCombo.Items.Count > 0 ? "可连接串口：" + _serialPortCombo.Items.Count.ToString() : "未发现串口");
        }

        private bool IsSerialConnected()
        {
            return _serialPort != null && _serialPort.IsOpen;
        }

        private void CloseSerialPort()
        {
            try
            {
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= OnSerialDataReceived;
                    if (_serialPort.IsOpen) _serialPort.Close();
                    _serialPort.Dispose();
                }
            }
            catch
            {
            }
            _serialPort = null;
            ClearSerialQueue();
            if (_connectSerialButton != null) _connectSerialButton.Text = "连接串口";
            SetSerialStatus("仿真数据模式");
        }

        private void SetSerialStatus(string text)
        {
            if (_serialStatusValue != null) _serialStatusValue.Text = text;
        }

        private MotorTopology GetSelectedTopology()
        {
            return _topologyCombo != null && _topologyCombo.SelectedIndex == 1 ? MotorTopology.ThreePhase : MotorTopology.FivePhase;
        }

        private bool IsAutoToleranceEnabled()
        {
            return _autoToleranceCheck == null || _autoToleranceCheck.Checked;
        }

        private FaultKind GetSelectedFault()
        {
            if (_faultCombo == null) return FaultKind.WindingOpen;
            if (_faultCombo.SelectedIndex == 1) return FaultKind.UpperSwitchOpen;
            if (_faultCombo.SelectedIndex == 2) return FaultKind.LowerSwitchOpen;
            return FaultKind.WindingOpen;
        }

        private string GetTopologyText()
        {
            return _engine.Topology == MotorTopology.ThreePhase ? "三相小电机" : "五相容错电机";
        }

        private static string GetFaultText(FaultKind kind)
        {
            if (kind == FaultKind.UpperSwitchOpen) return "A 相上功率管开路";
            if (kind == FaultKind.LowerSwitchOpen) return "A 相下功率管开路";
            return "A 相绕组开路";
        }

        private static string Csv(string value)
        {
            if (value == null) value = string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}

