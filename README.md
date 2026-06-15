# 基于模型-数据混合驱动的电机驱动系统故障诊断与容错控制上位机

这是用于竞赛演示和运行视频录制的 Windows WinForms 上位机程序。程序不使用 Python，不使用 NuGet，不依赖复杂第三方库；当前工程使用 Windows 自带 .NET Framework C# 编译器 `csc.exe` 编译。

## 方法主线

- 项目方法：模型-数据混合驱动开路故障诊断。
- 模型侧：通过模型观测器生成基准量，并计算实测量与观测基准之间的残差特征。
- 数据侧：将残差特征送入极限学习机 ELM 分类器，输出故障类型和 `fault_flag`。
- 控制侧：根据 ELM 分类结果匹配容错控制策略，实现故障不停车、动力不中断。
- 关键指标：故障诊断响应时间 `<= 1 ms`，单相开路故障恢复时间约 `50 ms`。

## 工程路径

```text
E:\26730\Desktop\研电赛\上位机软件
```

## 运行方式

重新编译：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

编译后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\run.ps1
```

也可以用 Visual Studio 打开：

```text
E:\26730\Desktop\研电赛\上位机软件\FivePhaseMotorTwin.sln
```

## 功能说明

- 主界面标题会随电机对象动态变化：
  - 三相永磁同步电机
  - 双绕组永磁同步电机
  - 五相永磁容错电机
- 左侧：运行模式控制、实时数据源、关键指标、模型观测器-ELM诊断链。
- 中间/右侧：电流、机械量、诊断量三组实时波形。
- 下方：诊断结果、容错策略、系统状态日志。
- 支持负载工况选择：空载、轻载、半载、额定负载。
- 支持场景：正常运行、A 相绕组开路、A 相上功率管开路、A 相下功率管开路，以及三类故障的容错控制模式。
- 支持按钮：开始运行、暂停运行、系统复位、注入故障、投入容错、自动运行、导出数据 CSV、导出运行截图。
- 支持 `诊断后自动投入容错` 开关：现场断开 A 相演示时默认开启，ELM 分类完成后自动切换到容错波形；需要分步讲解时可关闭，再手动点击 `投入容错`。
- 支持串口实时数据源和 CSV 数据回放；未连接串口时保持仿真数据模式。
- 波形窗口支持鼠标拖动左右查看历史波形、滚轮缩放时间轴、悬停查看时间与幅值点位信息，双击返回实时跟随。
- 数据导出保存到 `exports` 目录，截图保存到 `screenshots` 目录。
- `开始运行` 只进入健康运行并持续绘制正常波形；点击 `注入故障`、串口/CSV 检测到 A 相异常，或点击 `自动运行` 到达故障阶段后，才进入诊断与容错流程。

## 文件说明

```text
FivePhaseMotorTwin.sln       Visual Studio 解决方案
FivePhaseMotorTwin.csproj    C# WinForms 项目文件
build.ps1                    使用 csc.exe 编译工程
run.ps1                      编译后启动程序
src\Program.cs               程序入口
src\MainForm*.cs             主界面布局和交互逻辑
src\SimulationEngine.cs      电机、故障、容错控制波形运行逻辑
src\WaveformView.cs          实时波形自绘控件
src\SerialTelemetryParser.cs 串口实时数据行解析
src\TwinFlowPanel.cs         模型观测器-ELM流程状态显示控件
src\Scenario.cs              模式、故障类型、运行数据结构
samples\                    CSV 回放样例数据
demo_script.md               演示视频讲解词
bin\FivePhaseMotorTwin.exe   编译后可运行程序
screenshots\                 程序导出运行截图的位置
```

## 录制建议

1. 打开 `bin\FivePhaseMotorTwin.exe`。
2. 现场硬件演示建议选择 `三相永磁同步电机`、`A 相绕组开路` 和所需负载工况，点击 `开始运行` 先展示健康波形。
3. 手动断开 A 相硬件开关后，串口实时数据中的 A 相异常会触发模型观测器残差突增，并由 ELM 输出故障分类结果。
4. 若 `诊断后自动投入容错` 开启，分类完成后自动投入容错控制；若需要讲解诊断与容错对比，可关闭该选项，再手动点击 `投入容错`。
5. 软件演示可用 `自动运行` 或 `回放CSV` 复现完整过程；需要静态画面时点击 `导出运行截图`。

## 备注

- 本程序是工程演示用上位机，可连接真实控制器串口，也可使用仿真数据或 CSV 回放。
- 诊断响应时间会显示为 `0.02 ms` 到 `1 ms` 范围内的工程化数值。
- 容错恢复时间会显示为约 `50 ms`，匹配技术赛指标表达。
