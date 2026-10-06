# 组件对象池

`GameObjectPool<TPooled>` 包装 UnityEngine.Pool.ObjectPool，要求 TPooled 为 Component。当前生产使用者是 AudioManager 的 AudioEmitter 池。它不是 MonoBehaviour，不会自行进入场景或自动清理所有借出对象。

## 逐文件说明

- `GameObjectPool.cs`：实例化、启用/禁用、借还、计数、预热及清空包装；另维护借出对象列表供 CountInActive 使用。
- `GameObjectPool.cs.meta`：脚本 GUID，无默认引用和特殊执行顺序。
- `README.md` / `.meta`：目录总文档与资源标识。

## API 与生命周期

构造时传入组件 prefab、父 Transform、可选 onGet/onRelease、collectionCheck、defaultCapacity 和 poolSize。Create 在 root 下实例化 prefab 后禁用；底层 Get 先激活，再调用 onGet，包装 Get 随后才加入借出列表。Release 先调用 onRelease、再禁用；包装方法最后从借出列表移除。

| API | 当前行为 |
| --- | --- |
| CountActive / CountInactive / CountAll | 转发底层池计数；不是扫描场景得到的真实对象总数。 |
| CanReuse(maxSize) | 有闲置对象，或 CountAll 小于传入上限则返回 true；空池仍可能允许新建。 |
| Get() | 借出闲置对象或新建；自身不检查 CanReuse。 |
| Release(item) | 归还并从包装的借出列表移除；应只归还本池借出的有效对象且只归还一次。 |
| CountInActive(predicate) | 对包装维护的借出列表筛选计数；名称中 Active 指借出记录，不是 GameObject.activeSelf。 |
| Prewarm(count) | 先同时从底层借出 count 个，再逐一释放；负数/零不循环，不强制创建 count 个新对象。 |
| Clear() | 清空底层闲置池及包装的借出列表，不遍历销毁仍借出的活跃对象。 |

`poolSize` 是底层闲置保留上限，不是 Get 的创建硬上限；`defaultCapacity` 是初始集合容量，不是预实例化数量。CanReuse 接受调用方给出的独立上限，不保存或预留名额，有闲置对象时即使 CountAll 已大于该参数也返回 true。AudioManager 在 ReserveEmitter 中用 MaxPoolSize 调用此检查，因此音频请求路径另有创建限制，不能把音频语义与通用包装混为一谈。

## 预热与清理边界

Prewarm 直接用底层 pool.Get/Release，绕过包装 activeInPool 列表，但照常激活对象并执行回调。预热对象不会出现在 CountInActive，onGet 也可能观察到包装尚未登记普通 Get 的对象。回调不可假定列表与所有底层动作始终同步。

Prewarm 不执行 CanReuse，count 超过保留上限时可能临时创建更多对象，多余对象归还后交给底层销毁。对象生命周期回调可能有副作用；不能把预热视为纯分配。没有 try/finally 回收保证，onGet/onRelease 抛异常可能留下未配对借出及计数/列表不一致。

Clear 适用于调用方已处理借出对象之后。活跃对象不会被它销毁，而包装列表会丢失这些记录；不要把 Clear 当成“停止/销毁所有活跃对象”，也不要继续依赖清空前的计数。当前包装没有外部 Destroy 的自动修复、线程同步、对象归属验证或异常隔离。应避免在回调中递归借还同一对象。

## 核查与验证（2026-10-06）

逐文件检查代码及 meta 后，核对 AudioManager 的 ReserveEmitter、预热和销毁路径，以及 AudioIntegrationChecks。修正了 CanReuse 的“保证复用/池自身容量”错误说明，未修改运行逻辑。

现有音频测试间接覆盖复用、播放上限和释放；测试 fixture 的 prewarmAmount 为 0 且不调用 AudioManager.Start，不能据此声称覆盖实际预热、回调异常、带借出对象的 Clear 或通用池所有边界。后续修改池行为时需针对这些语义验证，完整集成回归仍按先 EditMode、后 PlayMode 执行。

本次在隔离 Unity 预览场景使用真实 GameObjectPool<Transform>：poolSize=2，Prewarm(2) 后 All/Inactive=2/2、包装借出列表为 0、两类回调各执行 2 次；随后 Get 三次得到 All/Active=3/3，CanReuse(3)=false，确认 Get 没有硬上限。带三个借出对象 Clear 后 All/包装列表均为 0，而三个对象仍存在。finally 关闭预览场景回收对象；未验证超额归还销毁、回调异常或 Clear 后继续归还的行为。

独立文档复审通过。本轮编译后 Console 无 error，最终工作区顺序通过 EditMode `ffde23500166454aad0d97b5b4ec39f2`（10/10）、PlayMode `cb4f5995eafe45c3aaad666f473cb1cf`（14/14）；包含同期 GameState/Setting 修复及新增测试。通过结果不扩大上面的池行为覆盖范围。
