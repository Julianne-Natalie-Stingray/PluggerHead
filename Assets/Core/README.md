# Core 预制体

逐文件核查日期：2026-10-06。

| 文件 | 实际组件与引用 |
| --- | --- |
| `Core.prefab` | 活动根 Core，包含 CoreFacade、TimerRunner、InputManager、AudioManager、SceneSwitchManager、LevelProgressTracker；活动子物体 AudioRoot 用于声部实例。音频管理器引用 AudioEmitter 组件和默认音频配置；切换管理器与进度跟踪器引用同一默认切换配置。 |
| `AudioEmitter.prefab` | 活动物体上的 AudioEmitter 与 AudioSource；playOnAwake=false，clip/group 为空，volume/pitch=1、不循环、priority=128、doppler=1、距离1/500。Manager 播放前配置实际 clip、分组及播放参数。 |

两者 Transform 为零位置、单位旋转和单位缩放；meta 与脚本、组件 fileID、配置引用已核对，无断链。Core 不含 AudioListener，场景仍需相机/监听器。实例化空 AudioEmitter 不会自动播放声音。

跨场景保活和重复 Core 销毁由 CoreFacade 实现，不是 prefab 文件自动提供。以根物体实例化 Core，并在调用服务前等待其初始化；场景配置完整性与使用边界见 [Core 实现说明](../../Scripts/Core/README.md)。本轮仅文档变更，复用最近集成测试结果，未独立实例化每个 prefab 或执行构建。
