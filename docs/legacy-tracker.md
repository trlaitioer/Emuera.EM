# 旧 tracker 归档

[归档分支](https://github.com/trlaitioer/Emuera.EM/tree/codex/archive-legacy-tracker/tracker) `codex/archive-legacy-tracker` 指向迁移前的[完整快照](https://github.com/trlaitioer/Emuera.EM/tree/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker) `4ebbc03ae8f0ef053c1647de9c371ba45e9f486c`。表中的原文链接固定到提交。与[较早的公开快照](https://github.com/trlaitioer/Emuera.EM/tree/502430abfbbacabc1d11d5558e21e43d91997851/tracker) 内容相同的文件链接到该版本。

用完整旧 ID 在下表查找承接票或原文。历史提交中的 `issues/36` 是主题目录内的编号。在可空迁移主题下，它对应 `nullable-migration-36`。 [迁移议题 #2](https://github.com/trlaitioer/Emuera.EM/issues/2)

从仓库根目录也可以直接读取完整快照。

```sh
git ls-tree -r --name-only 4ebbc03ae8f0ef053c1647de9c371ba45e9f486c -- tracker/
git show 4ebbc03ae8f0ef053c1647de9c371ba45e9f486c:tracker/nullable-migration/issues/36-pre-read-save-data-removal.md
```

## 旧规格

| 旧 ID | 现行入口 | 归档 |
| --- | --- | --- |
| `arg-contract-spec` | [内建函数实参槽](specs/argument-slots.md) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/arg-contract/spec.md) |
| `backlog-spec` | 见下方「backlog」 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/backlog/spec.md) |
| `dotnet-modernization-spec` | [ADR-0001](adr/0001-dotnet-modernization.md) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/dotnet-modernization/spec.md) |
| `multilingual-translation-spec` | [#22](https://github.com/trlaitioer/Emuera.EM/issues/22) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/multilingual-translation/spec.md) |
| `nullable-migration-spec` | [C# 可空引用契约](specs/nullable.md) · [读档暂存与扩展数据](specs/save-load.md) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/nullable-migration/spec.md) |
| `path-utils-spec` | [游戏路径与文件名](specs/paths.md) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/path-utils/spec.md) |
| `performance-optimization-spec` | 见下方「performance-optimization」 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/performance-optimization/spec.md) |
| `test-infrastructure-spec` | [测试工程](../Emuera.Tests/README.md) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/test-infrastructure/spec.md) |
| `ui-modernization-spec` | [游戏画布](specs/console-rendering.md) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/ui-modernization/spec.md) |

## 冻结审计报告

- 阶段 A 实参消费面。[原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/arg-contract/slots-consumption.md)
- 阶段 A 实参槽工作表。[原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/arg-contract/slots-worklist.md)
- 非空断言逐站复核。[原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/bang-sites.md)

现行命令见[生成文档与审计报告](README.md#生成文档与审计报告)。

## 旧议题

### arg-contract

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `arg-contract-01` | _ArgType.Array3D 读错标志位，三维数组实参槽不校验维度 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/01-argtype-array3d-flag-misread.md) |
| `arg-contract-02` | 规则块未写 OmitStart 时，该槽的省略检查恒假 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/02-argtype-omitstart-dead-check.md) |
| `arg-contract-03` | 函数体在「设计上可省略」的实参槽上解引用 null | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/arg-contract/issues/03-required-on-omittable-slot.md) |
| `arg-contract-04` | 实参契约表（阶段 A）：可省略槽的硬转型止血 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/04-argument-slots.md) |
| `arg-contract-05` | 实参契约深化：让声明兑现为槽位类型（阶段 B） | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/arg-contract/issues/05-argument-slots-type.md) |
| `arg-contract-06` | 槽位快照不跟随原地折叠：不可折叠外层会重新求值已折叠的内层实参 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/06-slot-snapshot-eval-timing.md) |
| `arg-contract-07` | 槽位访问器与旧 Required 同强度：非空没有被类型兜住，守卫纪律是唯一防线 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/07-accessor-runtime-assertions.md) |
| `arg-contract-08` | 旧签名的四个入口曾作为非虚门面长期保留，测试是它唯一的消费者（已移除） | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/08-legacy-facade-removal.md) |
| `arg-contract-09` | UniqueRestructure 收到的是副本，13 个类的写回被静默丢弃 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/09-uniquerestructure-argument-copy.md) |
| `arg-contract-10` | 迁移后残留的 4 处 VariableTerm 判别：2 处已改走 `Var(i)`，2 处不能换 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/arg-contract/issues/10-variable-term-discriminant-migration.md) |

