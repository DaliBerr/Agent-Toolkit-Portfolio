# Lilith Troubleshooting Routes

按症状选择一个专题，再搜索标题读取对应条目。路径相对共享库；不要通读所有专题。旧锚点保留在 Knowledge/Troubleshooting/Lilith_Unity_Troubleshooting.md，仅旧链接解析需要时读取。

## Editor、资产与测试安全

- Prefab 意外改写/旧绑定迁移、OnValidate 崩溃、Unity fake-null、字段/MenuItem 不可见、8082 离线、Plastic 状态与生成工程不同步、巨大 YAML 补丁、测试超时/焦点、测试删真实 Saves、取消后存档路由未还原、普通 Player 混入 MCP/TestHelper：
  - Read `Knowledge/Troubleshooting/Lilith/Editor_Safety.md`

## UI、布局与导航

- 打包后 Boss 血条消失/Addressables 事件源副本、玩家死亡后 Boss 血条残留、结算显示内部 ID、截图与静态告警不一致、木桩预览串入玩家视角、Token 可见尺寸/安装底框、拖动被仓库裁剪、空安装槽类型、白图发灰、非16:9留边、CanvasGroup 缺失、导航锁不释放、残留半透明弹窗、默认文本闪现、根拉伸/安全框偏移、存档页重复关闭：
  - Read `Knowledge/Troubleshooting/Lilith/UI.md`

## 连续世界、掉落与生命周期

- 入口 Loading 互等、作者资源 ID/版本/摘要不匹配、角色足迹不同、空高度层阻止开战、prepare-combat-save 脚点不一致、第二波被跳过、Factory 旧 scope 未释放、贴墙出生、大坐标移动锁、许可有效仍卡门、Boss 击杀后越界掉落阻止清房、实体 Ready/句柄 Unloaded、首次 attempt=0 掉落被拒、停用中重挂池化对象、退役 Token 地址残留：
  - Read `Knowledge/Troubleshooting/Lilith/World.md`

## 存档事务与性能

- 扣能量重建 HUD、攻击/拾取/清房/死亡掉帧、CombatPerf/SavePerf、SystemCallBlocker 拦日志、V5 重复校验/复制、后台提交与取消、Homing 已装但 RoomResolutionNotCheckpointable、结算中断丢奖励或恢复旧档：
  - Read `Knowledge/Troubleshooting/Lilith/Persistence_Performance.md`

## 渲染、地形与音效

- 动画草串墙图/持续变化的大色块、树冠离屏/Individual漏树、近层遮挡与远处合批、树干碰撞偏移/新NPOT贴图脚点错位/树冠挡路/重装丢碰撞/树间越界、UnityStats旧计数、Ground混铺露土墙、连接道sortOrder、脚步材质混播/桥水楼层：
  - Read `Knowledge/Troubleshooting/Lilith/Rendering_Audio.md`
- 素材独看正常但进地图比例不对、像小盆景：
  - Read `Knowledge/Tools/Pixel_Loom.md` 的 `Unity 画面尺度核对`

## 离线数值实验

- 低 CPU/进程池重建、证据窗口误作总预算、Python 环境导致 fingerprint 变化、pilot 重跑 reachability、loss 降但通关率恶化、Unity/Python RNG 与顺序漂移、模型覆盖缺口误判死选项：
  - Read `Knowledge/Troubleshooting/Lilith/Balance.md`

ArtTest 旧原型与冻结运行参数不作为当前规则；仅用户要求追溯时定向查归档。以上条目不授权进入 PlayMode、运行房间生成/完整 EditMode 或修改真实存档。
