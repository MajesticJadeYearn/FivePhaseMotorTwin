using System;
using System.Collections.Generic;

namespace FivePhaseMotorTwin
{
    public enum FaultKind
    {
        None,
        WindingOpen,
        UpperSwitchOpen,
        LowerSwitchOpen
    }

    public enum MotorTopology
    {
        ThreePhase = 3,
        FivePhase = 5
    }

    public enum ScenarioMode
    {
        Normal,
        WindingOpen,
        UpperSwitchOpen,
        LowerSwitchOpen,
        WindingOpenTolerance,
        UpperSwitchOpenTolerance,
        LowerSwitchOpenTolerance
    }

    public sealed class ScenarioInfo
    {
        public ScenarioMode Mode;
        public string DisplayName;
        public FaultKind Fault;
        public bool HasTolerance;
        public string FaultText;
        public string StrategyText;

        public bool HasFault
        {
            get { return Fault != FaultKind.None; }
        }

        public static readonly ScenarioInfo[] All = new ScenarioInfo[]
        {
            new ScenarioInfo(ScenarioMode.Normal, "正常运行", FaultKind.None, false, "无故障", "健康五相矢量控制 / 五相SVPWM"),
            new ScenarioInfo(ScenarioMode.WindingOpen, "A 相绕组开路故障", FaultKind.WindingOpen, false, "A 相绕组开路", "诊断完成，等待容错控制投入"),
            new ScenarioInfo(ScenarioMode.UpperSwitchOpen, "A 相上功率管开路故障", FaultKind.UpperSwitchOpen, false, "A 相上功率管开路", "诊断完成，等待半波畸变补偿"),
            new ScenarioInfo(ScenarioMode.LowerSwitchOpen, "A 相下功率管开路故障", FaultKind.LowerSwitchOpen, false, "A 相下功率管开路", "诊断完成，等待半波畸变补偿"),
            new ScenarioInfo(ScenarioMode.WindingOpenTolerance, "A 相绕组开路 + 容错控制", FaultKind.WindingOpen, true, "A 相绕组开路", "四相重构电流分配 + q 轴转矩补偿"),
            new ScenarioInfo(ScenarioMode.UpperSwitchOpenTolerance, "A 相上功率管开路 + 容错控制", FaultKind.UpperSwitchOpen, true, "A 相上功率管开路", "上桥臂开路半波识别 + 剩余相电流重构"),
            new ScenarioInfo(ScenarioMode.LowerSwitchOpenTolerance, "A 相下功率管开路 + 容错控制", FaultKind.LowerSwitchOpen, true, "A 相下功率管开路", "下桥臂开路半波识别 + 剩余相电流重构")
        };

        private ScenarioInfo(ScenarioMode mode, string displayName, FaultKind fault, bool hasTolerance, string faultText, string strategyText)
        {
            Mode = mode;
            DisplayName = displayName;
            Fault = fault;
            HasTolerance = hasTolerance;
            FaultText = faultText;
            StrategyText = strategyText;
        }

        public static ScenarioInfo FromMode(ScenarioMode mode)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Mode == mode) return All[i];
            }
            return All[0];
        }

        public static ScenarioMode WithTolerance(FaultKind kind)
        {
            if (kind == FaultKind.UpperSwitchOpen) return ScenarioMode.UpperSwitchOpenTolerance;
            if (kind == FaultKind.LowerSwitchOpen) return ScenarioMode.LowerSwitchOpenTolerance;
            return ScenarioMode.WindingOpenTolerance;
        }
    }

    public sealed class SignalSnapshot
    {
        public double Time;
        public double Ia;
        public double Ib;
        public double Ic;
        public double Id;
        public double Ie;
        public double IaRef;
        public double Speed;
        public double Torque;
        public double Iq;
        public double Residual;
        public double FaultFlag;
    }

    public sealed class SimulationFrame
    {
        public SignalSnapshot Sample;
        public string TopologyText;
        public string RunningState;
        public string FaultType;
        public string DiagnosisResult;
        public string ControlStrategy;
        public string TwinPhysicalText;
        public string TwinReferenceText;
        public string TwinResidualText;
        public string TwinFaultText;
        public string TwinStrategyText;
        public double DiagnosisTimeMs;
        public double RecoveryTimeMs;
        public double? FaultTime;
        public double? ToleranceTime;
        public List<string> Events;

        public SimulationFrame()
        {
            Events = new List<string>();
        }
    }
}
