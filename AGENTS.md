# AGENTS.md

## 文档地图
- 仓库布局、项目说明、构建测试命令见 [README.md](README.md)
- 代码文档风格与规范见 [CONTRIBUTING.md](CONTRIBUTING.md)
- 创建或拆分 GitHub Issue 时见 [议题规范](docs/issue-tracker.md)
- 其余见 [长期文档索引](docs/README.md)

## 重要须知

- 新议题在 GitHub Issues 中跟踪。现行规格放 `docs/specs/`，其余长期参考写 `docs/`。临时产物放 `.temp/`（已 gitignore，不入库）

### Git

1. 会话开始时，若用户没有显式说明，报告当前分支，询问用户是否在该分支继续、是否拉取远程分支
2. 需要推送的改动在从 `develop` 切出的工作分支上做，完成后 merge/rebase 回 `develop`
    - 触发条件：用户要求推送，或本次新增被 git 跟踪的文件
3. 仅在用户明确要求时 commit；格式见 `CONTRIBUTING.md` 的「commit 规范」

### 代码 / 文档

1. 未显式说明时，代码修改与 review 都不覆盖 `EmueraPluginExample/`
2. 改动后按需构建、测试
    - 改 `Emuera/` 下 C# -> 构建 + 测试
    - 改 `Emuera.Tests/`，或改动影响被测行为 -> 测试
    - 编译或测试失败 -> 收集错误信息交用户决定是否、如何修复
3. 修改、新增代码或文档之前应当先出方案给用户评审，用户确认后再修改文件
    - 用户只对方案某部分提出修改，不代表对剩余部分无异议，仍需用用户确认剩余部分
4. 注释/文档需要从用户的角度撰写
    - 注释与文档保持精炼，不记录不可验证的来源

