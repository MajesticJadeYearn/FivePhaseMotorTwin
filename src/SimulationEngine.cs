using System;

namespace FivePhaseMotorTwin
{
    public sealed class SimulationEngine
    {
        private const double AutoFaultDelaySeconds = 3.0;
        private const double ToleranceHandoffDelaySeconds = 0.18;

        private readonly Random _random = new Random(20260427);
        private ScenarioInfo _scenario;
        private FaultKind _selectedFault = FaultKind.WindingOpen;
        private bool _faultInjected;
        private bool _diagnosed;
        private bool _toleranceActive;
        private bool _stableAnnounced;
        private bool _autoDemo;
        private bool _autoToleranceEnabled = true;
        private int _externalFaultScore;
        private double _faultTime;
        private double _toleranceTime;
        private double _diagnosisTimeMs;
        private double _recoveryTimeMs;

        public SimulationEngine()
        {
            Topology = MotorTopology.FivePhase;
            Load = LoadProfile.NoLoad;
            _scenario = ScenarioInfo.FromMode(ScenarioMode.Normal);
            Reset();
        }

        public ScenarioInfo Scenario
        {
            get { return _scenario; }
        }

        public MotorTopology Topology { get; private set; }

        public LoadProfile Load { get; private set; }

        public FaultKind SelectedFault
        {
            get { return _selectedFault; }
        }

        public bool AutoToleranceEnabled
        {
            get { return _autoToleranceEnabled; }
        }

        public double TimeSeconds { get; private set; }

        public double? FaultMarker
        {
            get { return _faultInjected ? (double?)_faultTime : null; }
        }

        public double? ToleranceMarker
        {
            get { return _toleranceActive ? (double?)_toleranceTime : null; }
        }

        public void SetScenario(ScenarioMode mode)
        {
            _scenario = ScenarioInfo.FromMode(mode);
            if (_scenario.HasFault) _selectedFault = _scenario.Fault;
            ResetRuntime();
        }

        public void SetTopology(MotorTopology topology)
        {
            Topology = topology;
            Reset();
        }

        public void SetLoad(LoadProfile load)
        {
            Load = load;
        }

        public void SelectFault(FaultKind kind)
        {
            if (kind == FaultKind.None) kind = FaultKind.WindingOpen;
            _selectedFault = kind;
            if (!_faultInjected)
            {
                _scenario = ScenarioInfo.FromMode(ScenarioMode.Normal);
            }
        }

        public void SetAutoToleranceEnabled(bool enabled)
        {
            _autoToleranceEnabled = enabled;
            if (_faultInjected && !_toleranceActive)
            {
                FaultKind kind = _scenario.HasFault ? _scenario.Fault : _selectedFault;
                _scenario = ScenarioInfo.FromMode(GetModeForFault(kind, enabled));
                _selectedFault = kind;
            }
        }

        public void Reset()
        {
            _scenario = ScenarioInfo.FromMode(ScenarioMode.Normal);
            ResetRuntime();
        }

        private void ResetRuntime()
        {
            TimeSeconds = 0.0;
            _faultInjected = false;
            _diagnosed = false;
            _toleranceActive = false;
            _stableAnnounced = false;
            _autoDemo = false;
            _faultTime = double.NaN;
            _toleranceTime = double.NaN;
            _diagnosisTimeMs = 0.04;
            _recoveryTimeMs = 49.0;
            _externalFaultScore = 0;
        }

        public void StartAutoDemo()
        {
            ResetRuntime();
            _scenario = ScenarioInfo.FromMode(GetModeForFault(_selectedFault, true));
            _autoDemo = true;
        }

        public bool InjectFaultNow()
        {
            _scenario = ScenarioInfo.FromMode(GetModeForFault(_selectedFault, _autoToleranceEnabled));
            if (_faultInjected) return false;
            BeginFault();
            return true;
        }

