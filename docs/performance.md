# 性能验证 / Performance verification

本记录来自单台 Windows 设备上的实际应用窗口，日期为 2026-11-07。它说明测试覆盖范围，不承诺其他版本、机器或聊天内容有相同结果。

## 已完成

- 取消额度组件对整页聊天的观察，仅在菜单打开时处理菜单更新；隐藏时暂停，移除时清理定时器与监听。
- 缩小活动状态条的样式匹配范围，减少悬停引起的继承变量重算。
- 简化背景和装饰，保留红黑青主题；代码配色使用精确的容器匹配，允许语法高亮、终端彩色文本保留自身颜色。
- 本地真实窗口完成主题移除、重新应用和状态回读。最后一次应用中，原生控件位置变化为 0。

## 活动状态条展开

同一窗口短测试，以点击到第二个动画帧为指标：旧样式展开约 152–156 ms、收起约 287–289 ms；调整后约 27–36 ms。该指标不是整个应用的帧率，也不是不同聊天内容下的保证。

## 右侧浏览器面板

最初前台对比中，主题状态下打开至布局稳定约 217–222 ms，关闭主题约 161–162 ms。将水印移到外层没有改善，因此未采用该方案。

最终保留的修正删除了对代码/终端祖先名称进行大范围模糊匹配的规则。交错顺序的开合测试中，每种样式各记录 4 次打开和 4 次关闭：

| 样式计算 CPU 时间均值 | 原样式 | 最终修正 |
| --- | ---: | ---: |
| 打开 | 150.7 ms | 131.5 ms |
| 关闭 | 103.4 ms | 92.5 ms |

两类操作合计均值降低约 11.8%。**这一轮窗口处于隐藏状态，测量的是 Chromium 的样式计算 CPU 时间，不是用户可见的展开动画时间或 FPS。** 最终修正没有获得可靠的前台动画复测结果，不将上述百分比宣传为整体打开速度提升。

96 个有尺寸的界面控件在修正前后的几何、配色、背景和边框快照一致。额外的隔离样例检查了普通代码文字、显式语法颜色以及不相关菜单不被误染色。

## 可重复的自动检查

仓库包含主题生命周期与菜单性能回归测试，以及无需安装 Codex 的后端检查。它们验证作用范围、回滚、清理、连接和目标选择等行为，不替代真实设备的动画或滚动测试。

The right-pane percentage above describes style-recalculation CPU work in a hidden renderer, not end-to-end visible latency. The retained change has not completed a reliable foreground animation remeasurement. Session-specific profiling scripts, live screenshots, and personal diagnostics are intentionally excluded from this public repository.
