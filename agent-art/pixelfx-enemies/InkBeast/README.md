# 异形兽 PixelFX 源

造型为断腰墨狼，待机16、移动12、攻击16、受击8、死亡16帧，12 FPS。128×96画布，脚点左上(64,72)，Unity Pivot=(64,24)px，PPU32。小体型修订将原稿按70%绘入像素；首帧可见范围52×33像素，对照墨团小鬼42×36。保留贴近身体颜色的细边，不追加黑色膨胀描边。定义视觉倍率保持1，不再二次缩小。

将本目录JS复制到PixelFX的 `src/fx/critters/`，运行：

```shell
node tools/render.mjs critters/ink-beast-idle critters/ink-beast-move critters/ink-beast-attack critters/ink-beast-hit critters/ink-beast-death --sheet --no-actor
node tools/check.mjs 'critters/ink-beast-*'
```

一次性动作的工具原始导出包含t=duration额外空帧。正式图集去掉该额外帧，仍保留死亡动作自身的透明末帧。打包时须按68个有效Rect处理，不将行末空白作为动画帧。

正式图集在 `Assets/Art/Characters/Enemies/InkBeast/`，接入方式记录在《苍灵志》仓库文档中（未公开）。预览地面不烘焙进素材，运行时沿用动态投影。MIT许可见本目录LICENSE-PixelFX.txt。