        public bool ActivateToleranceNow()
        {
            if (!_faultInjected)
            {
                _scenario = ScenarioInfo.FromMode(GetModeForFault(_selectedFault, true));
                BeginFault();
                _diagnosed = true;
            }
            else if (!_scenario.HasTolerance)
            {
                FaultKind kind = _scenario.HasFault ? _scenario.Fault : _selectedFault;
                _scenario = ScenarioInfo.FromMode(GetModeForFault(kind, true));
                _selectedFault = kind;
            }

            if (_toleranceActive) return false;
            if (!_diagnosed) _diagnosed = true;
            ActivateTolerance();
            return true;
        }

        public SimulationFrame StepExternal(SignalSnapshot input, double dt)
        {
            if (input == null) return Step(dt);

            if (!double.IsNaN(input.Time) && input.Time >= 0.0 && input.Time >= TimeSeconds)
            {
                TimeSeconds = input.Time;
            }
            else
            {
                TimeSeconds += dt;
            }

            SimulationFrame frame = new SimulationFrame();
            SignalSnapshot sample = NormalizeExternalSample(input);
            double inferredResidual = CalculateExternalResidual(sample);
            if (sample.Residual > 0.0) inferredResidual = Math.Max(inferredResidual, sample.Residual);
            sample.Residual = Math.Max(0.0, Math.Min(1.18, inferredResidual));

            bool externalFlag = sample.FaultFlag > 0.5 || sample.Residual > 0.55;
            bool aPhaseOpen = IsAPhaseLikelyOpen(sample);
            if (externalFlag || aPhaseOpen)
            {
                _externalFaultScore++;
            }
            else if (_externalFaultScore > 0)
            {
                _externalFaultScore--;
            }

            if (!_faultInjected && (externalFlag || _externalFaultScore >= 5))
            {
                _scenario = ScenarioInfo.FromMode(GetModeForFault(_selectedFault, _autoToleranceEnabled));
                BeginFault();
                frame.Events.Add("实时数据检测到 A 相异常：" + _scenario.FaultText + "。");
                frame.Events.Add("数字孪生残差异常：实测电流与基准电流偏差突增。");
                if (_autoToleranceEnabled) frame.Events.Add("诊断完成后将自动投入容错控制。");
            }

            if (_scenario.HasTolerance && _faultInjected && _diagnosed && !_toleranceActive && TimeSeconds >= _faultTime + ToleranceHandoffDelaySeconds)
            {
                ActivateTolerance();
                frame.Events.Add("诊断结果联动容错控制投入：" + _scenario.StrategyText + "。");
            }

            UpdateDiagnosisAndRecovery(frame);
            if (_diagnosed) sample.FaultFlag = 1.0;

            frame.Sample = sample;
            frame.SourceText = "串口实时数据";
            FillFrameStatus(frame);
            return frame;
        }

        private void BeginFault()
        {
            _faultInjected = true;
            _diagnosed = false;
            _toleranceActive = false;
            _stableAnnounced = false;
            _faultTime = TimeSeconds;
            _diagnosisTimeMs = 0.02 + _random.NextDouble() * 0.88;
            _recoveryTimeMs = 47.5 + _random.NextDouble() * 4.8;
        }

        private void ActivateTolerance()
        {
            if (!_scenario.HasTolerance || !_faultInjected || _toleranceActive) return;
            _toleranceActive = true;
            _toleranceTime = TimeSeconds;
            _stableAnnounced = false;
        }

