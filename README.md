# 基于数字孪生的五相电机故障诊断与容错控制上位机

这是用于运行视频录制的 Windows WinForms 上位机程序。程序不使用 Python，不使用 NuGet，不依赖复杂第三方库；当前工程使用 Windows 自带 .NET Framework C# 编译器 `csc.exe` 编译。

## PPT 内容提炼

### 技术赛 PPT
- 项目关键词：五相永磁容错电机、AI 大模型、数字孪生故障诊断、多模态容错控制、诊断与容错闭环。
- 关键指标：故障诊断响应时间 `<= 1 ms`，单相开路故障恢复时间 `<= 50 ms`，故障位置/类型定位准确率 `>= 99.8%`。
- 演示重点：A 相绕组开路、A 相上功率管开路、诊断残差突增、fault_flag 置 1、容错后转速/转矩/q 轴电流恢复稳定。

### 商业赛 PPT
- 核心表达：`故障不停车、动力不中断`。
- 价值主线：现有驱动系统故障后失控，本项目提供五相永磁容错电机 + AI/数字孪生诊断系统 + 多模态容错控制器的一体化解决方案。
- 视频表达建议：先展示正常运行，再注入故障造成明显波形异常，随后投入容错控制并恢复平稳，用屏幕状态证明“故障后车辆仍可稳定行驶”。

### 既有软件平台 PPT
- 可借鉴逻辑：软件平台先显示电机运行数据，再通过数字孪生基准电流与实体电机实测电流做残差计算，完成故障类型识别，最后匹配容错控制策略。
- 本程序对应界面：左侧“数字孪生模型状态”区域按 `实体电机实时电流 -> 数字孪生基准电流 -> 残差计算 -> 故障类型识别 -> 容错策略匹配` 展示闭环流程。

## 工程路径

```text
E:\26730\Codex\FivePhaseMotorTwin
```

## 运行方式

最简单方式：

```text
E:\26730\Codex\FivePhaseMotorTwin\bin\FivePhaseMotorTwin.exe
```

如果需要重新编译，在 PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File E:\26730\Codex\FivePhaseMotorTwin\build.ps1
```

编译后运行：

```powershell
powershell -ExecutionPolicy Bypass -File E:\26730\Codex\FivePhaseMotorTwin\run.ps1
```

也可以用 Visual Studio 打开：

```text
E:\26730\Codex\FivePhaseMotorTwin\FivePhaseMotorTwin.sln
```

## 功能说明

- 主界面标题：基于数字孪生的五相电机故障诊断与容错控制上位机。
- 左侧：运行模式控制、关键指标、数字孪生模型状态。
- 中间/右侧：三组实时波形。
  - 五相电流：`ia, ib, ic, id, ie`
  - 双绕组电流：`A1, B1, C1, A2, B2, C2`
  - 机械与控制量：`Speed, Torque, iq`
  - 诊断量：`residual, fault_flag`
- 下方：诊断结果、容错策略、系统状态日志。
- 支持三相永磁同步电机、五相容错电机、双绕组永磁同步电机演示切换；三相模式显示 `ia, ib, ic`，五相模式显示 `ia, ib, ic, id, ie`，双绕组模式显示两套三相绕组电流。
- 支持负载工况选择：空载、轻载、半载、额定负载。
- 支持场景：正常运行、A 相绕组开路、A 相上功率管开路、A 相下功率管开路，以及三类故障的容错控制模式。
- 支持按钮：开始运行、暂停运行、系统复位、注入故障、投入容错、自动运行、导出数据 CSV、导出运行截图。
- 支持 `诊断后自动投入容错` 开关：现场断开 A 相演示时默认开启，故障诊断完成后自动切换到容错波形；需要分步讲解时可关闭，再手动点击 `投入容错`。
- 支持可选串口实时数据源：连接控制器后，接收 `ia,ib,ic`、`ia,ib,ic,speed,torque,iq`、`ia,ib,ic,id,ie,speed,torque,iq,residual,fault_flag` 等 CSV 行，也兼容 `ia=...,ib=...` 键值行；未连接串口时保持仿真数据模式。
- 支持 CSV 数据回放：可选择样例或历史数据文件复现实测波形，回放数据与串口数据共用同一套诊断和容错联动逻辑。
- 波形窗口支持鼠标拖动左右查看历史波形、滚轮缩放时间轴、悬停查看时间与幅值点位信息，双击返回实时跟随。
- 数据导出保存到 `exports` 目录，截图保存到 `screenshots` 目录。
- `开始运行` 只进入健康运行并持续绘制正常波形，不会自动注入故障；只有点击 `注入故障`、串口/CSV 检测到 A 相异常、或点击 `自动运行` 到达故障阶段后，才进入诊断与容错流程。
- 自动运行模式：正常运行 3 秒 -> 注入故障 -> 诊断后按同一延时投入容错控制 -> 稳定运行。

## 文件说明

```text
FivePhaseMotorTwin.sln       Visual Studio 解决方案
FivePhaseMotorTwin.csproj    C# WinForms 项目文件
build.ps1                    使用 csc.exe 编译工程
run.ps1                      编译后启动程序
src\Program.cs               程序入口
src\MainForm*.cs             主界面布局和交互逻辑
src\SimulationEngine.cs      五相电机、故障、容错控制波形运行逻辑
src\WaveformView.cs          实时波形自绘控件
src\SerialTelemetryParser.cs 串口实时数据行解析
src\TwinFlowPanel.cs         数字孪生流程状态显示控件
src\Scenario.cs              模式、故障类型、运行数据结构
samples\                    CSV 回放样例数据
demo_script.md               演示视频讲解词
bin\FivePhaseMotorTwin.exe   已编译可运行程序
screenshots\                 程序导出运行截图的位置
```

## 录制建议

1. 打开 `bin\FivePhaseMotorTwin.exe`。
2. 现场硬件演示建议选择 `三相永磁同步电机`、`A 相绕组开路`、所需负载工况，点击 `开始运行` 先展示健康波形。
3. 手动断开 A 相硬件开关后，串口实时数据中的 A 相异常会触发诊断，并在 `诊断后自动投入容错` 开启时自动切换到容错波形。
4. 若需要讲解诊断与容错对比，可关闭 `诊断后自动投入容错`，先展示故障诊断，再手动点击 `投入容错`。
5. 软件演示可用 `自动运行` 或 `回放CSV` 复现完整过程；需要静态画面时点击 `导出运行截图`，截图保存到 `screenshots` 目录。

## 备注

- 本程序是工程运行用数字孪生上位机，不连接真实控制器。
- 诊断响应时间会显示为 `0.02 ms` 到 `1 ms` 范围内的工程化数值。
- 容错恢复时间会显示为约 `50 ms`，匹配技术赛 PPT 中的指标表达。
- 编译与运行不需要 Python。