### backlog

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `backlog-01` | ConfigData.FindByName 的键形态与写入方不对称 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/01-find-by-name-keys.md) |
| `backlog-02` | 实参契约表只在解析期声明、到运行期靠 cast 兑现 | 由 `arg-contract-04` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/02-arg-contract-table-redemption.md) |
| `backlog-03` | _ArgType.Array3D 读错标志位，三维数组实参槽不校验维度 | 由 `arg-contract-01` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/03-argtype-array3d-flag-misread.md) |
| `backlog-04` | 规则块未写 OmitStart 时，该槽的省略检查恒假 | 由 `arg-contract-02` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/04-argtype-omitstart-dead-check.md) |
| `backlog-05` | 解释器与控制台之间的 seam 落在具体上帝类上 | [#4](https://github.com/trlaitioer/Emuera.EM/issues/4) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/05-interpreter-console-narrow-seam.md) |
| `backlog-06` | 67 条命令的实参形状与行为分居两个 module，只靠 FunctionCode 名字相连 | [#5](https://github.com/trlaitioer/Emuera.EM/issues/5) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/06-command-table-one-declaration.md) |
| `backlog-07` | Config 把解析期开关与 120 项全局值袋混在一个 static module | [#6](https://github.com/trlaitioer/Emuera.EM/issues/6) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/backlog/issues/07-config-parse-options-seam.md) |
| `backlog-08` | 文件枚举策略藏在 Config 静态里，读取入口的缓存路径没有失败模式 | [#7](https://github.com/trlaitioer/Emuera.EM/issues/7) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/08-file-enumeration-explicit-options.md) |
| `backlog-09` | 折行 / 按钮切分 / 定位不是 module，测试只好手抄生产逻辑 | [#8](https://github.com/trlaitioer/Emuera.EM/issues/8) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/09-line-wrap-module.md) |
| `backlog-10` | 输入协议没有 module：WinForms 事件处理器直接写 eraBasic 输入结果 | [#9](https://github.com/trlaitioer/Emuera.EM/issues/9) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/10-input-contract-module.md) |
| `backlog-11` | 诊断返回值化：告警是副作用，console 为空时被静默丢弃 | [#10](https://github.com/trlaitioer/Emuera.EM/issues/10) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/11-parser-diagnostics-return-values.md) |
| `backlog-12` | GlobalStatic 的装配顺序只写在注释里 | [#11](https://github.com/trlaitioer/Emuera.EM/issues/11) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/12-runtime-data-root-object.md) |
| `backlog-13` | ConstantData 的构造器 interface 吞下整个 csv/ 目录 | [#12](https://github.com/trlaitioer/Emuera.EM/issues/12) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/13-constantdata-table-loader-split.md) |
| `backlog-14` | ErbLoader 的装载顺序策略内联，且只在两个入口中的一个实现 | [#13](https://github.com/trlaitioer/Emuera.EM/issues/13) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/14-erbloader-load-order-module.md) |
| `backlog-15` | Argument 基类的 IsConst/ConstStr/ConstInt 是一个没有 interface 的三字段协议 | [#14](https://github.com/trlaitioer/Emuera.EM/issues/14) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/15-argument-folded-value-protocol.md) |
| `backlog-16` | 函数体在「设计上可省略」的实参槽上解引用 null | 由 `arg-contract-03` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/16-required-on-omittable-slot.md) |
| `backlog-17` | ReduceArgumentOnLoad 的 ONCE 档不生效，等价于 NO | [#15](https://github.com/trlaitioer/Emuera.EM/issues/15) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/17-reduce-arg-on-load-once-noop.md) |
| `backlog-18` | 266 个内建函数的名字只存在于注册字典的键上，与实现分居两个文件且无一致性检查 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/18-builtin-method-registration.md) |
| `backlog-19` | 内建函数把名字当行为用：7 个类靠 `switch (Name)` 分派、16 处名字字面量散在实现里 | [#16](https://github.com/trlaitioer/Emuera.EM/issues/16) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/19-name-retirement.md) |
| `backlog-20` | 内建函数注册表在静态构造里建完，启动期失败只能以 TypeInitializationException 呈现 | [#17](https://github.com/trlaitioer/Emuera.EM/issues/17) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/20-builtin-registration-init-timing.md) |
| `backlog-21` | JSONConfig:setting.json 内容非法 JSON 时抛 JsonException,启动链无处处置 | [#18](https://github.com/trlaitioer/Emuera.EM/issues/18) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/backlog/issues/21-jsonconfig-invalid-json.md) |

### dotnet-modernization

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `dotnet-modernization-01` | UPDATECHECK 换用 HttpClient | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/01-webclient-httpclient.md) |
| `dotnet-modernization-02` | 存档读取:畸形文本序列检出加固 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/02-save-readstring-fallback.md) |
| `dotnet-modernization-03` | URL 打开:移除依赖 Framework 默认值的死路径 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/03-process-start-url.md) |
| `dotnet-modernization-04` | 决策:保留 Microsoft.VisualBasic 的 StrConv 依赖 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/04-visualbasic-strconv.md) |
| `dotnet-modernization-05` | 决策:DLL 导入维持 DllImport 声明 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/05-dllimport-libraryimport.md) |
| `dotnet-modernization-06` | 决策:C# 语言特性现代化策略 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/06-csharp-modernization-policy.md) |
| `dotnet-modernization-07` | 平台守卫:清理 Windows-only 目标下的恒真判断 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/dotnet-modernization/issues/07-windows-only-guards.md) |

### nullable-migration

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `nullable-migration-01` | Nullable:核心运行时迁移 | 由 `nullable-migration-08`、`nullable-migration-09`、`nullable-migration-11` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/01-core-runtime.md) |
| `nullable-migration-02` | Nullable:变量/表达式 + UI/Plugin 迁移 | 由 `nullable-migration-08`、`nullable-migration-10`、`nullable-migration-12` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/02-variable-expression-ui-plugin.md) |
| `nullable-migration-03` | Nullable:csproj 恢复 enable 收尾 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/03-csproj-enable-cleanup.md) |
| `nullable-migration-04` | Nullable:其余文件清尾 | 由 `nullable-migration-07`、`nullable-migration-08`、`nullable-migration-09`、`nullable-migration-10`、`nullable-migration-11`、`nullable-migration-12` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/04-misc-files.md) |
| `nullable-migration-05` | Nullable:省略参数可空贯通 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/05-omitted-args-nullable.md) |
| `nullable-migration-06` | Nullable:横切可空契约决策 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/06-nullability-contracts.md) |
| `nullable-migration-07` | Nullable:解析链启用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/07-parser-chain.md) |
| `nullable-migration-08` | Nullable:表达式与方法链启用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/08-expression-method-chain.md) |
| `nullable-migration-09` | Nullable:参数与指令链启用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/09-argument-instruction-chain.md) |
| `nullable-migration-10` | Nullable:变量与数据链启用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/10-variable-data-chain.md) |
| `nullable-migration-11` | Nullable:进程与加载链启用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/11-process-loader-chain.md) |
| `nullable-migration-12` | Nullable:UI 与外围启用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/12-ui-periphery.md) |
| `nullable-migration-13` | Nullable:`!` 收敛(冗余删除与单点断言入口) | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/13-null-forgiving-cleanup.md) |
| `nullable-migration-14` | Nullable:非空断言的逐点复核 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/14-null-assertion-audit.md) |
| `nullable-migration-15` | Nullable:省略参数以 null 到达消费点 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/15-omitted-args-null-consumers.md) |
| `nullable-migration-16` | Nullable:配对被破坏时未标记错误行，空跳转目标进入执行期 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/16-nestcheck-pairing-invariant.md) |
| `nullable-migration-17` | Nullable:容器与成员断言的零散守卫缺失 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/17-container-member-guards.md) |
| `nullable-migration-18` | Nullable:无保护的 as 转换三处（含同源的 MAXARRAY/MINARRAY） | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/18-unguarded-as-casts.md) |
| `nullable-migration-19` | Nullable:插件与错误报告路径的空引用 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/19-plugin-and-error-path-nulls.md) |
| `nullable-migration-20` | Nullable:解析期以 null 求值上下文折叠元素数表达式 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/20-debug-eval-context-null.md) |
| `nullable-migration-21` | Nullable:其余成员与局部非空断言的复核 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/21-remaining-member-assertions.md) |
| `nullable-migration-22` | Nullable:保留断言的理由注释与契约诚实化 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/22-rationale-comments-and-contracts.md) |
| `nullable-migration-23` | Nullable:ERD 变量码的键索引在 KeywordToInteger 处解引用 null | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/23-erd-keyword-index-null.md) |
| `nullable-migration-24` | Nullable:首个 `@函数` 之前的 VARI/VARS 语句解引用 null 的 parentLine | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/24-vari-before-first-label.md) |
| `nullable-migration-25` | Nullable:显式 `@子键` 的局部变量引用在无父标签时解引用 null | 由 `nullable-migration-19` 承接 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/25-explicit-subkey-without-parent-label.md) |
| `nullable-migration-26` | Nullable:调试监视列表的空表达式解引用 null | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/26-debug-watch-empty-expression.md) |
| `nullable-migration-27` | Nullable:GOTO 跳入 REPEAT/FOR 体时 BREAK 解引用 null 的 `LoopCounter` | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/27-break-loopcounter-skipped-header.md) |
| `nullable-migration-28` | Nullable:坏输入路径解引用 null（配置/脚本/存档） | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/28-bad-input-nulls.md) |
| `nullable-migration-29` | Nullable:`GetKeywordDictionary` 的 Try 拆分与非空入口 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/29-keyword-dictionary-nonnull-entry.md) |
| `nullable-migration-30` | 存档坏输入抛出的 FileEE 落到「予期せぬエラー」+ 堆栈 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/30-fileee-unexpected-error-bucket.md) |
| `nullable-migration-31` | Nullable:可空化被非空消费方挡住的断言链 | [#19](https://github.com/trlaitioer/Emuera.EM/issues/19) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/31-assertion-honesty-nonnull-consumers.md) |
| `nullable-migration-32` | Nullable:断言审计收尾时发现的三处疑似缺陷与一处不一致 | 已复核，见 [#2](https://github.com/trlaitioer/Emuera.EM/issues/2) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/32-assertion-audit-findings.md) |
| `nullable-migration-33` | Nullable:`ConstantData.output` 的非空断言与测试夹具的 null console 冲突 | [#20](https://github.com/trlaitioer/Emuera.EM/issues/20) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/33-constantdata-console-nullability.md) |
| `nullable-migration-34` | 坏存档仍能以非 FileEE 异常逃逸的其余出口 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/34-save-reader-remaining-escapes.md) |
| `nullable-migration-35` | 存档写入侧的错误分级与「写失败毁旧档」 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/35-savedata-write-errors.md) |
| `nullable-migration-36` | 读档失败之前先删掉了当前存档 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/nullable-migration/issues/36-pre-read-save-data-removal.md) |
| `nullable-migration-37` | 头部 catch 把介质故障当成「不是本格式」 | [#3](https://github.com/trlaitioer/Emuera.EM/issues/3) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/37-header-catch-swallows-fileee.md) |
| `nullable-migration-38` | 零串计数被截断成 int 后静默读短 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/nullable-migration/issues/38-zero-run-count-truncation.md) |
| `nullable-migration-39` | 读档失败后普通变量与角色已被复位 | [#1](https://github.com/trlaitioer/Emuera.EM/issues/1) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/4ebbc03ae8f0ef053c1647de9c371ba45e9f486c/tracker/nullable-migration/issues/39-failed-load-state-rollback.md) |

### path-utils

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `path-utils-01` | 路径:引擎与 UI 侧的前缀判断、相对化与剥前缀收敛 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/path-utils/issues/01-engine-path-prefix.md) |
| `path-utils-02` | 路径:根目录类型化与「游戏目录」唯一定义 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/path-utils/issues/02-root-typing.md) |
| `path-utils-03` | 路径:剩余硬编码分隔符与死代码清理 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/path-utils/issues/03-path-literal-cleanup.md) |
| `path-utils-04` | 路径:存档路径统一到 GamePath | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/path-utils/issues/04-save-path-unification.md) |
| `path-utils-05` | 路径:审查发现的三处入口回归修复 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/path-utils/issues/05-review-regression-fixes.md) |
| `path-utils-06` | 路径:审查遗留问题(四处修复 + 一处伪回归核实) | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/path-utils/issues/06-review-leftovers.md) |