        public SimulationFrame Step(double dt)
        {
            TimeSeconds += dt;
            SimulationFrame frame = new SimulationFrame();

            if (_autoDemo)
            {
                if (!_faultInjected && TimeSeconds >= AutoFaultDelaySeconds)
                {
                    BeginFault();
                    frame.Events.Add("故障注入完成：" + _scenario.FaultText + "。");
                    frame.Events.Add("数字孪生残差异常：实测电流与基准电流偏差突增。");
                }
                if (_scenario.HasTolerance && _faultInjected && _diagnosed && !_toleranceActive && TimeSeconds >= _faultTime + ToleranceHandoffDelaySeconds)
                {
                    ActivateTolerance();
                    frame.Events.Add("诊断结果联动容错控制投入：" + _scenario.StrategyText + "。");
                }
            }
            else
            {
                if (_scenario.HasTolerance && _faultInjected && _diagnosed && !_toleranceActive && TimeSeconds >= _faultTime + ToleranceHandoffDelaySeconds)
                {
                    ActivateTolerance();
                    frame.Events.Add("诊断结果联动容错控制投入：" + _scenario.StrategyText + "。");
                }
            }

            UpdateDiagnosisAndRecovery(frame);

            frame.Sample = GenerateSignals(TimeSeconds);
            FillFrameStatus(frame);
            return frame;
        }

        private void UpdateDiagnosisAndRecovery(SimulationFrame frame)
        {
            if (_faultInjected && !_diagnosed && TimeSeconds >= _faultTime + _diagnosisTimeMs / 1000.0)
            {
                _diagnosed = true;
                frame.Events.Add("故障诊断完成：" + _scenario.FaultText + "，响应时间 " + _diagnosisTimeMs.ToString("0.00") + " ms。");
            }

            if (_toleranceActive && !_stableAnnounced && TimeSeconds >= _toleranceTime + _recoveryTimeMs / 1000.0)
            {
                _stableAnnounced = true;
                frame.Events.Add("系统恢复稳定运行：容错恢复时间 " + _recoveryTimeMs.ToString("0.0") + " ms。");
            }
        }

        private void FillFrameStatus(SimulationFrame frame)
        {
            frame.DiagnosisTimeMs = _faultInjected ? _diagnosisTimeMs : 0.0;
            frame.RecoveryTimeMs = _toleranceActive ? _recoveryTimeMs : 0.0;
            frame.FaultTime = FaultMarker;
            frame.ToleranceTime = ToleranceMarker;
            if (string.IsNullOrEmpty(frame.SourceText)) frame.SourceText = "仿真数据";
            frame.TopologyText = GetTopologyText();
            frame.LoadText = GetLoadText();

            if (!_faultInjected)
            {
                frame.RunningState = _autoDemo ? "自动运行：" + GetTopologyText() + " " + GetLoadText() + "健康阶段" : GetTopologyText() + " " + GetLoadText() + "健康运行";
                frame.FaultType = "无故障";
                frame.DiagnosisResult = "数字孪生残差正常，fault_flag = 0";
                frame.ControlStrategy = GetHealthyStrategy();
            }
            else if (!_diagnosed)
            {
                frame.RunningState = "故障注入，等待诊断";
                frame.FaultType = "待识别";
                frame.DiagnosisResult = "残差突增，诊断窗口采样中";
                frame.ControlStrategy = "保持当前保护策略";
            }
            else if (_scenario.HasTolerance && _toleranceActive)
            {
                frame.RunningState = _stableAnnounced ? "容错稳定运行" : "容错恢复中";
                frame.FaultType = _scenario.FaultText;
                frame.DiagnosisResult = "已定位 " + _scenario.FaultText + "，fault_flag = 1";
                frame.ControlStrategy = _scenario.StrategyText;
            }
            else
            {
                frame.RunningState = _scenario.HasTolerance ? "已诊断，自动容错待投入" : "故障运行，未投入容错";
                frame.FaultType = _scenario.FaultText;
                frame.DiagnosisResult = "已定位 " + _scenario.FaultText + "，fault_flag = 1";
                frame.ControlStrategy = _scenario.HasTolerance ? "诊断联动：" + _scenario.StrategyText : _scenario.StrategyText;
            }

            SignalSnapshot s = frame.Sample;
            frame.TwinPhysicalText = "实体电机实时电流  ia=" + s.Ia.ToString("0.00") + " A";
            frame.TwinReferenceText = "数字孪生基准电流  ia*=" + s.IaRef.ToString("0.00") + " A";
            frame.TwinResidualText = "残差计算  residual=" + s.Residual.ToString("0.000") + ", fault_flag=" + s.FaultFlag.ToString("0");
            frame.TwinFaultText = "故障类型识别  " + frame.FaultType;
            frame.TwinStrategyText = "容错策略匹配  " + frame.ControlStrategy;
        }

