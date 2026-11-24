# 代码审查插件：SystemCallBlocker

这是《苍灵志》游戏仓库里的 Unity Editor 插件，原样收录，注释也保持原样。

## 为什么需要它

在 Unity 里，`Random.Range` 和 `Debug.Log` 随手就能写出来，人和 Agent 都容易这样写。《苍灵志》对这两类调用有约定：

- **随机数**：须经带种子的 `Vocalith.Random`。它用 PCG32 算法，种子相同、调用顺序相同，输出就相同。这样 Agent 引入的问题可以按种子复现。
- **日志**：须经统一的 [`Vocalith.Logging.GameDebug`](https://github.com/DaliBerr/Lilith-Portfolio/blob/main/Assets/Scripts/Vocalith/Log/GameDebug.cs)。它的方法带 `[Conditional("UNITY_EDITOR")]` 和 `[Conditional("DEVELOPMENT_BUILD")]`，发行包里这些调用会被编译器去掉。

这个插件在编辑器里直接检查，不靠人或 Agent 自觉遵守。

## 它做什么

- **扫描时机**：每次脚本编译完成后，扫描 `Assets` 下的全部 C# 源码；即将进入 Play 模式前，再扫描一次。
- **规则**：共三条，分别针对 `System.Random`、`UnityEngine.Random` 和 `UnityEngine.Debug`。每条规则带替换提示，可以单独设置排除目录。
- **减少误报**：扫描前把注释、字符串和字符字面量替换成空格，同时保留换行。
- **报错与拦截**：发现违规时，在控制台报出规则、文件、违规所在的行号和替换建议；同一规则在同一行只报一次。如果是进入 Play 模式前发现的，插件会取消进入，修正后才能进入。
- **排除范围**：第三方插件、Editor 工具目录，以及 GameDebug 自身所在的目录，都不参与扫描。

## 文件

| 文件 | 内容 |
| --- | --- |
| [`SystemCallBlocker.cs`](SystemCallBlocker.cs) | 插件本体，放在 `Assets/Editor/` 下即可生效 |
| [`Random.cs`](Random.cs) | 用来替换系统随机数的 `Vocalith.Random`（PCG32） |