### performance-optimization

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `performance-optimization-01` | 性能:字符串拼接优化 | [#21](https://github.com/trlaitioer/Emuera.EM/issues/21) | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/01-string-concat.md) |
| `performance-optimization-02` | 性能:CALLFORM 动态调用缓存 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/02-callform-cache.md) |
| `performance-optimization-03` | 性能:解析期 Span 化 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/03-parse-span.md) |
| `performance-optimization-04` | 性能:解析并行化 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/04-parse-parallel.md) |
| `performance-optimization-05` | 性能:CSV 查询字典化 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/05-csv-dictionary.md) |
| `performance-optimization-06` | CS8981:小写 alias 处理决策 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/06-cs8981-alias.md) |
| `performance-optimization-07` | 告警:重复角色 No 收敛为每组一条 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/performance-optimization/issues/07-chara-duplicate-warning.md) |

### test-infrastructure

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `test-infrastructure-01` | 测试工程搭建 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/test-infrastructure/issues/01-test-project-scaffold.md) |
| `test-infrastructure-02` | Phase 1 测试用例 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/test-infrastructure/issues/02-phase1-test-cases.md) |
| `test-infrastructure-03` | Phase 2+:StrForm 与后续 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/test-infrastructure/issues/03-phase2-beyond.md) |
| `test-infrastructure-04` | 测试用例扩展:FORM 字符串 / 表达式 / 常用函数 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/test-infrastructure/issues/04-real-game-fixtures.md) |

### ui-modernization

| 旧 ID | 主题 | 去向 | 归档 |
| --- | --- | --- | --- |
| `ui-modernization-01` | 决策:WinForms 层升级需求评估 | 已结案 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/ui-modernization/issues/01-winforms-assessment.md) |
| `ui-modernization-02` | 画布与字体缩放:运行时窗口缩放与高 DPI | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/ui-modernization/issues/02-dpi-scale.md) |
| `ui-modernization-03` | 渲染字号反查:全角单元格宽对齐默认字体,em 基准保持不变 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/ui-modernization/issues/03-monospace-grid.md) |
| `ui-modernization-04` | GDI+ 文字绘制档:现状与缺陷修复 | 已实施 | [原文](https://github.com/trlaitioer/Emuera.EM/blob/502430abfbbacabc1d11d5558e21e43d91997851/tracker/ui-modernization/issues/04-graphics-text-mode.md) |