        private SignalSnapshot NormalizeExternalSample(SignalSnapshot input)
        {
            SignalSnapshot sample = new SignalSnapshot();
            sample.Time = TimeSeconds;
            sample.Ia = input.Ia;
            sample.Ib = input.Ib;
            sample.Ic = input.Ic;
            sample.Id = input.Id;
            sample.Ie = input.Ie;
            sample.Ix = input.Ix;
            sample.IaRef = Math.Abs(input.IaRef) > 0.0001 ? input.IaRef : input.Ia;
            sample.Speed = input.Speed;
            sample.Torque = input.Torque;
            sample.Iq = input.Iq;
            sample.Residual = input.Residual;
            sample.FaultFlag = input.FaultFlag;
            return sample;
        }

        private double CalculateExternalResidual(SignalSnapshot sample)
        {
            double otherSum = Math.Abs(sample.Ib) + Math.Abs(sample.Ic);
            int count = 2;
            if (Topology == MotorTopology.FivePhase)
            {
                otherSum += Math.Abs(sample.Id) + Math.Abs(sample.Ie);
                count = 4;
            }
            else if (Topology == MotorTopology.DualWinding)
            {
                otherSum += Math.Abs(sample.Id) + Math.Abs(sample.Ie) + Math.Abs(sample.Ix);
                count = 5;
            }

            double otherMean = otherSum / count;
            if (otherMean < 0.2) return Math.Max(0.02, sample.Residual);

            double drop = Math.Max(0.0, (otherMean - Math.Abs(sample.Ia)) / Math.Max(1.0, otherMean));
            return Math.Max(0.02, 0.04 + 0.82 * drop);
        }

        private bool IsAPhaseLikelyOpen(SignalSnapshot sample)
        {
            double otherSum = Math.Abs(sample.Ib) + Math.Abs(sample.Ic);
            int count = 2;
            if (Topology == MotorTopology.FivePhase)
            {
                otherSum += Math.Abs(sample.Id) + Math.Abs(sample.Ie);
                count = 4;
            }
            else if (Topology == MotorTopology.DualWinding)
            {
                otherSum += Math.Abs(sample.Id) + Math.Abs(sample.Ie) + Math.Abs(sample.Ix);
                count = 5;
            }

            double otherMean = otherSum / count;
            return otherMean > 2.0 && Math.Abs(sample.Ia) < otherMean * 0.22;
        }

