# 墨团小鬼可编辑源码

本目录保留 PixelFX JavaScript 绘制源和 MIT 许可。正式游戏接入与验证记录在《苍灵志》仓库文档中（未公开）。

已确认的小体型为原稿宽高的80%，直接绘入像素；96×96画布、Unity脚点(49,24)px、PPU32保持不变，不再额外缩放。待机16、移动12、攻击16、受击8、死亡20帧，共72帧，约12 FPS。仅右前等距视角，运行时按左右方向镜像。

正式图集和帧时序在 `Assets/Art/Characters/Enemies/InkImp/InkImp_AllStates.png/json`。图集五行分别为五套动作，每行右端可能留空，请按JSON的72个明确Rect切片。安装器会导入Point、无压缩、无Mipmap，并绑定正式敌人预制体。

要重新渲染，将本目录JS源复制到PixelFX的 `src/fx/critters/`，从PixelFX根目录运行：

```shell
node tools/render.mjs critters/ink-imp-idle critters/ink-imp-move critters/ink-imp-attack critters/ink-imp-hit critters/ink-imp-death --sheet --no-actor
node tools/check.mjs critters/ink-imp-idle critters/ink-imp-move critters/ink-imp-attack critters/ink-imp-hit critters/ink-imp-death
```

原始工具一次性动作会额外导出一个t=duration的透明终止帧，正式图集已去掉该额外帧；死亡动作自身20帧仍保留透明末帧。重新渲染后须按现有JSON的帧数和锚点重新打包，避免误加终止帧。原素材检查结果为5 pass / 0 fail / 0 warnings。

预览地面、接触影和玩家对照没有烘焙到透明图集；游戏使用独立动态投影。
