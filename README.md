# Emuera.EM

Emuera 是 eramaker 的 C# 模拟器，本质是一个 **eraBasic 语言解释器 + 游戏本体**：游戏由 `csv/`（数据）与 `erb/`（脚本）目录构成，Emuera 负责解析并运行它们

本仓库是 [EM](https://gitlab.com/EvilMask/emuera.em) 的 fork，在 EM 基础上继续做现代化改造与重构

`master` 保持与上游同步，改动集中在 `develop`，不推送到上游

## 平台

- Windows only（WinForms 桌面程序），目标框架 `net10.0-windows`，平台 `x64` / `x86`
- 版本号见 `Emuera/Emuera.csproj` 的 `InformationalVersion`

## 仓库布局

| 路径 | 内容 |
| --- | --- |
| `Emuera.sln`（根目录） | 解决方案 = `Emuera/` + `EmueraPluginExample/` |
| `Emuera/` | 解释器本体：`Runtime/Script/`（词法/句法/执行）、`Runtime/Script/Data/`（运行时数据）、`Runtime/Config/`（配置）、`UI/`（WinForms 界面） |
| `Emuera.Tests/` | xUnit 单元测试工程，不在解决方案内 |
| `EmueraPluginExample/` | 插件 API 示例 |
| `docs/` | 长期参考文档，索引见 [docs/README.md](docs/README.md) |
| `tracker/` | 议题与规格，按主题分目录；约定见 [docs/tracking/issue-tracker.md](docs/tracking/issue-tracker.md) |
| `Readme/` | 上游各版本的 readme 与 changelog，以及许可文件。readme 与 changelog 长期未更新且混有其他改版，以当前代码为准 |

## 构建

两种声音后端互斥，配置的差别只在这里：

| 配置 | 声音后端 | 构建方式 |
| --- | --- | --- |
| `Debug` / `Release` | WMP（`Runtime/Utils/Sound.WMP.cs`） | 需 Visual Studio 的 MSBuild（COM 引用） |
| `Debug-NAudio` / `Release-NAudio` | NAudio（`Runtime/Utils/Sound.NAudio.cs`） | `dotnet build` 可用 |

```bash
dotnet build Emuera.sln -c Debug-NAudio -p:Platform=x64
```

尽量显式指定 `-p:Platform` 以保证 `libwebp` 可用

[CI](.github/workflows/dotnet-build.yaml) 在推送 tag 时使用 MSBuild 发布 x64 的 WMP 与 NAudio 两个包，并校验 tag 与 `InformationalVersion` 一致

## 测试

具体信息见 [Emuera.Tests/README.md](Emuera.Tests/README.md)

## 贡献

代码风格见 [CONTRIBUTING.md](CONTRIBUTING.md)

## LICENSE

见 [Emuera.LICENSE.txt](Readme/License/Emuera.LICENSE.txt)、[私家改造版Emuera_readme.txt](Readme/私家改造版Emuera_readme.txt)

`libwebp` 见 [LibWebp.LICENSE.txt](Readme/License/LibWebp.LICENSE.txt)