        private SignalSnapshot GenerateSignals(double t)
        {
            int phaseCount = GetPhaseCount();
            double loadFactor = GetLoadFactor();
            double w = 2.0 * Math.PI * 4.5;
            double amp = 8.5 + 4.0 * loadFactor;
            double targetSpeed = 1500.0 - 45.0 * loadFactor;
            double targetTorque = 5.5 + 32.5 * loadFactor;
            double targetIq = 6.2 + 12.0 * loadFactor;
            double[] reference = new double[6];
            double[] actual = new double[6];

            for (int i = 0; i < phaseCount; i++)
            {
                double angle = GetPhaseAngleRadians(i);
                reference[i] = amp * Math.Sin(w * t + angle);
                actual[i] = reference[i] + 0.12 * Math.Sin(2.0 * Math.PI * 0.9 * t + i);
            }

            double faultAge = _faultInjected ? Math.Max(0.0, t - _faultTime) : 0.0;
            double controlAge = _toleranceActive ? Math.Max(0.0, t - _toleranceTime) : 0.0;
            double recovery = _toleranceActive ? (1.0 - Math.Exp(-controlAge / 0.055)) : 0.0;
            double transient = _toleranceActive ? Math.Exp(-controlAge / 0.075) : 1.0;
            double residual = 0.025 + 0.012 * Math.Abs(Math.Sin(2.0 * Math.PI * 0.7 * t));
            double faultFlag = (_diagnosed ? 1.0 : 0.0);
            double speed = targetSpeed + 1.8 * Math.Sin(2.0 * Math.PI * 0.5 * t);
            double torque = targetTorque + (0.18 + 0.28 * loadFactor) * Math.Sin(2.0 * Math.PI * 1.1 * t);
            double iq = targetIq + (0.15 + 0.16 * loadFactor) * Math.Sin(2.0 * Math.PI * 1.0 * t + 0.4);

            if (_faultInjected)
            {
                double ripple = Math.Sin(2.0 * w * t);
                residual = 0.22 + 0.65 * Math.Exp(-faultAge / 0.18) + 0.08 * Math.Abs(Math.Sin(w * t));

                if (_scenario.Fault == FaultKind.WindingOpen)
                {
                    actual[0] = 0.08 * Math.Sin(8.0 * w * t);
                    for (int i = 1; i < phaseCount; i++)
                    {
                        double compGain = _toleranceActive ? (1.16 - 0.08 * transient) : 1.06;
                        double distort = _toleranceActive ? (0.045 + 0.20 * transient) : 0.26;
                        actual[i] = compGain * reference[i] + amp * distort * Math.Sin(3.0 * w * t + i * 0.65);
                    }
                }
                else if (_scenario.Fault == FaultKind.UpperSwitchOpen)
                {
                    if (reference[0] > 0)
                    {
                        actual[0] = reference[0] * (_toleranceActive ? (0.18 + 0.12 * recovery) : 0.04);
                    }
                    else
                    {
                        actual[0] = reference[0] * (_toleranceActive ? 0.96 : 1.00);
                    }
                    ApplyBridgeCompensation(actual, reference, amp, t, _toleranceActive, transient, true, phaseCount);
                }
                else if (_scenario.Fault == FaultKind.LowerSwitchOpen)
                {
                    if (reference[0] < 0)
                    {
                        actual[0] = reference[0] * (_toleranceActive ? (0.18 + 0.12 * recovery) : 0.04);
                    }
                    else
                    {
                        actual[0] = reference[0] * (_toleranceActive ? 0.96 : 1.00);
                    }
                    ApplyBridgeCompensation(actual, reference, amp, t, _toleranceActive, transient, false, phaseCount);
                }

                if (_toleranceActive)
                {
                    double restore = recovery;
                    double faultSpeed = targetSpeed - (24.0 + 18.0 * loadFactor);
                    double faultTorque = targetTorque * (0.72 + 0.08 * loadFactor);
                    double faultIq = targetIq + 3.6 + 2.5 * loadFactor;
                    speed = faultSpeed + (targetSpeed - faultSpeed) * restore + (18.0 * transient + 1.6) * Math.Sin(2.0 * Math.PI * 6.5 * t);
                    torque = faultTorque + (targetTorque - faultTorque) * restore + (2.8 * transient + 0.26) * ripple;
                    iq = faultIq + (targetIq - faultIq) * restore + (2.2 * transient + 0.18) * Math.Sin(2.0 * Math.PI * 5.0 * t);
                    residual = 0.12 + 0.38 * transient + 0.04 * Math.Abs(Math.Sin(w * t));
                }
                else
                {
                    speed = targetSpeed - (22.0 + 20.0 * loadFactor) + (22.0 + 15.0 * loadFactor) * Math.Sin(2.0 * Math.PI * 3.0 * t) * (0.45 + 0.55 * Math.Min(1.0, faultAge / 0.4));
                    torque = targetTorque * 0.74 + (2.8 + 2.2 * loadFactor) * ripple + 0.9 * Math.Sin(2.0 * Math.PI * 11.0 * t);
                    iq = targetIq + 3.8 + 2.8 * loadFactor + (2.1 + 1.6 * loadFactor) * Math.Sin(2.0 * Math.PI * 5.0 * t + 0.8);
                }
            }

            SignalSnapshot s = new SignalSnapshot();
            s.Time = t;
            s.Ia = actual[0];
            s.Ib = actual[1];
            s.Ic = actual[2];
            s.Id = actual[3];
            s.Ie = actual[4];
            s.Ix = actual[5];
            s.IaRef = reference[0];
            s.Speed = speed;
            s.Torque = torque;
            s.Iq = iq;
            s.Residual = Math.Max(0.0, Math.Min(1.18, residual));
            s.FaultFlag = faultFlag;
            return s;
        }

        private static void ApplyBridgeCompensation(double[] actual, double[] reference, double amp, double t, bool tolerance, double transient, bool upper, int phaseCount)
        {
            double w = 2.0 * Math.PI * 4.5;
            double sign = upper ? 1.0 : -1.0;
            for (int i = 1; i < phaseCount; i++)
            {
                double comp = tolerance ? (0.10 + 0.20 * transient) : 0.24;
                double selective = Math.Max(0.0, sign * Math.Sin(w * t));
                actual[i] = reference[i] + amp * comp * selective * Math.Sin(w * t + i * 0.9);
            }
        }

        private static ScenarioMode GetModeForFault(FaultKind kind, bool tolerance)
        {
            if (kind == FaultKind.UpperSwitchOpen) return tolerance ? ScenarioMode.UpperSwitchOpenTolerance : ScenarioMode.UpperSwitchOpen;
            if (kind == FaultKind.LowerSwitchOpen) return tolerance ? ScenarioMode.LowerSwitchOpenTolerance : ScenarioMode.LowerSwitchOpen;
            return tolerance ? ScenarioMode.WindingOpenTolerance : ScenarioMode.WindingOpen;
        }

        private string GetTopologyText()
        {
            if (Topology == MotorTopology.ThreePhase) return "三相永磁同步电机";
            if (Topology == MotorTopology.DualWinding) return "双绕组永磁同步电机";
            return "五相容错电机";
        }

        private string GetHealthyStrategy()
        {
            if (Topology == MotorTopology.ThreePhase) return "健康三相矢量控制 / 三相 SVPWM";
            if (Topology == MotorTopology.DualWinding) return "双绕组三相矢量控制 / 双绕组解耦";
            return "健康五相矢量控制 / 五相 SVPWM";
        }

        private string GetLoadText()
        {
            if (Load == LoadProfile.Light) return "轻载";
            if (Load == LoadProfile.Medium) return "半载";
            if (Load == LoadProfile.Rated) return "额定负载";
            return "空载";
        }

        private double GetLoadFactor()
        {
            if (Load == LoadProfile.Light) return 0.30;
            if (Load == LoadProfile.Medium) return 0.60;
            if (Load == LoadProfile.Rated) return 1.00;
            return 0.0;
        }

        private int GetPhaseCount()
        {
            if (Topology == MotorTopology.ThreePhase) return 3;
            if (Topology == MotorTopology.DualWinding) return 6;
            return 5;
        }

        private double GetPhaseAngleRadians(int index)
        {
            if (Topology == MotorTopology.ThreePhase)
            {
                return -120.0 * index * Math.PI / 180.0;
            }
            if (Topology == MotorTopology.DualWinding)
            {
                int local = index % 3;
                double shift = index >= 3 ? -30.0 : 0.0;
                return (-120.0 * local + shift) * Math.PI / 180.0;
            }
            return -72.0 * index * Math.PI / 180.0;
        }
    }
}


