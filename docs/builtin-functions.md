# 内建函数（式中関数）契约表

本表覆盖 `Emuera/Runtime/Script/Statements/Function/BuiltinMethod/` 目录下 `[BuiltinMethod]` 工厂注册的全部 **266 个式中関数注册名**（183 个 `FunctionMethod` 子类）的实参契约。
形状、合法实参数与返回类型由代码机械推出（生成器 [`tracker/arg-contract/builtin-contract.py`](../tracker/arg-contract/builtin-contract.py)，可复跑）；说明列参考 [upstream 在线文档](https://evilmask.gitlab.io/emuera.em.doc/Reference/index.html) 并逐条按本仓代码核对，**不一致时以代码为准**。

## 凡例

**实参形状**（逐个槽位，逗号分隔；按代码里 `ArgTypeList` / `argumentTypeArray` 的声明推出）：

本表引用的代码位置（相对仓库根）：注册表 `Emuera/Runtime/Script/Statements/Function/Creator.cs`、
规则表与函数体 `Emuera/Runtime/Script/Statements/Function/BuiltinMethod/` 目录（每个注册名的实现文件与工厂 = `grep -rnF '[BuiltinMethod("NAME")]'` 的命中处）、
检查器 `Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs`。

| 记号 | 含义 |
| --- | --- |
| `int` / `str` | 该槽收整数 / 字符串 |
| `any` | 两种都收（`ArgType.Any`，不做类型检查） |
| `ref ` 前缀 | 该槽要**变量实体**而非值（`ArgType.Ref`：整数/字符串变量、数组变量、角色变量…）；字面量、表达式、`IsCalc` 变量与 `const` 变量都被拒 |
| `ref*` 前缀 | 同 `ref `，但该槽连 `const` 变量也收（`ArgType.AllowConstRef`）；字面量、表达式与 `IsCalc` 变量照样被拒 |
| `[]` `[,]` `[,,]` | 引用位的维度：一维 / 二维 / 三维数组 |
| `[…]` | 任意维数组（至少一维） |
| `随槽0` | 该槽的类型跟随 0 号槽（`ArgType.SameAsFirst`）：0 号槽收整数它就收整数，0 号槽收字符串它就收字符串；变长版本见 `…` |
| `(角色变量)` | 该槽还要求是角色数据变量（`ArgType.CharacterData`） |
| `…` | 变长尾：该槽起按末位规则重复（`ArgType.Variadic*`） |
| `?` | **数量上可省略**：从这个槽起可以少写（`i >= OmitStart`）；少写不产生空槽 |
| `-` | **可显式留空**：`f(a,,c)` 这种空槽合法（该槽未标 `ArgType.NotNull`）；具体空槽取什么值见「空槽语义」与「说明」 |
| `?` 与 `-` 叠加 | 两个记号可同时出现：`int?-` 表示该槽既可数量省略又可显式留空（例：`ARRAYMSORTEX` 的形状 `str, ref str[], int?-, int?`） |
| 无标记 | 必填且不可留空：显式空槽报 `CodeEE(ArgCanNotBeNull)` |

**合法实参数**：`恰好 2` = 恰好 2 个；`1–3` = 1 到 3 个；`>= 1` = 1 个以上（变长尾）。
多个候选行（同一注册名的多行）表示**多个规则段**：只要有一行完全匹配就能通过（`FunctionMethod.CheckArgumentTypeEx`
逐段试，首个通过即通过），个数/类型/空槽三轴都在行内。

**重要**：「数量省略」与「显式留空」是两件正交的事——`f(a,b,)` 只吞掉一个尾逗号（列表更短、不含 null），
而 `f(a,b,,)` / `f(a,,b)` 会在列表里留下 null 元素（见 [csv-erb-syntax.md](csv-erb-syntax.md) 第 10 节第 8 条）。

**其它口径**：
- 「合法实参数」与逐槽判据都来自 `Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs` 的
  `FunctionMethod.CheckArgumentTypeEx`；`argumentTypeArray`（`Type[]`）的类个数必须恰好相等、逐槽非空。
- 失败文案：个数不足 `NotEnoughArgs`、个数不符 `ArgsCountNotMatches`、过多 `TooManyFuncArgs`、显式空槽 `ArgCanNotBeNull`、类型不符 `ArgIsNotInt`/`ArgIsNotStr`、引用位不符 `ArgIsNotVar`/`ArgIsNotIntVar`/`ArgIsNotStrVar`/`ArgIsNotNDIntArray` 等。
- 这些检查默认在**首次执行该行**时进行（`ReduceArgumentOnLoad` 默认 `NO`），只有 `AnalysisMode` / `ReduceArgumentOnLoad=YES` / 指令声明 `FORCE_SETARG` / `FOR` 行才提前到装载期。
- 本表只覆盖**式中関数**（能用在小括号表达式里的内建函数）；**指令**（`PRINT`、`FOR` 之类）走另一套实参模型，且两者极性相反，见 [csv-erb-syntax.md](csv-erb-syntax.md) 第 10 节第 9 条。
- 「说明」列 = upstream 文档说了什么（摘要）＋ 按代码核对后的差异与边界。**upstream 与代码不一致、或上游没写而代码有约束的地方，一律以代码为准并写在该列里**；少数注册名（`GETSPCHARA`、`GETNUMB`、`UNICODEBYTE`、`HOTKEY_STATE(_INIT)`、`GETDOINGFUNCTION`）在 upstream 站点上没有对应页面，其说明纯按代码写成。


## 索引（按注册名）

| 注册名 | 返回 | 类别 | 首个候选形状 |
| --- | --- | --- | --- |
| `ABS` | int | 算术 | int |
| `ALLSAMES` | int | 算术 | any, 随槽0…, （尾部：自第 2 槽起按末位规则重复） |
| `ARRAYMSORT` | int | 变量操作・变量参照・CSV 参照 | ref any[], ref any[…]…?, （尾部：自第 2 槽起按末位规则重复） |
| `ARRAYMSORTEX` | int | 变量操作・变量参照・CSV 参照 | str, ref str[], int?-, int? |
| `BARSTR` | str | 显示操作・字体操作・显示规格参照 | int, int, int |
| `BITMAP_CACHE_ENABLE` | int | 显示操作・字体操作・显示规格参照 | int |
| `CBGCLEAR` | int | 图像处理相关 | （无实参） |
| `CBGCLEARBUTTON` | int | 图像处理相关 | （无实参） |
| `CBGREMOVEBMAP` | int | 其他 | （无实参） |
| `CBGREMOVERANGE` | int | 图像处理相关 | int, int |
| `CBGSETBMAPG` | int | 图像处理相关 | int |
| `CBGSETBUTTONSPRITE` | int | 图像处理相关 | int, str, str, int, int, int, str? |
| `CBGSETG` | int | 图像处理相关 | int, int, int, int |
| `CBGSETSPRITE` | int | 图像处理相关 | str, int, int, int |
| `CBRT` | int | 算术 | int |
| `CHARATU` | str | 字符串操作・参照 | str, int |
| `CHKCHARADATA` | int | 存档数据操作 | str |
| `CHKDATA` | int | 存档数据操作 | int |
| `CHKFONT` | int | 显示操作・字体操作・显示规格参照 | str |
| `CLEARMEMORY` | int | 其他 | （无实参） |
| `CLIENTHEIGHT` | int | 其他 | （无实参） |
| `CLIENTWIDTH` | int | 其他 | （无实参） |
| `CMATCH` | int | 算术 | ref* any[] (角色变量), 随槽0, int?-, int?- |
| `COLOR_FROMNAME` | int | 显示操作・字体操作・显示规格参照 | str |
| `COLOR_FROMRGB` | int | 显示操作・字体操作・显示规格参照 | int, int, int |
| `CONVERT` | str | 算术 | int, int |
| `CSVABL` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVBASE` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVCALLNAME` | str | 变量操作・变量参照・CSV 参照 | int, int?- |
| `CSVCFLAG` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVCSTR` | str | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVEQUIP` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVEXP` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVJUEL` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVMARK` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVMASTERNAME` | str | 变量操作・变量参照・CSV 参照 | int, int?- |
| `CSVNAME` | str | 变量操作・变量参照・CSV 参照 | int, int?- |
| `CSVNICKNAME` | str | 变量操作・变量参照・CSV 参照 | int, int?- |
| `CSVRELATION` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CSVTALENT` | int | 变量操作・变量参照・CSV 参照 | int, int, int?- |
| `CURRENTALIGN` | str | 显示操作・字体操作・显示规格参照 | （无实参） |
| `CURRENTREDRAW` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `DT_CELL_GET` | int | DataTable（数据库）系 | str, int, str, int? |
| `DT_CELL_GETS` | str | DataTable（数据库）系 | str, int, str, int? |
| `DT_CELL_ISNULL` | int | DataTable（数据库）系 | str, int, str, int? |
| `DT_CELL_SET` | int | DataTable（数据库）系 | str, int, str, any?-, int? |
| `DT_CLEAR` | int | DataTable（数据库）系 | str |
| `DT_COLUMN_ADD` | int | DataTable（数据库）系 | str, str, any?, int? |
| `DT_COLUMN_EXIST` | int | DataTable（数据库）系 | str, str |
| `DT_COLUMN_LENGTH` | int | DataTable（数据库）系 | str |
| `DT_COLUMN_NAMES` | int | DataTable（数据库）系 | str, ref str[]?- |
| `DT_COLUMN_REMOVE` | int | DataTable（数据库）系 | str, str |
| `DT_CREATE` | int | DataTable（数据库）系 | str |
| `DT_EXIST` | int | DataTable（数据库）系 | str |
| `DT_FROMXML` | int | DataTable（数据库）系 | str, str, str |
| `DT_NOCASE` | int | DataTable（数据库）系 | str, int |
| `DT_RELEASE` | int | DataTable（数据库）系 | str |
| `DT_ROW_ADD` | int | DataTable（数据库）系 | str, str…?, any…?-, （尾部：自第 2 槽起按末位规则重复） |
| `DT_ROW_LENGTH` | int | DataTable（数据库）系 | str |
| `DT_ROW_REMOVE` | int | DataTable（数据库）系 | str, int |
| `DT_ROW_SET` | int | DataTable（数据库）系 | str, int, str…, any…, （尾部：自第 3 槽起按末位规则重复） |
| `DT_SELECT` | int | DataTable（数据库）系 | str, str?-, str?-, ref int[]? |
| `DT_TOXML` | str | DataTable（数据库）系 | str, ref str[]? |
| `ENCODETOUNI` | int | 字符串操作・参照 | str, int?- |
| `ENUMFILES` | int | 其他 | str, str?, int?, ref str[]? |
| `ENUMFUNCBEGINSWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMFUNCENDSWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMFUNCWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMMACROBEGINSWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMMACROENDSWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMMACROWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMVARBEGINSWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMVARENDSWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ENUMVARWITH` | int | 变量操作・变量参照・CSV 参照 | str, ref str[]? |
| `ERDNAME` | str | 变量操作・变量参照・CSV 参照 | ref* any, int, int? |
| `ESCAPE` | str | 字符串操作・参照 | str |
| `EXISTCSV` | int | 角色操作・参照 | int, int?- |
| `EXISTFILE` | int | 其他 | str |
| `EXISTFUNCTION` | int | 函数系（CALL 等） | str, int? |
| `EXISTMETH` | int | 函数系（CALL 等） | str |
| `EXISTSOUND` | int | 声音系 | str |
| `EXISTVAR` | int | 变量操作・变量参照・CSV 参照 | str |
| `EXPONENT` | int | 其他 | int |
| `FINDCHARA` | int | 角色操作・参照 | ref any (角色变量), 随槽0, int?-, int?- |
| `FINDELEMENT` | int | 变量操作・变量参照・CSV 参照 | ref* any[], 随槽0, int?-, int?-, int?- |
| `FINDLASTCHARA` | int | 角色操作・参照 | ref any (角色变量), 随槽0, int?-, int?- |
| `FINDLASTELEMENT` | int | 变量操作・变量参照・CSV 参照 | ref* any[], 随槽0, int?-, int?-, int?- |
| `FIND_CHARADATA` | int | 存档数据操作 | str?- |
| `FLOWINPUT` | int | 调试辅助・系统流程控制 | int, int?, int?, int? |
| `FLOWINPUTS` | int | 其他 | int, str? |
| `GCLEAR` | int | 图像处理相关 | int, int |
| `GCREATE` | int | 图像处理相关 | int, int, int |
| `GCREATED` | int | 图像处理相关 | int |
| `GCREATEFROMFILE` | int | 图像处理相关 | int, str, int? |
| `GDASHSTYLE` | int | 图像处理相关 | int, int, int |
| `GDISPOSE` | int | 图像处理相关 | int |
| `GDRAWG` | int | 图像处理相关 | int, int, int, int, int, int, int, int, int, int, ref* int[,]?- |
| `GDRAWGWITHMASK` | int | 图像处理相关 | int, int, int, int, int |
| `GDRAWGWITHROTATE` | int | 图像处理相关 | int, int, int, int?, int? |
| `GDRAWLINE` | int | 图像处理相关 | int, int, int, int, int |
| `GDRAWSPRITE` | int | 图像处理相关 | int, str |
| `GDRAWTEXT` | int | 图像处理相关 | int, str, int?, int? |
| `GETBGCOLOR` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETBIT` | int | 算术 | int, int |
| `GETCHARA` | int | 角色操作・参照 | int, int?- |
| `GETCOLOR` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETCONFIG` | int | 其他 | str |
| `GETCONFIGS` | str | 其他 | str |
| `GETDEFBGCOLOR` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETDEFCOLOR` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETDISPLAYLINE` | str | 显示操作・字体操作・显示规格参照 | int |
| `GETDOINGFUNCTION` | str | 其他 | （无实参） |
| `GETEXPLV` | int | 变量操作・变量参照・CSV 参照 | int, int |
| `GETFOCUSCOLOR` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETFONT` | str | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETKEY` | int | AWAIT 相关 | int |
| `GETKEYTRIGGERED` | int | AWAIT 相关 | int |
| `GETLINESTR` | str | PRINT 系 | str |
| `GETMEMORYUSAGE` | int | 其他 | （无实参） |
| `GETMETH` | int | 函数系（CALL 等） | str, int?-, any…?-, （尾部：自第 3 槽起按末位规则重复） |
| `GETMETHS` | str | 函数系（CALL 等） | str, str?-, any…?-, （尾部：自第 3 槽起按末位规则重复） |
| `GETMILLISECOND` | int | 日期・时刻获取 | （无实参） |
| `GETNUM` | int | 变量操作・变量参照・CSV 参照 | ref* any, str, int? |
| `GETNUMB` | int | 其他 | str, str |
| `GETPALAMLV` | int | 变量操作・变量参照・CSV 参照 | int, int |
| `GETSECOND` | int | 日期・时刻获取 | （无实参） |
| `GETSPCHARA` | int | 其他 | int |
| `GETSTYLE` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `GETTEXTBOX` | str | 其他 | （无实参） |
| `GETTIME` | int | 日期・时刻获取 | （无实参） |
| `GETTIMES` | str | 日期・时刻获取 | （无实参） |
| `GETVAR` | int | 变量操作・变量参照・CSV 参照 | str |
| `GETVARS` | str | 变量操作・变量参照・CSV 参照 | str |
| `GFILLRECTANGLE` | int | 图像处理相关 | int, int, int, int, int |
| `GGETBRUSH` | int | 图像处理相关 | int |
| `GGETCOLOR` | int | 图像处理相关 | int, int, int |
| `GGETFONT` | str | 图像处理相关 | int |
| `GGETFONTSIZE` | int | 图像处理相关 | int |
| `GGETFONTSTYLE` | int | 图像处理相关 | int |
| `GGETPEN` | int | 图像处理相关 | int |
| `GGETPENWIDTH` | int | 图像处理相关 | int |
| `GGETTEXTSIZE` | int | 图像处理相关 | str, str, int, int? |
| `GHEIGHT` | int | 图像处理相关 | int |
| `GLOAD` | int | 图像处理相关 | int, int |
| `GROUPMATCH` | int | 算术 | any, 随槽0…, （尾部：自第 2 槽起按末位规则重复） |
| `GSAVE` | int | 图像处理相关 | int, int |
| `GSETBRUSH` | int | 图像处理相关 | int, int |
| `GSETCOLOR` | int | 图像处理相关 | int, int, int, int |
| `GSETFONT` | int | 图像处理相关 | int, str, int, int? |
| `GSETPEN` | int | 图像处理相关 | int, int, int |
| `GWIDTH` | int | 图像处理相关 | int |
| `HOTKEY_STATE` | int | 其他 | int, int |
| `HOTKEY_STATE_INIT` | int | 其他 | int |
| `HTML_ESCAPE` | str | HTML 系 | str |
| `HTML_GETPRINTEDSTR` | str | HTML 系 | int? |
| `HTML_POPPRINTINGSTR` | str | HTML 系 | （无实参） |
| `HTML_STRINGLEN` | int | HTML 系 | str, int? |
| `HTML_STRINGLINES` | int | HTML 系 | str, int |
| `HTML_SUBSTRING` | str | HTML 系 | str, int |
| `HTML_TOPLAINTEXT` | str | HTML 系 | str |
| `INRANGE` | int | 算术 | int, int, int |
| `INRANGEARRAY` | int | 算术 | ref* int[], int, int, int?-, int?- |
| `INRANGECARRAY` | int | 算术 | ref* int[] (角色变量), int, int, int?-, int?- |
| `ISACTIVE` | int | AWAIT 相关 | （无实参） |
| `ISDEFINED` | int | 变量操作・变量参照・CSV 参照 | str |
| `ISNUMERIC` | int | 字符串操作・参照 | str |
| `ISSKIP` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `LIMIT` | int | 算术 | int, int, int |
| `LINEISEMPTY` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `LOADTEXT` | str | 存档数据操作 | any, int?, int? |
| `LOG` | int | 算术 | int |
| `LOG10` | int | 算术 | int |
| `MAP_CLEAR` | int | MAP（关联数组）系 | str |
| `MAP_CREATE` | int | MAP（关联数组）系 | str |
| `MAP_EXIST` | int | MAP（关联数组）系 | str |
| `MAP_FROMXML` | int | MAP（关联数组）系 | str, str |
| `MAP_GET` | str | MAP（关联数组）系 | str, str |
| `MAP_GETKEYS` | str | MAP（关联数组）系 | str, int? |
| `MAP_HAS` | int | MAP（关联数组）系 | str, str |
| `MAP_RELEASE` | int | MAP（关联数组）系 | str |
| `MAP_REMOVE` | int | MAP（关联数组）系 | str, str |
| `MAP_SET` | int | MAP（关联数组）系 | str, str, str |
| `MAP_SIZE` | int | MAP（关联数组）系 | str |
| `MAP_TOXML` | str | MAP（关联数组）系 | str |
| `MATCH` | int | 算术 | ref* any[], 随槽0, int?-, int?- |
| `MAX` | int | 算术 | int, int…?, （尾部：自第 2 槽起按末位规则重复） |
| `MAXARRAY` | int | 算术 | ref* int[], int?-, int?- |
| `MAXCARRAY` | int | 算术 | ref* int[] (角色变量), int?-, int?- |
| `MESSKIP` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `MIN` | int | 算术 | int, int…?, （尾部：自第 2 槽起按末位规则重复） |
| `MINARRAY` | int | 算术 | ref* int[], int?-, int?- |
| `MINCARRAY` | int | 算术 | ref* int[] (角色变量), int?-, int?- |
| `MONEYSTR` | str | 显示操作・字体操作・显示规格参照 | int, str?- |
| `MOUSEB` | str | AWAIT 相关 | （无实参） |
| `MOUSESKIP` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `MOUSEX` | int | AWAIT 相关 | （无实参） |
| `MOUSEY` | int | AWAIT 相关 | （无实参） |
| `MOVETEXTBOX` | int | 其他 | int, int, int |
| `NOSAMES` | int | 算术 | any, 随槽0…, （尾部：自第 2 槽起按末位规则重复） |
| `OUTPUTLOG` | int | 存档数据操作 | str?, int? |
| `POWER` | int | 算术 | int, int |
| `PRINTCLENGTH` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `PRINTCPERLINE` | int | 显示操作・字体操作・显示规格参照 | （无实参） |
| `RAND` | int | 其他 | int, int? |
| `REGEXPMATCH` | int | 字符串操作・参照 | str, str, int? |
| `REPLACE` | str | 字符串操作・参照 | str, str, str, int? |
| `RESUMETEXTBOX` | int | 其他 | int, int, int |
| `SAVENOS` | int | 存档数据操作 | （无实参） |
| `SAVETEXT` | int | 存档数据操作 | str, any, int?, int? |
| `SETANIMETIMER` | int | 图像处理相关 | int |
| `SETTEXTBOX` | int | 其他 | str |
| `SETVAR` | int | 变量操作・变量参照・CSV 参照 | str, any |
| `SIGN` | int | 算术 | int |
| `SPRITEANIMEADDFRAME` | int | 图像处理相关 | str, int, int, int, int, int, int, int, int |
| `SPRITEANIMECREATE` | int | 图像处理相关 | str, int, int |
| `SPRITECREATE` | int | 图像处理相关 | str, int |
| `SPRITECREATED` | int | 图像处理相关 | str |
| `SPRITEDISPOSE` | int | 图像处理相关 | str |
| `SPRITEDISPOSEALL` | int | 图像处理相关 | int |
| `SPRITEGETCOLOR` | int | 图像处理相关 | str, int, int |
| `SPRITEHEIGHT` | int | 图像处理相关 | str |
| `SPRITEMOVE` | int | 图像处理相关 | str, int, int |
| `SPRITEPOSX` | int | 图像处理相关 | str |
| `SPRITEPOSY` | int | 图像处理相关 | str |
| `SPRITESETPOS` | int | 图像处理相关 | str, int, int |
| `SPRITEWIDTH` | int | 图像处理相关 | str |
| `SQRT` | int | 算术 | int |
| `STRCOUNT` | int | 字符串操作・参照 | str, str |
| `STRFIND` | int | 字符串操作・参照 | str, str, int?- |
| `STRFINDU` | int | 字符串操作・参照 | str, str, int?- |
| `STRFORM` | str | 字符串操作・参照 | str |
| `STRJOIN` | str | 字符串操作・参照 | ref* any[…], str?-, int?-, int?- |
| `STRLENS` | int | 字符串操作・参照 | str |
| `STRLENSU` | int | 字符串操作・参照 | str |
| `SUBSTRING` | str | 字符串操作・参照 | str, int?-, int?- |
| `SUBSTRINGU` | str | 字符串操作・参照 | str, int?-, int?- |
| `SUMARRAY` | int | 算术 | ref int[…], int?-, int?- |
| `SUMCARRAY` | int | 算术 | ref* int[…] (角色变量), int?-, int?- |
| `TOFULL` | str | 字符串操作・参照 | str |
| `TOHALF` | str | 字符串操作・参照 | str |
| `TOINT` | int | 字符串操作・参照 | str |
| `TOLOWER` | str | 字符串操作・参照 | str |
| `TOSTR` | str | 字符串操作・参照 | int, str?- |
| `TOUPPER` | str | 字符串操作・参照 | str |
| `UNICODE` | str | 字符串操作・参照 | int |
| `UNICODEBYTE` | int | 其他 | str |
| `VARSETEX` | int | 变量操作・变量参照・CSV 参照 | str, any, int?, int?, int? |
| `VARSIZE` | int | 变量操作・变量参照・CSV 参照 | str, int?- |
| `XML_ADDATTRIBUTE` | int | XML 系 | int, str, str, str?, int?, int? |
| `XML_ADDATTRIBUTE_BYNAME` | int | XML 系 | str, str, str, str?, int?, int? |
| `XML_ADDNODE` | int | XML 系 | int, str, str, int?, int? |
| `XML_ADDNODE_BYNAME` | int | XML 系 | str, str, str, int?, int? |
| `XML_DOCUMENT` | int | XML 系 | any, str |
| `XML_EXIST` | int | XML 系 | any |
| `XML_GET` | int | XML 系 | any, str, int?, int? |
| `XML_GET_BYNAME` | int | XML 系 | any, str, int?, int? |
| `XML_RELEASE` | int | XML 系 | any |
| `XML_REMOVEATTRIBUTE` | int | XML 系 | int, str, int? |
| `XML_REMOVEATTRIBUTE_BYNAME` | int | XML 系 | str, str, int? |
| `XML_REMOVENODE` | int | XML 系 | int, str, int? |
| `XML_REMOVENODE_BYNAME` | int | XML 系 | str, str, int? |
| `XML_REPLACE` | int | XML 系 | any, str |
| `XML_REPLACE_BYNAME` | int | XML 系 | str, str, str, int? |
| `XML_SET` | int | XML 系 | int, str, str, int?, int? |
| `XML_SET_BYNAME` | int | XML 系 | str, str, str, int?, int? |
| `XML_TOSTR` | str | XML 系 | any |

## 分族契约

### PRINT 系

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `GETLINESTR` | str | str | 恰好 1 | —（省略→个数错；留空→CodeEE(ArgCanNotBeNull)） | 按当前绘制宽度重复模式串并裁剪，返回 CUSTOMDRAWLINE/DRAWLINEFORM 会显示的整行字符串；实参为空串抛 CodeEE(ArgIsEmptyString)；非空但显示宽度为 0（如 U+200B）会死循环；可折叠，可用于 #DIM 常量初值 |

### 显示操作・字体操作・显示规格参照

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `BARSTR` | str | int, int, int | 恰好 3 | —（省略→个数错；留空→CodeEE(ArgCanNotBeNull)） | 按 当前值/最大值/长度 生成 [***..] 形式的进度条字符串并返回；三实参必填；max<=0、length<=0、length>=100 抛 CodeEE；填充字符取自 _replace.csv 的 BARChar1/2 |
| `BITMAP_CACHE_ENABLE` | int | int | 恰好 1 | — | 设置之后新增的显示行是否启用位图缓存加速绘制，返回固定值 0；非 0 启用、0 关闭；标志不自动复位，对之后所有新增行持续生效（并非只影响下一行）；文档写返回 void，实际返回 0 |
| `CHKFONT` | int | str | 恰好 1 | —（唯一实参不可省略、不可留空） | 检查指定名称的字体是否可用，已安装返回 1，否则返回 0；兼查游戏 font/ 目录加载的私有字体；名称按序数精确比较、大小写敏感；空串合法且返回 0 |
| `COLOR_FROMNAME` | int | str | 恰好 1 | — | 按颜色名求 0xRRGGBB 颜色值；颜色名无效时返回 -1；颜色名 "transparent"（不分大小写）抛 CodeEE 而非返回 -1；空串等未知名的返回 -1；实参须为字符串且不可省略 |
| `COLOR_FROMRGB` | int | int, int, int | 恰好 3 | — | 把 0-255 的 R、G、B 合成 0xRRGGBB 数值返回；三个实参任一 <0 或 >255 抛 CodeEE(ArgIsOutOfRange)，消息含第几号实参；三槽均为 int 且不可省略 |
| `CURRENTALIGN` | str | （无实参） | 恰好 0 | — | 返回当前 ALIGNMENT 状态的字串：LEFT / CENTER / RIGHT；无实参；文档示例/API 行把名字误写成 CURRENTALIGHN；作指令时结果写入 RESULTS |
| `CURRENTREDRAW` | int | （无实参） | 恰好 0 | — | 返回当前 REDRAW 状态：0=仅在需要输入时绘制，否则 1；无实参；REDRAW 2/3 的第 2 位只触发即时重绘，故仍返回 1；作指令时写入 RESULT:0 |
| `GETBGCOLOR` | int | （无实参） | 恰好 0 | — | 返回当前使用的背景颜色，形如 0xRRGGBB；取 Console.bgColor（SETBGCOLOR 设置，初值 Config.BackColor）并屏蔽 alpha 位 |
| `GETCOLOR` | int | （无实参） | 恰好 0 | — | 返回当前使用的文字颜色，形如 0xRRGGBB；取 SETCOLOR 后的用户样式色并屏蔽 alpha；多写实参报 ArgsNotNeeded，不存在可留空槽 |
| `GETDEFBGCOLOR` | int | （无实参） | 恰好 0 | — | 返回 emuera.config 指定的默认背景颜色，形如 0xRRGGBB；取 Config.BackColor；CanRestructure 为真，可被常量折叠而在解析期定型 |
| `GETDEFCOLOR` | int | （无实参） | 恰好 0 | — | 返回 emuera.config 指定的文字颜色，形如 0xRRGGBB；取 Config.ForeColor；CanRestructure 为真，可被常量折叠而在解析期定型 |
| `GETDISPLAYLINE` | str | int | 恰好 1 | — | 取当前日志缓冲中第 n 个显示行的纯文本；索引越界返回空串；索引越界(<0 或 ≥行数)返回空串；索引指向当前显示缓冲，受 MaxLog 修剪影响；自动换行时行数多于 LINECOUNT，按它遍历会漏行 |
| `GETFOCUSCOLOR` | int | （无实参） | 恰好 0 | — | 返回配置中按钮/高亮选中时的文字颜色，形如 0xRRGGBB；取 Config.FocusColor；CanRestructure 为真；多写实参报 ArgsNotNeeded |
| `GETFONT` | str | （无实参） | 恰好 0 | — | 返回当前使用的字体名，未 SETFONT 时为配置标准字体名；读 userStyle.Fontname；SETFONT 省略实参或传空串即回落 Config.FontName；不可折叠 |
| `GETSTYLE` | int | （无实参） | 恰好 0 | — | 返回当前字体样式的位掩码：1 粗体、2 斜体、4 删除线、8 下划线；读 StringStyle.FontStyle；FONTSTYLE 只映射 1/2/4/8 位，其它位丢弃；默认 Regular 返回 0 |
| `ISSKIP` | int | （无实参） | 恰好 0 | —（恰 0 实参，无空槽；多写实参报 ArgsNotNeeded） | 返回当前是否处于 SKIPDISP 忽略输出状态（1/0），无实参；读 Process.SkipPrint；文档称 SKIPDISP 恒把 RESULT:0 置 0，代码实为置成标志值(1/0) |
| `LINEISEMPTY` | int | （无实参） | 恰好 0 | —（恰 0 实参，无空槽；多写实参报 ArgsNotNeeded） | 返回当前行缓冲区是否为空（1=空行，0=已有输出）；读 Console.EmptyLine(=printBuffer.IsEmpty)；PRINTC 已排入按钮时算非空，返回 0 |
| `MESSKIP` | int | （无实参） | 恰好 0 | —（恰 0 实参，自检；任何实参报 TooManyFuncArgs） | 手工裁定（无规则表，类自行检查）：返回是否处于消息跳过（WAIT/宏跳过）状态（1/0） |
| `MONEYSTR` | str | int, str?- | 1–2 | 第 2 实参省略或留空：不做格式转换，直接拼接数字与单位 | 把数值按货币单位（可选格式转换）拼成字符串返回；单位与位置取 Config.MoneyLabel/MoneyFirst；格式串非法抛 CodeEE(InvalidFormat)；第 1 实参必填 |
| `MOUSESKIP` | int | （无实参） | 恰好 0 | —（恰 0 实参，自检；任何实参报 TooManyFuncArgs） | 手工裁定（无规则表，类自行检查）：与 MESSKIP 行为相同的旧名，返回消息跳过状态（1/0） |
| `PRINTCLENGTH` | int | （无实参） | 恰好 0 | — | 返回配置 PRINTC的字符数（Config.PrintCLength）；无实参；配置默认 25（文档未给）；CanRestructure=true，表达式中会折叠为常量 |
| `PRINTCPERLINE` | int | （无实参） | 恰好 0 | — | 返回配置 PRINTC的排列数量（Config.PrintCPerLine），默认 3；无实参；指令形式可带目标变量（PRINTCPERLINE X），省略则写 RESULT:0，与函数形式不同 |

### 字符串操作・参照

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `CHARATU` | str | str, int | 恰好 2 | —（少写或留空均报错） | 取出字符串中指定位置的字符并返回；位置越界时返回空串；第 2 实参必需（文档写 [获取字符位置] 易误读为可省）；按 UTF-16 代码单元索引，代理对会拆成半个字符 |
| `ENCODETOUNI` | int | str, int?- | 1–2 | 槽2 省略或显式留空都取 0（位置 0） | 返回第 1 参数字符串在指定位置的 Unicode 码点（可跨代理对）；空串返回 -1；空串抢先返回 -1，位置检查被跳过；位置为负或 ≥ 串长抛 CodeEE；命令形态是另一实现（写 RESULT 数组） |
| `ESCAPE` | str | str | 恰好 1 | —（少写或留空均报错） | 用 Regex.Escape 转义正则元字符后返回，常配 REPLACE / REGEXPMATCH 使用；转义规则即 .NET Regex.Escape（空白字符也加反斜杠），不是 HTML/CSV 转义，勿与 HTML_ESCAPE 混用 |
| `ISNUMERIC` | int | str | 恰好 1 | —（少写或留空均报错） | 判断参数能否被 TOINT 数值化：可以返回 1，否则返回 0；仅 ASCII 会继续判断，出现全角即 0；0x/0b/指数形式算数值，但 0b2、1e999 会抛 CodeEE 而非返回 0 |
| `REGEXPMATCH`（候选 1） | int | str, str, int? | 2–3 | 第3实参可整省（=0，不写 RESULT:1/RESULTS）；显式留空报错 | 用正则 pattern 匹配 str 并返回匹配个数；可回填分组数与各匹配的分组内容；4实参须为 ref int/ref str[] 且恰好4个；组数含组0；命名组时 GetGroupNames 输出会多写；数组过小静默截断；无匹配返回0 |
| `REGEXPMATCH`（候选 2） | int | str, str, ref int, ref str[] | 恰好 4 | 第3实参可整省（=0，不写 RESULT:1/RESULTS）；显式留空报错 | （同上） |
| `REPLACE`（候选 1） | str | str, str, str, int? | 3–4 | —（4 个槽都拒绝留空）；槽4 整体省略时取 0 | 把目标串中匹配第 2 参数正则（或字面串）的部分替换为第 3 参数后返回；槽4：0 或其它=正则替换，1=按字符串数组逐次替换（槽3 须为非 const 字符串一维数组，多出的匹配替换为空串），2=纯字面替换；正则非法抛 CodeEE |
| `REPLACE`（候选 2） | str | str, str, ref* str[], int | 恰好 4 | —（4 个槽都拒绝留空）；槽4 整体省略时取 0 | （同上） |
| `STRCOUNT` | int | str, str | 恰好 2 | —（少写或留空均报错） | 把第二实参当正则，统计第一实参中的匹配次数并返回；正则非法时运行期抛 CodeEE(InvalidRegexArg)；计非重叠匹配、区分大小写；编译后的正则常驻静态缓存 |
| `STRFIND` | int | str, str, int?- | 2–3 | start 省略/留空取 0 | 在目标串中从 start 起查找子串，按编码字节数返回其 0 起索引，未找到返回 -1；字节基准随 useLanguage（默认 SHIFT-JIS）；start<0 变 0、≥目标长度直接 -1；查找串为空返回 start（目标为空则 -1） |
| `STRFINDU` | int | str, str, int?- | 2–3 | start 省略/留空取 0 | 同 STRFIND，但 start 与返回索引按 UTF-16 代码单元（字符）计；start<0 直接返回 -1（与 STRFIND 变 0 不同）；start≥目标长度也 -1；代理对算 2 个 |
| `STRFORM` | str | str | 恰好 1 | —（无可省略槽；省略→个数错，留空→ArgCanNotBeNull） | 把参数串按 PRINTFORM 的格式化规则展开，返回展开后的字符串；实参须为 string；展开失败抛 CodeEE(InvalidFormString)；常量实参解析期折叠；指令形式结果入 RESULTS |
| `STRJOIN` | str | ref* any[…], str?-, int?-, int?- | 1–4 | 槽2=“,”；槽3=0；槽4=余长（一直连到数组末尾） | 把数组（任意元素类型、任意维数）的元素用分隔符连接成一个字符串返回；槽4 显式留空等同省略；省略算出的余长为负时也抛 CodeEE 且报“第4引数”；字符变量的字符串数组运行时报错 |
| `STRLENS` | int | str | 恰好 1 | —（少写或留空均报错） | 按当前语言编码的字节数计算字符串长度（全角计 2）并返回；字节数取 Config.Language 的编码，默认日语即 SHIFT-JIS；纯 ASCII 时直接返回字符数 |
| `STRLENSU` | int | str | 恰好 1 | —（少写或留空均报错） | 返回字符串的 UTF-16 码元个数（str.Length），全角字符计 1；按 .NET UTF-16 码元计数，代理对（如 emoji）算 2 而非 1 |
| `SUBSTRING` | str | str, int?-, int?- | 1–3 | start 省略/留空取 0；length 省略/留空取 -1（取到末尾） | 按当前编码的字节数从 start 起截取 length 字节，返回截得的子串；字节基准随 useLanguage（默认 932），非固定 SHIFT-JIS；start<0 视为 0；切口向后取整可能多出 1 个字符 |
| `SUBSTRINGU` | str | str, int?-, int?- | 1–3 | start 省略/留空取 0；length 省略/留空取 -1（取到末尾） | 按 UTF-16 代码单元从 start 起取 length 个字符，返回截得的子串；start<0 视为 0；start≥长度或 length=0 返回空串；代理对算 2 个，可能切在代理对中间 |
| `TOFULL` | str | str | 恰好 1 | —（少写或留空均报错） | 把半角字符转成全角（英数、片假名、空格等），返回转换后的字符串；半角空格会变成全角空格（见文档示例）；空串返回空串；走 VB StrConv(Wide) |
| `TOHALF` | str | str | 恰好 1 | —（少写或留空均报错） | 把全角字符转成半角（片假名转半角片假名等），返回转换后的字符串；无对应半角的字符原样保留；片假名会拆成半角+浊点半角字符（如 ﾊﾟ） |
| `TOINT` | int | str | 恰好 1 | —（少写或留空均报错） | 把参数字符串转成整数返回；无法解释为数值时返回 0；识别 0x/0b/指数与前导 ±；小数只取整数部分（1.9→1）；超 int64 或 0b2 会抛 CodeEE 而非返回 0 |
| `TOLOWER` | str | str | 恰好 1 | —（少写或留空均报错） | 把字符串中的字母转成小写，返回转换后的字符串；用当前区域性 ToLower（未指定 InvariantCulture）；空串返回空串 |
| `TOSTR` | str | int, str?- | 1–2 | 槽2 省略或留空 = i.ToString()，即无格式的十进制 | 按可选格式串把整数转成字符串返回，如 "X8"、"#,###"、"0000万0000"；格式串直接交给 Int64.ToString；格式非法抛 CodeEE(InvalidFormat)；格式为空串按通用格式输出 |
| `TOUPPER` | str | str | 恰好 1 | —（少写或留空均报错） | 把字符串中的字母转成大写，返回转换后的字符串；用当前区域性 ToUpper（未指定 InvariantCulture）；空串返回空串 |
| `UNICODE` | str | int | 恰好 1 | —（少写或留空均报错） | 返回码值对应的单个 UTF-16 字符；不处理代理对，码值上限 0xFFFF；码值 <0 或 >0xFFFF 抛 CodeEE；除 0x0A/0x0D 外的 <0x20 及 0x7F-0x9F 只警告并返回空串；代理区值原样返回 |

### 算术

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `ABS` | int | int | 恰好 1 | —（恰 1 实参，少写或留空都报错） | 返回实参的绝对值，结果为 64 位整数；long.MinValue 取绝对值无对应正值，抛 CodeEE(MinInt64CanNotApplyABS)；指令形式 ABS X 把结果写入 RESULT |
| `ALLSAMES` | int | any, 随槽0…, （尾部：自第 2 槽起按末位规则重复） | >= 2 | —（显式留空报 CodeEE(ArgCanNotBeNull)） | 所有实参的值全部相等时返回 1，否则返回 0；最少 2 个实参；三者共用 SameAsFirst 规则，第 1 实参决定尾部实参的期望类型；字符串按序数比较 |
| `CBRT` | int | int | 恰好 1 | —（恰 1 实参，少写或留空都报错） | 返回实参的立方根，结果截断为 64 位整数；浮点截断：(long)Math.Pow(ret,1/3) 使 CBRT(64)=3、CBRT(125)=4，完全立方数常少 1；负实参抛 CodeEE |
| `CMATCH` | int | ref* any[] (角色变量), 随槽0, int?-, int?- | 2–4 | 留空与少写尾实参同义：start=0、end=CHARANUM | 统计角色范围 [start,end) 内、各角色同一数组元素等于 value 的个数；start>=CHARANUM 或 end>CHARANUM 抛 CodeEE；首实参须是 1 维角色数组变量（CFLAG:2、CSTR:1） |
| `CONVERT` | str | int, int | 恰好 2 | —（显式留空报 CodeEE(ArgCanNotBeNull)） | 把第 1 实参按第 2 实参指定的进制转成字符串返回；负数在 2/8/16 进制下按二进制补码输出、没有负号（16 进制为小写）；进制非 2/8/10/16 在求值期抛 CodeEE |
| `GETBIT` | int | int, int | 恰好 2 | —（恰 2 实参，少写或留空都报错） | 取参1的第参2位，返回 0 或 1；0～63 的范围检查在取值时进行（CheckArgumentType 里的常量检查已注释掉）；负数按 64 位算术右移 |
| `GROUPMATCH` | int | any, 随槽0…, （尾部：自第 2 槽起按末位规则重复） | >= 2 | —（显式留空报 CodeEE(ArgCanNotBeNull)） | 返回第 1 实参的值在第 2 个起的实参中出现的次数（不含自身）；尾部实参类型必须与第 1 实参一致，混用 int/str 在检查期即报错；重复值会被重复计数 |
| `INRANGE` | int | int, int, int | 恰好 3 | —（恰 3 实参，少写或留空都报错） | 判断参1是否落在 [参2, 参3] 闭区间内，返回 1 或 0；含两端点；参2 大于参3 时恒返回 0，不报错 |
| `INRANGEARRAY` | int | ref* int[], int, int, int?-, int?- | 3–5 | 留空与少写尾实参同义：start=0、end=数组长度 | 统计数组 [start,end) 内满足 min<=v<max 的元素个数；区间左闭右开，与 INRANGE 的双闭不同；start=end=数组长度合法且返回 0 |
| `INRANGECARRAY` | int | ref* int[] (角色变量), int, int, int?-, int?- | 3–5 | 留空与少写尾实参同义：start=0、end=CHARANUM | 统计角色范围 [start,end) 内各角色数组元素满足 min<=v<max 的个数；start>=CHARANUM 或 end>CHARANUM 抛 CodeEE；区间左闭右开，与 INRANGE 不同 |
| `LIMIT` | int | int, int, int | 恰好 3 | —（恰 3 实参，少写或留空都报错） | 把参1夹到 [参2, 参3] 区间内并返回（钳位）；先判 value<min 再判 value>max；参2 大于参3 时返回参2（LIMIT(5,10,0)=10），不报错 |
| `LOG` | int | int | 恰好 1 | —（恰 1 实参，少写或留空都报错） | 返回实参的自然对数，结果截断为 64 位整数；文档未写实参限制；实参 <=0 抛 CodeEE，结果截断（LOG(2)=0、LOG(100)=4） |
| `LOG10` | int | int | 恰好 1 | —（恰 1 实参，少写或留空都报错） | 返回实参的常用（10 为底）对数，结果截断为 64 位整数；与 LOG 同一个类，仅底数换成 10；实参 <=0 抛 CodeEE，LOG10(100)=2 |
| `MATCH` | int | ref* any[], 随槽0, int?-, int?- | 2–4 | 留空与少写尾实参同义：start=0、end=数组长度 | 统计数组 [start,end) 内等于 value 的元素个数，数值与字符串数组通用；value 类型须与数组一致；传 CFLAG:2 等角色数组时范围参数是第二下标，只统计 TARGET 角色 |
| `MAX` | int | int, int…?, （尾部：自第 2 槽起按末位规则重复） | >= 1 | —（尾实参可少写，少写的槽不参与比较；显式空槽报错） | 返回全部实参（至少 1 个）中的最大值；1 个实参时返回自身；只有尾实参可按数量省略，显式留空一律 CodeEE(ArgCanNotBeNull) |
| `MAXARRAY` | int | ref* int[], int?-, int?- | 1–3 | 留空与少写尾实参同义：start=0、end=数组长度 | 返回数组 [start,end) 内的最大元素值；start==end==数组长度时仍先读 X:start，会抛越界异常；start>end 则返回 X:start |
| `MAXCARRAY` | int | ref* int[] (角色变量), int?-, int?- | 1–3 | 留空与少写尾实参同义：start=0、end=CHARANUM | 返回角色范围 [start,end) 内各角色同一数组元素的最大值；start>=CHARANUM 或 end>CHARANUM 抛 CodeEE；start=end 时返回起始角色的元素 |
| `MIN` | int | int, int…?, （尾部：自第 2 槽起按末位规则重复） | >= 1 | —（尾实参可少写，少写的槽不参与比较；显式空槽报错） | 返回全部实参（至少 1 个）中的最小值；1 个实参时返回自身；与 MAX 同一个类，只换比较方向；显式留空一律 CodeEE(ArgCanNotBeNull) |
| `MINARRAY` | int | ref* int[], int?-, int?- | 1–3 | 留空与少写尾实参同义：start=0、end=数组长度 | 返回数组 [start,end) 内的最小元素值；start==end==数组长度时仍先读 X:start，会抛越界异常；start>end 则返回 X:start |
| `MINCARRAY` | int | ref* int[] (角色变量), int?-, int?- | 1–3 | 留空与少写尾实参同义：start=0、end=CHARANUM | 返回角色范围 [start,end) 内各角色同一数组元素的最小值；start>=CHARANUM 或 end>CHARANUM 抛 CodeEE；start=end 时返回起始角色的元素 |
| `NOSAMES` | int | any, 随槽0…, （尾部：自第 2 槽起按末位规则重复） | >= 2 | —（显式留空报 CodeEE(ArgCanNotBeNull)） | 所有实参的值两两不同（无重复）时返回 1，否则返回 0；重复判定用 Distinct 序数比较（区分大小写）；仅 2 个实参也合法；类型混用在检查期报错 |
| `POWER` | int | int, int | 恰好 2 | —（函数形式恰 2、命令形式恰 3 实参，留空或数量不符报错） | 计算 x 的 y 次方，截断为 64 位整数后返回（或赋给变量）；结果为 (long)Math.Pow 截断；NaN、无穷、超出 long 范围抛 CodeEE；命令形式首实参须为可赋值变量 |
| `SIGN` | int | int | 恰好 1 | —（恰 1 实参，少写或留空都报错） | 返回实参的符号：负数 -1、零 0、正数 1；与 ABS 同页；无异常条件；指令形式 SIGN X 把结果写入 RESULT |
| `SQRT` | int | int | 恰好 1 | —（恰 1 实参，少写或留空都报错） | 返回实参的平方根，小数部分直接截断；负实参抛 CodeEE(ArgIsNegative)；Math.Sqrt 后截断，先放大可减少精度损失 |
| `SUMARRAY` | int | ref int[…], int?-, int?- | 1–3 | 槽2=0；槽3=末维长度（省略与留空同义，均合计到末尾） | 返回数值数组末维 [index1, index2) 区间元素之和；文档称不可指定多维/角色数组，代码用 RefIntArray 接受任意维；索引越界报 CodeEE，index1>index2 得 0 |
| `SUMCARRAY` | int | ref* int[…] (角色变量), int?-, int?- | 1–3 | 留空与少写尾实参同义：start=0、end=CHARANUM | 返回角色范围 [start,end) 内各角色同一数组元素的合计值；文档称 B<CHARANUM，代码允许 end=CHARANUM；2D 角色数组（CDFLAG）解析放行但运行期越界 |

### 角色操作・参照

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `EXISTCSV` | int | int, int?- | 1–2 | 槽 2（SP 标志）省略或留空均按 0（非 SP）处理 | 查 chara*.csv 中是否存在 NO 等于参数的已定义角色，存在返回 1，否则返回 0；文档未提第 2 参；传非 0 抛 CodeEE（CompatiSPChara 关闭），开启后该标志亦被忽略 |
| `FINDCHARA` | int | ref any (角色变量), 随槽0, int?-, int?- | 2–4 | 槽 3 省略/留空取 0（起点），槽 4 取 CHARANUM（终点） | 按角色变量查找值等于第 2 参（须同型）的角色，返回首个匹配的列表编号，无匹配返回 -1；起点=CHARANUM 或 CHARANUM=0 时抛 CodeEE；返回列表下标而非 CSV 的 NO；起点≥终点返回 -1 |
| `FINDLASTCHARA` | int | ref any (角色变量), 随槽0, int?-, int?- | 2–4 | 槽 3 省略/留空取 0（起点），槽 4 取 CHARANUM（终点） | 同 FINDCHARA，但自后向前查找，返回最后一个值匹配的角色列表编号，无匹配返回 -1；起点=CHARANUM 或 CHARANUM=0 时抛 CodeEE；返回列表下标而非 CSV 的 NO；起点≥终点返回 -1 |
| `GETCHARA` | int | int, int?- | 1–2 | 第2实参可整省或显式留空（均按 0/false 处理）；第1实参不可空 | 在持有角色列表中查找 NO 等于第1实参的角色，返回其下标，找不到返回 -1；第2实参仅在 CompatiSPChara 兼容模式下生效（先查非SP再查SP），默认配置下根本不读；返回下标从0起 |

### 变量操作・变量参照・CSV 参照

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `ARRAYMSORT` | int | ref any[], ref any[…]…?, （尾部：自第 2 槽起按末位规则重复） | >= 1 | 无槽可留空（空槽报 CodeEE）；尾部数组可整段省略，只写首参则仅排它自己 | 以首个一维数组的升序重排其后各数组；全部成功返回 1，任一数组长度不足返回 0；文档称仅支持命令，本仓已注册为函数，表达式内也可调用；中途失败时前面的数组已被重排（非原子） |
| `ARRAYMSORTEX`（候选 1） | int | str, ref str[], int?-, int? | 2–4 | 第3参留空=顺序（同省略）；第4参留空报错，省略=扫到0/空串为止 | 按索引数组的排序结果重排列出的其它数组；成功返回1，失败返回0；省略长度时键数组先遇0（整型）或空串（字符串）即返回0并放弃重排；长度实参为0也返回0，文档只说返回1 |
| `ARRAYMSORTEX`（候选 2） | int | ref int[], ref str[], int?-, int? | 2–4 | 第3参留空=顺序（同省略）；第4参留空报错，省略=扫到0/空串为止 | （同上） |
| `CSVABL` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 ABL[index]；返回 int，模板未定义该角色时抛 CodeEE；索引须小于 CSV 声明的 ABL 数组长度，越界抛 CodeEE；范围内未定义的值返回 0 |
| `CSVBASE` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 BASE[index]；返回 int，模板未定义该角色时抛 CodeEE；索引上限取 BASE 与 MAXBASE 声明长度的较大者，越界抛 CodeEE；未定义值返回 0 |
| `CSVCALLNAME` | str | int, int?- | 1–2 | 第 2 参留空或省略均取 0（表示非 SP 角色） | 取 CSV 模板的 CALLNAME；返回字符串，模板未定义该角色时抛 CodeEE；文档未载第 2 参（SP 标志）；未开兼容选项「使用SP角色」时传非 0 抛 CodeEE |
| `CSVCFLAG` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 CFLAG[index]；返回 int，模板未定义该角色时抛 CodeEE；索引须小于 CSV 声明的 CFLAG 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `CSVCSTR` | str | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 CSTR[index] 字符串；越界或角色未定义时抛 CodeEE；第 2 参是必需的索引而非 SP 标志；越界抛 OoRCstr；范围内的缺失元素返回空串 |
| `CSVEQUIP` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 EQUIP[index]；返回 int，模板未定义该角色时抛 CodeEE；索引须小于 CSV 声明的 EQUIP 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `CSVEXP` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 EXP[index]（经验初值）；返回 int，角色未定义时抛 CodeEE；索引须小于 CSV 声明的 EXP 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `CSVJUEL` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 JUEL[index]（珠初值）；返回 int，角色未定义时抛 CodeEE；索引须小于 CSV 声明的 JUEL 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `CSVMARK` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 MARK[index]（刻印初值）；返回 int，角色未定义时抛 CodeEE；索引须小于 CSV 声明的 MARK 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `CSVMASTERNAME` | str | int, int?- | 1–2 | 第 2 参留空或省略均取 0（表示非 SP 角色） | 取 CSV 模板的 MASTERNAME；返回字符串，模板未定义该角色时抛 CodeEE；文档未载第 2 参（SP 标志）；未开兼容选项「使用SP角色」时传非 0 抛 CodeEE |
| `CSVNAME` | str | int, int?- | 1–2 | 第 2 参留空或省略均取 0（表示非 SP 角色） | 取 CSV 模板的 NAME；返回字符串，模板未定义该角色时抛 CodeEE；文档只写 1 个 int 参数，实际第 2 参（SP 标志）可选；SP 标志现已不参与查表 |
| `CSVNICKNAME` | str | int, int?- | 1–2 | 第 2 参留空或省略均取 0（表示非 SP 角色） | 取 CSV 模板的 NICKNAME；返回字符串，模板未定义该角色时抛 CodeEE；文档未载第 2 参（SP 标志）；未开兼容选项「使用SP角色」时传非 0 抛 CodeEE |
| `CSVRELATION` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 RELATION[index]；返回 int，模板未定义该角色时抛 CodeEE；索引须小于 CSV 声明的 RELATION 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `CSVTALENT` | int | int, int, int?- | 2–3 | 第 3 参留空或省略均取 0（表示非 SP 角色） | 读 CSV 模板的 TALENT[index]（才能初值）；返回 int，角色未定义时抛 CodeEE；索引须小于 CSV 声明的 TALENT 数组长度，越界抛 CodeEE；未定义值返回 0 |
| `ENUMFUNCBEGINSWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举非事件函数名以 keyword 开头者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[] 作输出（不可为常量）；匹配不区分大小写，结果未排序，超容量被截断 |
| `ENUMFUNCENDSWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举非事件函数名以 keyword 结尾者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[] 作输出（不可为常量）；匹配不区分大小写，结果未排序，超容量被截断 |
| `ENUMFUNCWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举非事件函数名中包含 keyword 者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[] 作输出（不可为常量）；匹配不区分大小写，结果未排序，超容量被截断 |
| `ENUMMACROBEGINSWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举 #DEFINE 宏名以 keyword 开头者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[] 作输出（不可为常量）；匹配不区分大小写，结果未排序，超容量被截断 |
| `ENUMMACROENDSWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举 #DEFINE 宏名以 keyword 结尾者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[] 作输出（不可为常量）；匹配不区分大小写，结果未排序，超容量被截断 |
| `ENUMMACROWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举 #DEFINE 宏名中包含 keyword 者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[] 作输出（不可为常量）；匹配不区分大小写，结果未排序，超容量被截断 |
| `ENUMVARBEGINSWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举全局变量/常量名以 keyword 开头者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[]；不含局部变量（与 EXISTVAR 不同），匹配不区分大小写，结果未排序 |
| `ENUMVARENDSWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举全局变量/常量名以 keyword 结尾者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[]；不含局部变量（与 EXISTVAR 不同），匹配不区分大小写，结果未排序 |
| `ENUMVARWITH` | int | str, ref str[]? | 1–2 | 第 2 参省略时写入 RESULTS（默认 100 格）；留空报错 | 枚举全局变量/常量名中包含 keyword 者，写入 RESULTS，返回写入条数；文档只写 1 参，实际第 2 参可传 str[]；不含局部变量（与 EXISTVAR 不同），匹配不区分大小写，结果未排序 |
| `ERDNAME` | str | ref* any, int, int? | 2–3 | 第3参省略=只用变量名（不带@维度）；显式留空报错 | 返回 ERD 变量指定下标对应的关键字名；无对应项时返回空串；维度从1起，1维变量须省略该参；值为负或该变量无对应 ERD 项时返回空串且不报错 |
| `EXISTVAR` | int | str | 恰好 1 | 无槽可省略或留空：空槽与少写尾参都会报 CodeEE | 按名字查变量/常量并返回位掩码（1整数 2字符串 4常量 8二维 16三维），不存在返回 0；可查到当前函数作用域的私有变量（ENUMVAR 系列查不到）；名字比较随 IgnoreCase 配置 |
| `FINDELEMENT` | int | ref* any[], 随槽0, int?-, int?-, int?- | 2–5 | 槽3→0；槽4→数组长度；槽5→0（部分匹配） | 在一维数组 [start,end) 内找等于 value 的最小下标；无匹配返回 -1；字符串 value 按正则解释，非法正则抛 CodeEE；start/end 越界抛 CodeEE；start≥end 返回 -1 |
| `FINDLASTELEMENT` | int | ref* any[], 随槽0, int?-, int?-, int?- | 2–5 | 槽3→0；槽4→数组长度；槽5→0（部分匹配） | 在一维数组 [start,end) 内找等于 value 的最大下标；无匹配返回 -1；字符串 value 按正则解释，非法正则抛 CodeEE；start/end 越界抛 CodeEE；start≥end 返回 -1 |
| `GETEXPLV` | int | int, int | 恰好 2 | — | 按 EXPLV 的边界把 value 分级，返回其等级（0 起，上限 maxLV）；仅比较 EXPLV[1]..EXPLV[maxLV]；maxLV≤0 直接返回 maxLV；maxLV 超出数组长度抛越界异常 |
| `GETNUM` | int | ref* any, str, int? | 2–3 | 省略第 3 参→用变量自身名字；留空→CodeEE（不能为空） | 按名称在关键字表中查整数，指令形式写入 RESULT:0；查不到返回 -1；第 2 参须为字符串；第 3 参拼成「变量名@N」选择 ERD 名称表，文档未载此参数 |
| `GETPALAMLV` | int | int, int | 恰好 2 | — | 按 PALAMLV 的边界把 value 分级，返回其等级（0 起，上限 maxLV）；仅比较 PALAMLV[1]..PALAMLV[maxLV]；maxLV≤0 直接返回 maxLV；maxLV 超出数组长度抛越界异常 |
| `GETVAR` | int | str | 恰好 1 | — | 运行时解析变量名，返回该整型变量/常量的值；非变量或非整型抛 CodeEE；整型常量也能读；名字里可带数组下标如 A:1；无法解析的标识符在解析阶段即抛 CodeEE |
| `GETVARS` | str | str | 恰好 1 | — | 运行时解析变量名，返回该字符串变量/常量的值；非变量或非字符串抛 CodeEE；字符串常量也能读；固定 1 参，显式留空或多写少写实参都报 CodeEE |
| `ISDEFINED` | int | str | 恰好 1 | 无槽可省略或留空：空槽与少写尾参都会报 CodeEE | 判断文本宏名是否已用 #DEFINE 定义：已定义返回 1，否则返回 0；只查 #DEFINE 宏字典，不认变量与函数；名字比较的大小写敏感度随 IgnoreCase 配置 |
| `SETVAR` | int | str, any | 恰好 2 | — | 运行时解析变量名，把第 2 个实参赋给该变量，恒返回 1；常量不可写；类型不符抛 CodeEE，但消息把变量名说成类型不符（值类型才是问题）；恒返回1 |
| `VARSETEX` | int | str, any, int?, int?, int? | 2–5 | 省略第3参=整维、第4参=0、第5参=数组末尾；显式留空均报错 | 用变量名字符串把数组的一段（或全部维度）赋为同一个值，恒返回 1；越界抛 IndexOutOfRangeException，from/to 不校验不互换；3维省略 to 时 end=0；字符串数组忽略 setAllDim |
| `VARSIZE` | int | str, int?- | 1–2 | 省略或留空均取 0，即最左维（EM+EE 选项下 1 亦为最左维） | 按名称取变量的数组长度（指定某一维）；名字不是变量或该维不存在时抛 CodeEE；维越界抛 CodeEE；负维对可变长引用变量会走 array.GetLength(负) 越界；命令形式不受 ERD 选项影响 |

### 存档数据操作

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `CHKCHARADATA` | int | str | 恰好 1 | —（实参恰好 1 个，无槽可省略或留空） | 按名字检查 dat/chara_*.dat，返回状态码（0=可加载），备注或失败原因写入 RESULTS:0；0=可加载,1=不存在,2=别的游戏,3=版本不符,4=其它；空名/越界名按不存在返 1；RESULTS:0 记备注或原因；不写 RESULT:1 |
| `CHKDATA` | int | int | 恰好 1 | — | 检查存档可否加载：返回状态码 0–4，注释写 RESULTS:0、版本写 RESULT:1；文档称 RESULT:0 还会得到存档时间戳，代码无此路径；实参为负或超 int 上限抛 CodeEE |
| `FIND_CHARADATA` | int | str?- | 0–1 | 槽1省略或留空均按 *（搜全部 chara_*.dat） | 在 dat 目录中按通配名搜索 chara_*.dat，把 * 部分自 RESULTS:0 起写入并返回命中数；命中名写入 RESULTS:0 起，超出元素数只截断不报错；模式含分隔符/.. 或空串时直接 0 命中；顺序未排序 |
| `LOADTEXT` | str | any, int?, int? | 1–3 | —（无槽可留空；显式留空一律报 CodeEE） | 读取文本文件内容并返回，文件不存在、读失败或扩展名非法时返回空串；返回值中 CR 被删除；实际文件名是 sav 下 txtXX.txt（文档写 textXX）；第2/3参无效，读时按 DetectEncoding 自动判编码；fileNo<0 得空串 |
| `OUTPUTLOG` | int | str?, int? | 0–2 | 槽1省略→emuera.log；槽2省略→0(false)；留空抛 CodeEE | 把当前显示内容写入日志文件（UTF-8 BOM），恒定返回 1（不反映写出失败）；页面只列 1 个参数且称仅支持指令；代码实为式中函数，另有未文档化的第 2 参 hideInfo(1=隐藏环境信息)，且路径非法/写出失败时仍返回 1 |
| `SAVENOS` | int | （无实参） | 恰好 0 | — | 返回配置的「显示存档数据数量」（默认 20）；作命令时赋给数值变量；配置值被夹在 20–80；函数形式不接受任何实参，多写实参报「不需要实参」 |
| `SAVETEXT` | int | str, any, int?, int? | 2–4 | —（无槽可留空；显式留空一律报 CodeEE） | 把字符串原样写入文本文件，成功返回 1，fileNo 越界、路径或扩展名非法、IO 异常时返回 0；实际文件名 txtXX.txt（文档写 textXX）；扩非白名单则改写为 .txt 仍写出；第4参 forceUTF8 未用，写死 UTF-8 BOM |

### 日期・时刻获取

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `GETMILLISECOND` | int | （无实参） | 恰好 0 | —（无实参，无槽可省略或留空） | 返回自公元0001-01-01起的毫秒数（DateTime.Now.Ticks/10000）；无实参，写实参即报 TooManyFuncArgs；精度受系统时钟限制（约15ms），短间隔内连续调用可能返回同值 |
| `GETSECOND` | int | （无实参） | 恰好 0 | —（无实参，无槽可省略或留空） | 返回自公元0001-01-01起的整秒数（DateTime.Now.Ticks/10^7）；文档 API 行把函数名误写成 int GETTIME；无实参，写实参即报错；精度同样受时钟限制 |
| `GETTIME` | int | （无实参） | 恰好 0 | —（无实参，无槽可省略或留空） | 返回当前日期时间拼成的17位整数，形如 yyyyMMddHHmmssfff；文档称同时写RESULT:0与RESULTS:0，代码只写RESULT:0（字符串名GETTIMES）；各字段分次取Now，跨秒边界可能不自洽 |
| `GETTIMES` | str | （无实参） | 恰好 0 | —（无实参，无槽可省略或留空） | 返回当前日期时间字符串，格式 yyyy/MM/dd HH:mm:ss；作为指令使用时写入RESULTS:0；文档未单列此名，是在 GETTIME 页给出的表达式形式 |

### 调试辅助・系统流程控制

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `FLOWINPUT` | int | int, int?, int?, int? | 1–4 | 槽1必填；槽2~4可省略→保持上次值；任一槽留空抛CodeEE | 设定系统流程INPUT的默认值与左键/跳过行为（每参非0即启用），固定返回0；文档标void，代码返回int 0并写RESULT:0；设定存ProcessState且无复位手段；第2实参=0时默认值不生效；槽4非0不等待输入易死循环 |

### 函数系（CALL 等）

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `EXISTFUNCTION` | int | str, int? | 1–2 | 第2实参省略时按0处理（同一直查分支）；显式留空 f(a,,) 编译期报错 | 查名字对应的非事件标签：普通标签1、#FUNCTION 2、#FUNCTIONS 3，查不到返0；第2实参非0时改走大小写不敏感（CurrentCultureIgnoreCase）全表线性查找，文档未记载；@EVENT* 事件标签默认不算存在 |
| `EXISTMETH` | int | str | 恰好 1 | — | 查用户定义的式中函数：整数型 #FUNCTION 返回1、字符串型 #FUNCTIONS 返回2，否则返0；页面提示'命令与式中函数均支持'与实现不符：只查用户定义 #FUNCTION(S)，普通标签返0；且以零实参构造调用项，REF 形参者返0 |
| `GETMETH` | int | str, int?-, any…?-, （尾部：自第 3 槽起按末位规则重复） | >= 1 | 省略/留空槽2=无兜底（未找到即抛CodeEE）；槽3起留空原样转发给目标 | 按名字调用用户定义的整数型式中函数并返回其返回值；目标不存在时返回第二参数；只查用户定义#FUNCTION(S)（REF 亦可）；非#FUNCTION标签抛CodeEE，内建函数不可调，页面提示'命令/式中函数均可'与实现不符 |
| `GETMETHS` | str | str, str?-, any…?-, （尾部：自第 3 槽起按末位规则重复） | >= 1 | 省略/留空槽2=无兜底（未找到即抛CodeEE）；槽3起留空原样转发给目标 | 按名字调用用户定义的字符串型式中函数并返回其返回值；目标不存在时返回第二参数；只查用户定义#FUNCTION(S)（REF 亦可）；非#FUNCTION标签抛CodeEE，内建函数不可调；目标非字符串型抛CodeEE |

### HTML 系

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `HTML_ESCAPE` | str | str | 恰好 1 | —（唯一实参不可省略，留空报 CodeEE(ArgCanNotBeNull)） | 把 & < > 及单双引号五个字符替换为 &amp; 等 HTML 字符引用后返回；只转义 & > < " ' 五个字符，空格等其余字符原样保留；与 Unescape 对称 |
| `HTML_GETPRINTEDSTR` | str | int? | 0–1 | 省略实参=0（最新行）；显式留空被拒绝，抛 CodeEE | 返回指定显示行的 HTML 格式字符串；行号取不到内容时返回空串；lineNo 自末尾计数（0=最新一行），越界返回空串；负数抛 CodeEE；GETDISPLAYLINE 则自首行计数 |
| `HTML_POPPRINTINGSTR` | str | （无实参） | 恰好 0 | — | 取走并清空待换行输出缓冲区的 HTML 字符串；无缓冲区时返回空串；结果不含 <p>，故 align 属性不生效；控制台未启用或缓冲区为空时返回空串 |
| `HTML_STRINGLEN` | int | str, int? | 1–2 | 省略则按 0 计（返回半角字符数）；留空报 ArgCanNotBeNull | 按 HTML_PRINT 排版测量 html 首行宽度；默认把像素换算成半角字符数返回；像素换算半角字符数用 ceil(2*px/FontSize)，全角记 2；超出窗口宽度会被截断 |
| `HTML_STRINGLINES` | int | str, int | 恰好 2 | —（两个实参都必填，留空报 CodeEE(ArgCanNotBeNull)） | 循环用 HtmlSubString 按 width 切分 html，累计切出的行数；空串返回 0；width 过小（含 0/负数）时一个字符都放不下，HtmlSubString 不消耗字符 → 死循环 |
| `HTML_SUBSTRING` | str | str, int | 恰好 2 | —（两个实参都必填，留空报 CodeEE(ArgCanNotBeNull)） | 按 HTML 排版宽度切出不超过 width 的前段返回；剩余部分写入 RESULTS:1；文档 API 行仍写 int，正文注意已说明 v8b 起为 str；width 先按 width*FontSize/2 换算像素 |
| `HTML_TOPLAINTEXT` | str | str | 恰好 1 | —（唯一实参必填，留空报 CodeEE(ArgCanNotBeNull)） | 用正则删掉 <> 包裹的标签，再还原 &amp; 等字符引用，返回纯文本；正则 \<[^<]*\> 删标签，尖括号不成对时按整段删除；& 无分号等非法引用会抛 CodeEE |

### AWAIT 相关

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `GETKEY` | int | int | 恰好 1 | —（实参必填：少写报个数错，留空报 ArgCanNotBeNull） | 读 Win32 GetKeyState，指定虚拟键码当前按下返回 1，否则返回 0；键码 <0 或 >255 静默返回 0；GetKeyState 依赖消息泵，不配 AWAIT/输入等待会读到陈旧值；作指令时结果写入 RESULT |
| `GETKEYTRIGGERED` | int | int | 恰好 1 | —（实参必填：少写报个数错，留空报 ArgCanNotBeNull） | 同 GETKEY，但只在键的切换状态与上次调用不同且当前按下时返回 1；普通键低位恒 0，同一键码除进程内首次外恒返回 0，与文档『按下瞬间』不符；与 GETKEY 共用 keytoggle 表，同键码混用会互相吞掉触发 |
| `ISACTIVE` | int | （无实参） | 恰好 0 | —（恰 0 实参，无空槽；写实参报 ArgsNotNeeded） | 判断窗口是否活动：活动返回 1，否则返回 0；无实参；活动判定用 Form.ActiveForm（本应用内任一窗体激活即算 1，不限于游戏窗口）；作指令时结果写入 RESULT |
| `MOUSEB` | str | （无实参） | 恰好 0 | —（恰 0 实参，无空槽；写实参报 ArgsNotNeeded） | 返回鼠标下按钮的输入值（整数按钮转成字符串，否则用原字符串），无按钮时为""；返回的是按钮的输入值而非显示文本，普通文本/无按钮都为空串；会临时置 AlwaysRefresh 并按真实光标位置刷新悬停态 |
| `MOUSEX` | int | （无实参） | 恰好 0 | —（恰 0 实参，无空槽；写实参报 ArgsNotNeeded） | 返回鼠标光标相对客户端区域左下角的 X 坐标（向右为正）；直接读 Cursor.Position，不依赖消息泵；窗口尚未创建时恒返回 0 而不报错；作指令时结果写入 RESULT |
| `MOUSEY` | int | （无实参） | 恰好 0 | —（恰 0 实参，无空槽；写实参报 ArgsNotNeeded） | 返回鼠标光标的 Y 坐标（原点在左下角，向下为正，客户端内为负）；窗口尚未创建时恒返回 0；要换成客户端左上角基准用 MOUSEY()+CLIENTHEIGHT() |

### 图像处理相关

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `CBGCLEAR` | int | （无实参） | 恰好 0 | — | 清空全部 CBG 背景项（含按钮项）并解除按钮映射，固定返回 1；清空 cbgList 后插入 zdepth=0 占位项；文档未写返回值，代码恒返回 1；WINAPI 模式的拦截已注释掉 |
| `CBGCLEARBUTTON` | int | （无实参） | 恰好 0 | — | 删除全部按钮图并清空按钮映射，固定返回 1；只删 isButton 为真的项；WINAPI 绘制模式的拦截被注释掉，两种模式都执行 |
| `CBGREMOVERANGE` | int | int, int | 恰好 2 | — | 按 Z 深度闭区间批量删除背景图，固定返回 1；zMin>zMax 静默不删、不报错；zdepth==0 的占位项永不删，故 zMin<=0 也删不掉它 |
| `CBGSETBMAPG` | int | int | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 把 gID 的 Graphics 登记为按钮映射：成功返回 1，图像未创建返回 0；CBG_SetButtonMap 的返回值被丢弃：重复设同一 gID 仍返回 1；gID<0/过大抛 CodeEE；WINAPI 抛 GDIPlusOnly |
| `CBGSETBUTTONSPRITE` | int | int, str, str, int, int, int, str? | 6–7 | 第 7 参 tooltip 可省略（取 null）；写了却留空报 CodeEE | 登记 button 对应的常态/选中精灵按钮：成功返回 1，button<0 或 >0xFFFFFF 返回 0；两个精灵名查不到也返回 1（不校验 null，与 CBGSETSPRITE 不同）；zDepth=0/越界抛 CodeEE；WINAPI 抛 GDIPlusOnly |
| `CBGSETG` | int | int, int, int, int | 恰好 4 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 把 gID 的图像按 (x,y)、zDepth 登记进背景列表：成功返回 1，图像未创建返回 0；zDepth=0 或超 int 范围、gID<0/过大均抛 CodeEE；WINAPI 绘制模式抛 GDIPlusOnly；文档未写返回值 |
| `CBGSETSPRITE` | int | str, int, int, int | 恰好 4 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 把精灵按 (x,y)、zDepth 登记进背景列表：成功返回 1，精灵不存在或已销毁返回 0；名字先 ToUpper 再查 imageDictionary，查不到即返回 0；zDepth=0/超 int 范围抛 CodeEE；文档未写返回值 |
| `GCLEAR`（候选 1） | int | int, int | 恰好 2 | —（两种形状实参数固定；留空报 ArgCanNotBeNull） | 用 cARGB 清空 gID 的 Graphics（整区，或 6 参时的矩形区）；成功 1，未创建 0；6 参矩形清屏是 EM 私有扩展，upstream 只写 2 参；x/y/w/h 不校验，w/h=0 静默无效 |
| `GCLEAR`（候选 2） | int | int, int, int, int, int, int | 恰好 6 | —（两种形状实参数固定；留空报 ArgCanNotBeNull） | （同上） |
| `GCREATE` | int | int, int, int | 恰好 3 | —（三个实参全必填；留空报 ArgCanNotBeNull） | 创建 gID 的 width×height 位图 Graphics；成功 1，gID 已存在 0；gID<0 或尺寸 ≤0/>8192(MAX_IMAGESIZE) 抛 CodeEE；已存在不报错、直接返回 0 |
| `GCREATED` | int | int | 恰好 1 | 无；须恰好1个实参，空槽抛CodeEE(ArgCanNotBeNull) | 判断 gID 的 Graphics 是否已创建：已创建返回1，未创建或已销毁返回0；gID为负或超过int上限时抛CodeEE而非返回0；WINAPI文字模式抛CodeEE |
| `GCREATEFROMFILE` | int | int, str, int? | 2–3 | 第3实参 isRelative 省略取 0(false)；留空报 ArgCanNotBeNull | 从图像文件创建 gID 的 Graphics；成功 1，已存在/找不到/非图像/过大 0；upstream 未记载第3实参：非0 时按游戏根目录解析；绝对路径须落在 resources 内 |
| `GDASHSTYLE` | int | int, int, int | 恰好 3 | — | 设置该图像画笔的虚线样式与端点形状（供GDRAWLINE用），成功返回1；文档API行误写成GDRAWline；style/cap越界抛InvalidEnumArgumentException（非CodeEE）；未设画笔时自动建笔 |
| `GDISPOSE` | int | int | 恰好 1 | —（唯一实参必填；留空报 ArgCanNotBeNull） | 销毁 gID 的 Graphics 并释放位图/画笔/字体；成功 1，未创建 0；销毁后 IsCreated 为假，再调用 GCLEAR/GDISPOSE 等只返回 0，不报错 |
| `GDRAWG`（候选 1） | int | int, int, int, int, int, int, int, int, int, int, ref* int[,]?- | 10–11 | 第11实参可省略或留空 = 不使用颜色矩阵 | 把 src 的 srcRect 区缩放画到 dest 的 destRect；成功 1，任一未创建 0；矩形仅校验 int 范围与非0尺寸，不查越界（GDI+ 自行裁剪）；dest 与 src 可同一个 |
| `GDRAWG`（候选 2） | int | int, int, int, int, int, int, int, int, int, int, ref* int[,,]?- | 10–11 | 第11实参可省略或留空 = 不使用颜色矩阵 | （同上） |
| `GDRAWGWITHMASK` | int | int, int, int, int, int | 恰好 5 | —（五个实参全必填；留空报 ArgCanNotBeNull） | 以 mask 蓝通道为不透明度把 src 画到 dest 的 (destX,destY)；成功 1，否则 0；负 destX/destY 不被拒绝，内部按行算像素下标会越界抛异常；纯 CPU 逐像素，很慢 |
| `GDRAWGWITHROTATE` | int | int, int, int, int?, int? | 3–5 | 省略x,y（共3实参）时用源图中心；留空槽报CodeEE | 把第2实参图像绕指定中心旋转Angle度后画到第1实参图像上，成功返回1；只写4个实参（省略y）检查器放行、运行期IndexOutOfRangeException；旋转的世界变换残留，同图后续绘制会叠加旋转 |
| `GDRAWLINE` | int | int, int, int, int, int | 恰好 5 | — | 在该图像上用当前画笔从(fromX,fromY)到(toX,toY)画直线，成功返回1；文档返回值列只写1，实际图像未GCREATE时返回0；未设画笔用前景色1像素临时笔；坐标超int范围抛CodeEE |
| `GDRAWSPRITE`（候选 1） | int | int, str | 恰好 2 | —（可省略第7实参，但留空一律报 ArgCanNotBeNull） | 把已创建的 Sprite 画到 gID，可指定目标矩形与颜色矩阵；成功 1，否则 0；4 参只给位置，6 参给目标矩形（缩放），7 参尾参须 5x5 数值数组；sprite 未创建返回 0 |
| `GDRAWSPRITE`（候选 2） | int | int, str, int, int | 恰好 4 | —（可省略第7实参，但留空一律报 ArgCanNotBeNull） | （同上） |
| `GDRAWSPRITE`（候选 3） | int | int, str, int, int, int, int, ref* int[,]? | 6–7 | —（可省略第7实参，但留空一律报 ArgCanNotBeNull） | （同上） |
| `GDRAWSPRITE`（候选 4） | int | int, str, int, int, int, int, ref* int[,,]? | 6–7 | —（可省略第7实参，但留空一律报 ArgCanNotBeNull） | （同上） |
| `GDRAWTEXT` | int | int, str, int?, int? | 2–4 | 省略尾参 x,y 取 (0,0)；留空 x/y 抛 CodeEE | 在 gID 的 Graphics 上绘制文本，并把文本宽高写入 RESULT:1/2，返回 1；3 个实参能过检查却不绘制（只写 RESULT）；未创建返回 0 且不写 RESULT；RESULT:1=宽、2=高 |
| `GFILLRECTANGLE` | int | int, int, int, int, int | 恰好 5 | —（五个实参全必填；留空报 ArgCanNotBeNull） | 用当前画刷填充 gID 上的矩形；成功 1，gID 未创建 0；upstream 说缺省用配置文字色，代码实际用 Config.BackColor；width/height 为 0 抛 CodeEE |
| `GGETBRUSH` | int | int | 恰好 1 | — | 返回该图像GSETBRUSH设定的单色画刷颜色（无符号ARGB）；未创建返回0；WINAPI文字模式抛CodeEE；GCREATE后未调用GSETBRUSH时brush为null，取色抛NullReferenceException |
| `GGETCOLOR` | int | int, int, int | 恰好 3 | 无；须恰好3个实参，空槽抛CodeEE(ArgCanNotBeNull) | 取 gID 图像 (x,y) 像素颜色（0xAARRGGBB）；未创建或超界返回-1，成功返回ARGB值；越界判断把x<0写了两遍、漏判y<0，y为负时Bitmap.GetPixel抛.NET异常；全透明黑返回0而非-1 |
| `GGETFONT` | str | int | 恰好 1 | — | 取得 gID 图像的字体名；图像未创建时返回空串；未创建返回空串；已创建但未 GSETFONT（或 GSETFONT 失败）时 font 为 null，取 font.Name 抛 NRE |
| `GGETFONTSIZE` | int | int | 恰好 1 | — | 取得 gID 图像的字号（GSETFONT 传入的像素值）；未创建时返回 0；返回 (int)font.Size；未 GSETFONT 时读 font.Size 抛 NRE；负数 gID 抛 CodeEE(GIdIsNegative) |
| `GGETFONTSTYLE` | int | int | 恰好 1 | — | 取得字体样式位（1粗体 2斜体 4删除线 8下划线）；未创建时返回 0；读独立 style 字段，未 GSETFONT 时为 0 而不抛 NRE；只回显 GSETFONT 的 1/2/4/8 位 |
| `GGETPEN` | int | int | 恰好 1 | — | 返回该图像GSETPEN设定的画笔颜色（无符号ARGB）；未创建返回0；WINAPI文字模式抛CodeEE；GCREATE后未调用GSETPEN时pen为null，取色抛NullReferenceException |
| `GGETPENWIDTH` | int | int | 恰好 1 | — | 返回该图像GSETPEN设定的画笔宽度；未创建返回0；WINAPI模式抛CodeEE；未设画笔时pen为null抛NullReferenceException；宽度按float回读并截断 |
| `GGETTEXTSIZE` | int | str, str, int, int? | 3–4 | 省略 fontStyle 取 Regular(0)；留空抛 CodeEE | 用字体名、字号、样式量测文本，返回宽并把高写入 RESULT:1；宽高按 SizeF 截断为整数；字体名非法或字号<=0 时 Font 构造抛异常，无 try/catch |
| `GHEIGHT` | int | int | 恰好 1 | 无；须恰好1个实参，空槽抛CodeEE(ArgCanNotBeNull) | 返回 gID 图形的像素高度；未创建或已销毁返回0；gID为负或超过int上限时抛CodeEE；WINAPI文字模式抛CodeEE |
| `GLOAD` | int | int, int | 恰好 2 | 无；须恰好2个实参，空槽抛CodeEE(ArgCanNotBeNull) | 从 sav 目录按编号读取 GSAVE 保存的 PNG 创建 gID 图形，成功返回1，失败返回0；gID已存在时先返回0、不校验fileNo；fileNo越界/文件不存在/加载异常/尺寸超8192均返回0 |
| `GSAVE` | int | int, int | 恰好 2 | 无；须恰好2个实参，空槽抛CodeEE(ArgCanNotBeNull) | 把 gID 图形以 PNG 存到 sav 目录（文件名 img+4位编号），成功返回1，失败返回0；路径形如sav/img0000.png（fileNo零填充4位）；fileNo越界、目录创建或保存异常均返回0 |
| `GSETBRUSH` | int | int, int | 恰好 2 | 无；须恰好2个实参，空槽抛CodeEE(ArgCanNotBeNull) | 为 gID 的 Graphics 设置单色画刷（供 GFILLRECTANGLE 填充用），成功返回1，未创建返回0；cARGB不在0–0xFFFFFFFF内抛CodeEE；WINAPI文字模式下抛GDIPlusOnly；旧画刷由GSetBrush释放 |
| `GSETCOLOR` | int | int, int, int, int | 恰好 4 | 无；须恰好4个实参，空槽抛CodeEE(ArgCanNotBeNull) | 把 gID 图像在 (x,y) 处的像素改成 cARGB 颜色，成功返回1，未创建或超界返回0；越界判断把x<0写了两遍、漏判y<0，y为负时Bitmap.SetPixel抛.NET异常而非返回0；WINAPI模式抛CodeEE |
| `GSETFONT` | int | int, str, int, int? | 3–4 | 第4个实参可整体省略，省略时取Regular=0；不能写成空槽 | 为 gID 设置字体（名称、像素字号、可选样式），成功返回1，未创建或字体创建失败返回0；文档参数表把fontSize误写为string；样式位1/2/4/8对应Bold/Italic/Strikeout/Underline，未知位忽略 |
| `GSETPEN` | int | int, int, int | 恰好 3 | 无；须恰好3个实参，空槽抛CodeEE(ArgCanNotBeNull) | 为 gID 设置画笔颜色与线宽，供 GDRAWLINE/GDRAWTEXT 使用，成功返回1，未创建返回0；penWidth不做范围校验，负宽度由GDI+静默当1处理；cARGB越界抛CodeEE；重设画笔会保留已有虚线样式 |
| `GWIDTH` | int | int | 恰好 1 | 无；须恰好1个实参，空槽抛CodeEE(ArgCanNotBeNull) | 返回 gID 图形的像素宽度；未创建或已销毁返回0；gID为负或超过int上限时抛CodeEE；WINAPI文字模式抛CodeEE |
| `SETANIMETIMER` | int | int | 恰好 1 | — | 设置动画重绘定时器的时间间隔（毫秒），固定返回 1；文档称仅支持命令/无返回值，代码却注册为返回 int 的式中函数；上限误用 short.MaxValue(32767)，超限抛 CodeEE |
| `SPRITEANIMEADDFRAME` | int | str, int, int, int, int, int, int, int, int | 恰好 9 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 给动画精灵加一帧（截 gID 的矩形，按偏移放置、delay 毫秒显示）；成功返回 1，失败返回 0；delay≤0 或 >int.MaxValue、矩形越界或宽高≤0 都返回 0（不加帧）；矩形宽高为 0/超 int 范围抛 CodeEE |
| `SPRITEANIMECREATE` | int | str, int, int | 恰好 3 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 新建指定名字与宽高的动画精灵（帧需再用 SPRITEANIMEADDFRAME 加）；成功返回 1，重名返回 0；width/height 须 >0 且 ≤8192，否则抛 CodeEE；名字为空返回 0；动画精灵 IsCreated 恒真，故同名必失败 |
| `SPRITECREATE`（候选 1） | int | str, int | 恰好 2 | 无槽可留空：实参数只能 2 或 6 个，其余都报 CodeEE | 用 gID 图形（整幅或截取矩形）建名为 spriteName 的精灵；成功返回 1，失败返回 0；2 参取整幅且尺寸建时冻结；矩形与父图不交叠、w/h 为 0 抛 CodeEE；空名/重名/gID 未创建返回 0 |
| `SPRITECREATE`（候选 2） | int | str, int, int, int, int, int | 恰好 6 | 无槽可留空：实参数只能 2 或 6 个，其余都报 CodeEE | （同上） |
| `SPRITECREATED` | int | str | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 判断名为 spriteName 的精灵当前是否存在（含 csv 声明与代码创建）；存在返回 1，否则 0；动画精灵一旦建成即恒返回 1（IsCreated 恒真）；名字大小写不敏感，csv 资源也能查到 |
| `SPRITEDISPOSE` | int | str | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 释放指定精灵（只解绑名字，不动父 Graphics）；已创建返回 1，否则返回 0；唯一不检查 GDI 模式的精灵函数（WINAPI 下也能用）；名字大小写不敏感；csv 同名资源可被 SPRITEDISPOSEALL(0) 恢复 |
| `SPRITEDISPOSEALL` | int | int | 恰好 1 | — | 销毁全部精灵，参数非0时连CSV精灵一起销毁，返回被销毁的数量；参数按!=0判断；0分支只替换字典不Dispose，已单独销毁的CSV精灵会被复活，返回值可能为负 |
| `SPRITEGETCOLOR` | int | str, int, int | 恰好 3 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 取精灵在 (x,y) 处的颜色（0xAARRGGBB）；精灵不存在或坐标越界返回 -1；对动画精灵取色（坐标在范围内）会抛 NotSupportedException；全透明黑返回 0；x/y 超 int 范围抛 CodeEE |
| `SPRITEHEIGHT` | int | str | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 返回精灵的高度（建精灵时定下的基准尺寸）；精灵不存在或已销毁返回 0；非实时父图尺寸：csv 精灵取 destSize，SPRITECREATE 截取矩形宽高为负时取绝对值 |
| `SPRITEMOVE` | int | str, int, int | 恰好 3 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 把 (movex,movey) 叠加到精灵的相对偏移上：成功返回 1，精灵不存在返回 0；改的是随绘制缩放的相对偏移字段 DestBasePosition，无正负/范围限制；x/y 超 int 范围在 ReadPoint 抛 CodeEE |
| `SPRITEPOSX` | int | str | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 返回精灵相对位置的 X（由 SPRITESETPOS/SPRITEMOVE 设定）；精灵不存在返回 0；是随绘制一起缩放的相对偏移，默认 0；本桶不含改它的 SPRITESETPOS/SPRITEMOVE |
| `SPRITEPOSY` | int | str | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 返回精灵相对位置的 Y（由 SPRITESETPOS/SPRITEMOVE 设定）；精灵不存在返回 0；是随绘制一起缩放的相对偏移，默认 0；本桶不含改它的 SPRITESETPOS/SPRITEMOVE |
| `SPRITESETPOS` | int | str, int, int | 恰好 3 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 把精灵的相对偏移直接设为 (x,y)：成功返回 1，精灵不存在返回 0；与 SPRITEMOVE 共用 SpriteSetPosMethod，仅按注册名分派赋值/叠加；同样受 ReadPoint 的 int 范围检查；upstream 把实参写作 posX/posY |
| `SPRITEWIDTH` | int | str | 恰好 1 | 无槽可留空：显式留空与少写尾实参都报 CodeEE | 返回精灵的宽度（建精灵时定下的基准尺寸）；精灵不存在或已销毁返回 0；非实时父图尺寸：csv 精灵取 destSize，SPRITECREATE 截取矩形宽高为负时取绝对值 |

### 声音系

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `EXISTSOUND` | int | str | 恰好 1 | 无；个数须恰好 1；显式空槽报 CodeEE(ArgCanNotBeNull) | 在游戏 sound/ 目录内解析实参指定的音频文件名，文件存在返回 1，否则返回 0；不补扩展名、不做模糊匹配，与 PLAYSOUND 同一解析；越界路径(如 ..\x.wav)与空串直接返回 0；命令写法亦可(返回值丢弃) |

### XML 系

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `XML_ADDATTRIBUTE`（候选 1） | int | int, str, str, str?, int?, int? | 3–6 | attrValue 省略取空串；methodType/doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | 给 xpath 匹配的节点新增属性，返回新增次数；无匹配返 0，目标文档不存在返 -1；methodType=0 要求选中元素节点，选中属性/文本/文档节点时不新增（原为运行期空引用异常）；1/2 要求选中属性节点，选中元素/文本/文档节点时不新增；「不新增」时单匹配返 0、多匹配沿用既定的「返匹配数」；多匹配复用同一属性只落最后一个 |
| `XML_ADDATTRIBUTE`（候选 2） | int | ref str, str, str, str?, int?, int? | 3–6 | attrValue 省略取空串；methodType/doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | （同上） |
| `XML_ADDATTRIBUTE_BYNAME` | int | str, str, str, str?, int?, int? | 3–6 | attrValue 省略取空串；methodType/doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | 以 xmlName 指定的 XmlDocument 为目标，给 xpath 匹配节点新增属性并返回新增次数；第 1 实参须为字符串（变量或字面量皆可，按内容查表）；其余坑同 XML_ADDATTRIBUTE |
| `XML_ADDNODE`（候选 1） | int | int, str, str, int?, int? | 3–5 | methodType/doSetAll 省略取 0（doSetAll 要给满 5 个实参才读）；显式留空抛 CodeEE | 向 xpath 选中的元素插入 nodeXml 新节点，返回匹配（新增）次数；无匹配返 0；多匹配且 doSetAll=0 时不新增却返回匹配数；setAll 复用同一节点实例，实际只落最后一个；methodType=0 要求选中元素节点，选中属性/文本/文档节点时不新增；1/2 要求参照节点的父节点是元素（元素节点与文本节点皆可），选中根/文档/属性节点时不新增；「不新增」时单匹配返 0、多匹配仍返匹配数 |
| `XML_ADDNODE`（候选 2） | int | ref str, str, str, int?, int? | 3–5 | methodType/doSetAll 省略取 0（doSetAll 要给满 5 个实参才读）；显式留空抛 CodeEE | （同上） |
| `XML_ADDNODE_BYNAME` | int | str, str, str, int?, int? | 3–5 | methodType/doSetAll 省略取 0（doSetAll 要给满 5 个实参才读）；显式留空抛 CodeEE | 以 xmlName 指定的 XmlDocument 为目标，向 xpath 选中的元素插入 nodeXml 并返回新增次数；返回类型与同族主名一样是 int；多匹配/doSetAll 的坑与 XML_ADDNODE 相同 |
| `XML_DOCUMENT` | int | any, str | 恰好 2 | 无；个数须恰好 2；显式空槽报 CodeEE(ArgCanNotBeNull) | 解析 XML 文本并以字符串 ID 存入文档表，成功返回 1，该 ID 已被占用则返回 0；解析失败抛 CodeEE（消息文本误用 XML_GET 前缀）；ID 取字符串实参或整数的 TOSTR，不做归一化 |
| `XML_EXIST` | int | any | 恰好 1 | 无；个数须恰好 1；显式空槽报 CodeEE(ArgCanNotBeNull) | 按字符串 ID 查文档表，存在返回 1，不存在返回 0；RESETDATA 与返回标题界面会自动清空文档表；整数实参按 TOSTR 结果查找 |
| `XML_GET`（候选 1） | int | any, str, int?, int? | 2–4 | 省略第3参=不输出，第4参=0(Value)；显式空槽报 CodeEE | 按 xpath 检索节点并返回匹配数，可写入 RESULTS 或字符串数组；文档 ID 不存在返回 -1；第 1 参为整型才当 ID，字符串一律当 XML 文本；RESULTS 写入受长度截断，返回值仍是完整匹配数 |
| `XML_GET`（候选 2） | int | any, str, ref str[], int? | 3–4 | 省略第3参=不输出，第4参=0(Value)；显式空槽报 CodeEE | （同上） |
| `XML_GET_BYNAME`（候选 1） | int | any, str, int?, int? | 2–4 | 省略第3参=不输出，第4参=0(Value)；显式空槽报 CodeEE | 以字符串名为文档 ID 按 xpath 检索节点，返回匹配数；文档不存在返回 -1；第 1 参字符串一律按名查表，不能当作 XML 文本传入；其余语义同 XML_GET |
| `XML_GET_BYNAME`（候选 2） | int | any, str, ref str[], int? | 3–4 | 省略第3参=不输出，第4参=0(Value)；显式空槽报 CodeEE | （同上） |
| `XML_RELEASE` | int | any | 恰好 1 | 无；个数须恰好 1；显式空槽报 CodeEE(ArgCanNotBeNull) | 删除字符串 ID 对应的文档，成功返回 1；该 ID 不存在时返回 0；文档未写 ID 不存在时的行为：代码返回 0 且不报错 |
| `XML_REMOVEATTRIBUTE`（候选 1） | int | int, str, int? | 2–3 | doSetAll 省略取 0（要给满 3 个实参才读）；显式留空抛 CodeEE(ArgCanNotBeNull) | 删除 xpath 选中的属性，返回匹配（删除）次数；xpath 选中元素或无匹配返 0；删除无方向参数，函数体读槽 3 是死代码；多匹配且 doSetAll=0 时不删却返回匹配数（与文档「失败返 0」不符）；xpath 须选属性（/xml/@a） |
| `XML_REMOVEATTRIBUTE`（候选 2） | int | ref str, str, int? | 2–3 | doSetAll 省略取 0（要给满 3 个实参才读）；显式留空抛 CodeEE(ArgCanNotBeNull) | （同上） |
| `XML_REMOVEATTRIBUTE_BYNAME` | int | str, str, int? | 2–3 | doSetAll 省略取 0（要给满 3 个实参才读）；显式留空抛 CodeEE(ArgCanNotBeNull) | 以 xmlName 指定的 XmlDocument 为目标，删除 xpath 选中的属性并返回删除次数；第 1 实参为字符串名，按内容查表；返回类型与主名同为 int；坑同 XML_REMOVEATTRIBUTE |
| `XML_REMOVENODE`（候选 1） | int | int, str, int? | 2–3 | doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | 删除 xpath 选中的节点，返回匹配（删除）次数；根节点或无匹配返 0；删除无 methodType，函数内读槽 3 的局部变量是死代码；多匹配 doSetAll=0 时未删却返回匹配数；xpath 选中属性时 ParentNode 为 null 返 0 |
| `XML_REMOVENODE`（候选 2） | int | ref str, str, int? | 2–3 | doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | （同上） |
| `XML_REMOVENODE_BYNAME` | int | str, str, int? | 2–3 | doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | 以 xmlName 指定的 XmlDocument 为目标，删除 xpath 选中的节点并返回删除次数；第 1 实参为字符串名，按内容查表；删除无方向参数，坑同 XML_REMOVENODE |
| `XML_REPLACE`（候选 1） | int | any, str | 恰好 2 | doSetAll 省略取 0，2 参形态无可省略槽；显式留空抛 CodeEE(ArgCanNotBeNull) | 整体替换 xmlId 文档或替换 xpath 选中的节点，返回更新次数；目标不存在返 -1；2 参时第 1 实参按名字查表（字符串变量也不回写，整体替换固定返 1）；newXml 先解析，非法 XML 先抛 CodeEE 而非返 -1 |
| `XML_REPLACE`（候选 2） | int | int, str, str, int? | 3–4 | doSetAll 省略取 0，2 参形态无可省略槽；显式留空抛 CodeEE(ArgCanNotBeNull) | （同上） |
| `XML_REPLACE`（候选 3） | int | ref str, str, str, int? | 3–4 | doSetAll 省略取 0，2 参形态无可省略槽；显式留空抛 CodeEE(ArgCanNotBeNull) | （同上） |
| `XML_REPLACE_BYNAME` | int | str, str, str, int? | 3–4 | doSetAll 省略取 0；显式留空抛 CodeEE(ArgCanNotBeNull) | 以 xmlName 指定的 XmlDocument 为目标，替换 xpath 选中的节点并返回更新次数；只有 3-4 参形态，没有整体替换；xpath 选中根或属性时 Replace 返 0；多匹配 doSetAll=0 时未替换却返回匹配数 |
| `XML_SET`（候选 1） | int | int, str, str, int?, int? | 3–5 | 省略第4参=0(不批量)，第5参=0(Value)；显式空槽报 CodeEE | 把 xpath 命中节点的内容设为 value，返回匹配节点数；文档 ID 不存在返回 -1；doSetAll=0 且多匹配时不更新却仍返回匹配数（文档称失败返 0）；仅匹配数>0 时回写第 1 参变量；// 提示与实测不符 |
| `XML_SET`（候选 2） | int | ref str, str, str, int?, int? | 3–5 | 省略第4参=0(不批量)，第5参=0(Value)；显式空槽报 CodeEE | （同上） |
| `XML_SET_BYNAME` | int | str, str, str, int?, int? | 3–5 | 省略第4参=0(不批量)，第5参=0(Value)；显式空槽报 CodeEE | 以字符串名为文档 ID，把 xpath 命中节点内容设为 value，返回匹配节点数；第 1 参是普通字符串（可为字面量），非 ref；文档不存在返回 -1，其余同 XML_SET |
| `XML_TOSTR` | str | any | 恰好 1 | 无；个数须恰好 1；显式空槽报 CodeEE(ArgCanNotBeNull) | 返回字符串 ID 对应文档的 OuterXml；该 ID 不存在时返回空字符串；文档未写 ID 不存在时的行为：代码返回空串；输出为 OuterXml（空元素展开成 <xml />） |

### MAP（关联数组）系

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `MAP_CLEAR` | int | str | 恰好 1 | —（mapName 必填；省略报参数数不足，留空报 CodeEE） | 清空指定 MAP 的全部键值对，MAP 存在返回 1、不存在返回 -1，MAP 本身仍保留；MAP 不存在（名字写错也算）一律返回 -1；清空后 MAP 仍存在，MAP_SIZE 变 0 |
| `MAP_CREATE` | int | str | 恰好 1 | —（mapName 必填；省略报参数数不足，留空报 CodeEE） | 新建一个空的 string→string 字典；已存在返回 0，创建成功返回 1；已存在时不覆盖也不报错，直接返回 0；返回标题界面或 RESETDATA 会自动删除全部 MAP |
| `MAP_EXIST` | int | str | 恰好 1 | —（mapName 必填；省略报参数数不足，留空报 CodeEE） | 查询指定名字的 MAP 是否存在；存在返回 1，不存在返回 0；名字比较区分大小写（Dictionary 默认比较器），myMap 与 MyMap 视为两个 MAP |
| `MAP_FROMXML` | int | str, str | 恰好 2 | —（mapName/xmlMap 必填；省略报不足，留空报 CodeEE） | 把 XML 文本解析进指定 MAP；成功返回 1，MAP 不存在返回 0；不先清空：同名键被覆盖、其余旧键保留；XML 非法抛 CodeEE；缺 k 或 v 的 p 被跳过；值取 InnerXml、键取 InnerText |
| `MAP_GET` | str | str, str | 恰好 2 | —（mapName、key 均必填；省略报参数数不足，留空报 CodeEE） | 取指定 MAP 中 key 的值；MAP 或 key 不存在返回空串，不报错；空串既可能是真值也可能是查不到，需先用 MAP_HAS / MAP_EXIST 判别；键名区分大小写 |
| `MAP_GETKEYS`（候选 1） | str | str, int? | 1–2 | 省第2/3实参走1参形态；doOutput 留空报错、写 0 返回空串 | 输出指定 MAP 的全部键名，按实参个数有逗号串 / RESULTS / 字符串数组三种形态；MAP 不存在返回空串且不动 RESULTS/RESULT；写数组时只填到数组长度，RESULT 仍为完整键数 |
| `MAP_GETKEYS`（候选 2） | str | str, ref str[], int | 恰好 3 | 省第2/3实参走1参形态；doOutput 留空报错、写 0 返回空串 | （同上） |
| `MAP_HAS` | int | str, str | 恰好 2 | —（mapName、key 均必填；省略报参数数不足，留空报 CodeEE） | 查询 key 是否存在于指定 MAP；存在 1、无此 key 0、MAP 不存在 -1；0 与 -1 语义不同，判存在请写 =1；MAP 不存在不会退化成 0（与不报错的 MAP_GET 不同） |
| `MAP_RELEASE` | int | str | 恰好 1 | —（mapName 必填；省略报参数数不足，留空报 CodeEE） | 删除指定名字的 MAP 及其全部内容；无论是否存在都返回 1；MAP 不存在也不报错、仍返回 1；与 MAP_CLEAR / MAP_SIZE 的 -1 语义不同，不能用返回值判存在 |
| `MAP_REMOVE` | int | str, str | 恰好 2 | —（mapName、key 均必填；省略报参数数不足，留空报 CodeEE） | 删除指定 MAP 中的 key；MAP 存在返回 1，MAP 不存在返回 -1；key 本来就不存在也返回 1（Map.Remove 的结果被忽略），文档未区分这两种情况 |
| `MAP_SET` | int | str, str, str | 恰好 3 | —（3 个实参都必填；省略报不足，留空报 CodeEE） | 设置 MAP 中 key 的值（键不存在则新增）；成功 1，MAP 不存在 -1；value 允许空字符串，但要存空串必须写 ""，留空槽会报 CodeEE |
| `MAP_SIZE` | int | str | 恰好 1 | —（mapName 必填；省略报参数数不足，留空报 CodeEE） | 返回指定 MAP 中键值对的数量；MAP 不存在返回 -1；空 MAP 返回 0，与 MAP 不存在（-1）可区分 |
| `MAP_TOXML` | str | str | 恰好 1 | —（mapName 必填；省略报参数数不足，留空报 CodeEE） | 把指定 MAP 序列化为 XML 字符串返回；MAP 不存在返回空串；键值不做 XML 转义，含 < & 等字符时产出的文本 MAP_FROMXML 解不开；顺序即 Dictionary 枚举序 |

### DataTable（数据库）系

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `DT_CELL_GET` | int | str, int, str, int? | 3–4 | 省略 asId 视为 0（按行号）；asId 留空报 CodeEE | 取指定单元格的整数；表、行列不存在或值为空都返回 0；字符串列存非数字文本时走 Convert.ToInt64 会抛异常而非返回 0；asId 用主键 id 查行 |
| `DT_CELL_GETS` | str | str, int, str, int? | 3–4 | 省略 asId 视为 0（按行号）；asId 留空报 CodeEE | 取指定单元格的字符串；表、行列不存在或值为空时返回空串；asId 分支直接 (string) 强转，对整数列会抛 InvalidCastException；按行号分支用 ToString 不抛 |
| `DT_CELL_ISNULL` | int | str, int, str, int? | 3–4 | 省略 asId 视为 0（按行号）；asId 留空报 CodeEE | 判断单元格是否为空；空 1、非空 0、表不存在 -1、行列不存在 -2；GET/GETS 失败都只有一种返回值，ISNULL 用 -1/-2 区分表与行列两类错误，且不抛异常 |
| `DT_CELL_SET` | int | str, int, str, any?-, int? | 3–5 | 留空/省略第 4 槽都写 DBNull；asId 省略按行号、留空报错 | 把单元格赋值为 value（留空写 DBNull）；1 成功、0 写 id 列、-1/-2/-3 各类失败；类型是否相符按实参静态类型判断（不试转换），不符只返回 -2 不报错；列名 id 不区分大小写，写 id 返回 0 |
| `DT_CLEAR` | int | str | 恰好 1 | —（name 必填；省略报参数数不足，留空报 CodeEE） | 清空指定 DataTable 的全部行，保留列设置；表存在返回 1、不存在 -1；清空后 DT_ROW_LENGTH 为 0，列数与 id 列不变；DT_CREATE 会自动加 id 列 |
| `DT_COLUMN_ADD` | int | str, str, any?, int? | 2–4 | 省略 type 默认 string；省略 nullable 视为可空 | 新加一列（可指定数据类型与是否可空）；列已存在返回 0，成功返回 1；type 名区分大小写（只有小写 int8/int16/int32/int64/string）；非法 type 或数值抛 CodeEE；表不存在返回 -1 |
| `DT_COLUMN_EXIST` | int | str, str | 恰好 2 | —（两个实参都必填；省略报参数数不足，留空报 CodeEE） | 查列是否存在；存在返回类型码（1=int8…5=string），不存在返回 0；文档漏了第三种结果：DataTable 本身不存在时返回 -1；类型码与 DT_COLUMN_ADD 的 type 编号一致 |
| `DT_COLUMN_LENGTH` | int | str | 恰好 1 | —（name 必填；省略报参数数不足，留空报 CodeEE） | 返回指定 DataTable 的列数；表不存在返回 -1；DT_CREATE 自动加的 id 列也计入，所以刚建好的表就有 1 列 |
| `DT_COLUMN_NAMES` | int | str, ref str[]?- | 1–2 | 省略或留空第 2 实参都改写 RESULTS；表不存在返回 -1 | 把列名依次写入字符串数组（省略则写 RESULTS），返回列数；写用户数组时按列数逐项写、不检查数组长度，数组短于列数会越界抛异常；RESULTS 路径安全 |
| `DT_COLUMN_REMOVE` | int | str, str | 恰好 2 | —（两个实参都必填；省略报参数数不足，留空报 CodeEE） | 删除指定列；成功返回 1，列不存在或试图删 id 列返回 0；表不存在返回 -1；id 判定不区分大小写（写 ID 同样删不掉）；删列会连带丢掉该列数据 |
| `DT_CREATE` | int | str | 恰好 1 | —（name 必填；省略报参数数不足，留空报 CodeEE） | 新建一张 DataTable（自动带 id 主键列）；已存在返回 0，成功 1；id 列类型 int64、唯一主键、不允许 NULL；默认区分大小写；返回标题或 RESETDATA 会自动删除全部表 |
| `DT_EXIST` | int | str | 恰好 1 | —（name 必填；省略报参数数不足，留空报 CodeEE） | 查询指定名字的 DataTable 是否存在；存在 1，不存在 0；名字比较区分大小写（Dictionary 默认比较器），两张表名只差大小写会各建一张 |
| `DT_FROMXML` | int | str, str, str | 恰好 3 | —（3 个槽都必填，显式留空报 ArgCanNotBeNull） | 用 XML 模式与数据重建并替换指定名的 DataTable，成功返回 1、解析异常返回 0；文档说“覆盖”，实为新建表后整体替换字典项；解析失败时原表不动；表名以 schema 内声明为准，字典键仍取第 1 实参 |
| `DT_NOCASE` | int | str, int | 恰好 2 | —（name、ignoreCase 都必填；省略/留空都报错） | 设置 DT_SELECT 字符串比较是否忽略大小写；成功 1，表不存在 -1；写 0 为区分大小写（默认）、非 0 为忽略；它改的是 DataTable.CaseSensitive，列名匹配也受其影响 |
| `DT_RELEASE` | int | str | 恰好 1 | —（name 必填；省略报参数数不足，留空报 CodeEE） | 删除指定 DataTable 及其全部行列；无论是否存在都返回 1；表不存在也不报错、仍返回 1；与 DT_CLEAR、DT_COLUMN_LENGTH 的 -1 语义不同，无法用返回值判存在 |
| `DT_ROW_ADD`（候选 1） | int | str, str…?, any…?-, （尾部：自第 2 槽起按末位规则重复） | >= 1，且超出 1 的部分按每 2 个一组（(n-1)%2=0） | 成对形式的值槽留空写 DBNull；列名槽与数组形式各槽留空报 CodeEE | 新增一行并赋值，返回新行的 id（时间点值）；表不存在返回 -1；id 取内部时间点而非自增序号；列名写错或写 id、值类型与列类型不符都抛 CodeEE；只给表名会插入空行 |
| `DT_ROW_ADD`（候选 2） | int | str, ref str[], ref any[], int | 恰好 4 | 成对形式的值槽留空写 DBNull；列名槽与数组形式各槽留空报 CodeEE | （同上） |
| `DT_ROW_LENGTH` | int | str | 恰好 1 | —（name 必填；省略报参数数不足，留空报 CodeEE） | 返回指定 DataTable 的行数；表不存在返回 -1；与 DT_COLUMN_LENGTH 同一实现，只差取 Rows.Count 还是 Columns.Count |
| `DT_ROW_REMOVE`（候选 1） | int | str, int | 恰好 2 | —（各实参都不可留空；数组形式的 count 必须给） | 按 id 删行；单值形式成功 1、数组形式返回删除行数，表不存在 -1；单值形式找不到该 id 返回 0（文档只写了返回 1）；批量形式 count<=0 返回 0，且 count 会先被数组长度截断 |
| `DT_ROW_REMOVE`（候选 2） | int | str, ref int[], int | 恰好 3 | —（各实参都不可留空；数组形式的 count 必须给） | （同上） |
| `DT_ROW_SET`（候选 1） | int | str, int, str…, any…, （尾部：自第 3 槽起按末位规则重复） | >= 4，且超出 2 的部分按每 2 个一组（(n-2)%2=0） | —（各实参都不可留空；数组形式的 count 必给） | 按 id 定位行并改若干列，返回成功设置的值个数；表不存在 -1；该 id 不存在返回 -2（文档未列）；列名写错或写 id、值类型与列类型不符都抛 CodeEE |
| `DT_ROW_SET`（候选 2） | int | str, int, ref str[], ref any[], int | 恰好 5 | —（各实参都不可留空；数组形式的 count 必给） | （同上） |
| `DT_SELECT` | int | str, str?-, str?-, ref int[]? | 1–4 | 槽2、3 省略或留空＝不筛选/不排序；槽4 省略则写 RESULT:1 起 | 筛选并按规则排序 DataTable，把首列(id)值写入 output 或 RESULT，返回命中行数；表不存在返回 -1、RESULT:0 写为命中数（文档未载）；取第 0 列而非名为 id 的列；数组写满即截断但返回总数；表达式非法抛非 CodeEE 异常 |
| `DT_TOXML` | str | str, ref str[]? | 1–2 | 槽2 省略则模式存 RESULTS:1；留空报 ArgCanNotBeNull | 把指定 DataTable 序列化为 XML，模式写入所传的一维字符串数组变量，返回数据 XML；文档未载表不存在时返回空串且不写模式；模式写入所传数组下标 0（RESULT 系才从 1 起）；槽2 给出时须为一维字符串数组变量（规则档 `RefString1D`+`NotNull`），标量/二维/三维在检查期报 `CodeEE(ArgIsNotNDStrArray)`，不再是运行期空引用；即 WriteXmlSchema/WriteXml 输出 |

### 其他

| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `CBGREMOVEBMAP` | int | （无实参） | 恰好 0 | — | 解除 CBGSETBMAPG 设置的按钮映射，固定返回 1；无论此前是否设置过映射都返回 1；文档未写返回值；GDIPLUS 限制检查已被注释掉 |
| `CLEARMEMORY` | int | （无实参） | 恰好 0 | —（恰 0 实参；写实参报 ArgsNotNeeded） | 执行 GC.Collect() 并返回清理前后进程工作集内存之差（byte），可能为负；只调 GC.Collect()，不等终结器也不压缩 LOH；没有可释放对象时返回 0，内存增长时为负 |
| `CLIENTHEIGHT` | int | （无实参） | 恰好 0 | —（恰 0 实参；写实参报 ArgsNotNeeded） | 返回主绘图区（客户端区）当前高度的像素值；无失败路径；取 Console.ClientHeight（即 MainPicBox.Height），随窗口缩放实时变化；窗口未建时是控件默认值 |
| `CLIENTWIDTH` | int | （无实参） | 恰好 0 | —（恰 0 实参；写实参报 ArgsNotNeeded） | 返回主绘图区（客户端区）当前宽度的像素值；无失败路径；取 Console.ClientWidth（即 MainPicBox.Width）；与 CLIENTHEIGHT 一起用于把客户端坐标换算 |
| `ENUMFILES` | int | str, str?, int?, ref str[]? | 1–4 | 留空报错；省略则 pattern="*"、option=0、写入 RESULTS | 枚举目录下匹配的文件，写入 RESULTS 或指定字符串数组，返回写入条数；返回 min(命中数,目标数组长) 而非命中总数；目录非法/不存在或模式含分隔符时返回 -1 |
| `EXISTFILE` | int | str | 恰好 1 | —（恰 1 实参；空字符串合法但返回 0，空槽报错） | 判断相对游戏根的文件是否存在；存在返回 1，否则（含路径非法）返回 0；目录也返回 0（File.Exists 语义）；越出游戏根或含非法字符返回 0 不抛错；根内 .. 回退仍合法 |
| `EXPONENT` | int | int | 恰好 1 | — | 返回 e 的实参次幂，结果向零截断为整数；超 int64 或溢出时报错；结果截断：EXPONENT(-1)=0；实参≥44 结果即超 int64 抛 CodeEE，极大值报溢出 |
| `FLOWINPUTS` | int | int, str? | 1–2 | 第 2 实参留空抛 CodeEE；整体省略则沿用上次的默认字符串 | 设定系统流程的 INPUT 是否按 INPUTS（字符串输入）处理并给出默认串，返回 0；文档标返回值 void，代码实际返回 int 0；只作用于系统流程 INPUT，不改变 ERB 内 INPUT 命令 |
| `GETCONFIG` | int | str | 恰好 1 | —（恰 1 实参；空槽报错，空字符串另抛 ArgIsEmptyString） | 取配置项（replace.csv 可覆盖）的整数值；该配置不是整数时抛 CodeEE；未知项名或不可取值的项抛 CodeEE；值为字符串时抛 InvalidType 提示改用 GETCONFIGS；YES/NO 记 1/0 |
| `GETCONFIGS` | str | str | 恰好 1 | —（恰 1 实参；空槽报错，空字符串另抛 ArgIsEmptyString） | 取配置项的字符串值；该配置在 ERB 中是整数时抛 CodeEE；两边按取值后的实际类型判定：走 default 分支的项若值形如数字会取到整数，只能配 GETCONFIG |
| `GETDOINGFUNCTION` | str | （无实参） | 恰好 0 | — | 返回当前执行行所属标签（函数）名，取不到扫描行时返回空串；文档无说明；CanRestructure=true，作实参时会在解析期折叠为当时的扫描标签名 |
| `GETMEMORYUSAGE` | int | （无实参） | 恰好 0 | —（恰 0 实参；写实参报 ArgsNotNeeded） | 返回当前进程的工作集内存大小（WorkingSet64），单位为 byte；取当前进程 WorkingSet64（含共享页），与任务管理器口径接近；无失败路径 |
| `GETNUMB` | int | str, str | 恰好 2 | — | 按变量名与 csv 关键字取数值并返回；关键字为空或查不到返回 -1；第1参是变量名字符串而非变量本身，名字非变量抛 CodeEE(NotVariableName)；取不到多维 ERD；代码注明未使用未测试 |
| `GETSPCHARA` | int | int | 恰好 1 | — | 返回 CFLAG:0 非 0（SP 角色）且 NO 等于实参的角色在列表中的下标；无则 -1；需开启兼容性选项「使用SP角色」，默认关闭，调用必抛 CodeEE；与 GETCHARA 不同，没有第 2 实参 |
| `GETTEXTBOX` | str | （无实参） | 恰好 0 | —（恰 0 实参；写实参报 ArgsNotNeeded） | 返回当前输入框内的文本；没有输入内容时返回空字符串；直接读输入框控件的 Text，不改变其内容；返回空串与“读取失败”无法区分 |
| `HOTKEY_STATE` | int | int, int | 恰好 2 | — | 把热键状态数组第 index 项设为 value 并返回 0（即 HOTKEY.ERB 的 state[i]=v）；未先调 HOTKEY_STATE_INIT 抛非 CodeEE 的 Exception；index 越界抛 IndexOutOfRangeException |
| `HOTKEY_STATE_INIT` | int | int | 恰好 1 | — | 分配长度为 size 的热键状态数组（初值全 0）并返回 0；重复调用会重建数组、清空原有状态；需 exe 目录有 HOTKEY.ERB 且 Ctrl+D 启用热键才生效 |
| `MOVETEXTBOX` | int | int, int, int | 恰好 3 | —（3 个 int 全必填；省略报不足，留空报 CodeEE） | 预约把输入框移到 (xPos,yPos)、宽度设为 width，返回 1；下次 INPUT 时生效；三值都会被夹取（x≥0、不越上边、宽≥50）且截断成 int，越界不报错；画面回滚时暂回原位 |
| `RAND` | int | int, int? | 1–2 | 省略第2实参即 min=0；两槽显式留空都报 ArgCanNotBeNull | 返回 [min, max) 内的随机整数；单参 RAND(X) 等价 RAND(0, X)，即 min 取 0；RAND(0)/RAND:0 抛 CodeEE 但消息不同；max-min 溢出时抛 ArgumentOutOfRangeException；非2的幂取余有偏 |
| `RESUMETEXTBOX` | int | int, int, int | 恰好 3 | 3 个 int 必填（值全被忽略，写 0,0,0 即可）；留空报 CodeEE | 取消 MOVETEXTBOX 的位移预约，让输入框回到原本位置，返回 1；与 MOVETEXTBOX 同一实现类，固定 3 个 int 是检查器要求，运行期完全不读这三个值 |
| `SETTEXTBOX` | int | str | 恰好 1 | —（text 必填；传空串合法（清空），留空槽报 CodeEE） | 把输入框内容整体替换为 text，返回 1；text 为空串即清空输入框；直接改控件 Text（光标与滚动位置一并复位），不影响 MOVETEXTBOX 的位移预约 |
| `UNICODEBYTE` | int | str | 恰好 1 | — | 返回首字符的 Unicode 码位（小端 UTF-32 前 4 字节读作 int）；只取首字符，其余字节被丢弃；空串时 BitConverter 越界抛异常（非 CodeEE）；代理对按一个码位返回 |

## 显式留空（记号 `-`）语义一览

只列**允许显式留空**的注册名（`f(a,,c)` 这种写法能过检查器的那些槽）——这一维与「数量省略」正交，写脚本时最容易踩。空槽语义取自本表「空槽语义」列；有可留空槽却没写清语义的会在生成时报警。

| 注册名 | 可留空的槽 | 空槽语义 |
| --- | --- | --- |
| `ARRAYMSORTEX` | 3 | 第3参留空=顺序（同省略）；第4参留空报错，省略=扫到0/空串为止 |
| `CMATCH` | 3、4 | 留空与少写尾实参同义：start=0、end=CHARANUM |
| `CSVABL` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVBASE` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVCALLNAME` | 2 | 第 2 参留空或省略均取 0（表示非 SP 角色） |
| `CSVCFLAG` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVCSTR` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVEQUIP` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVEXP` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVJUEL` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVMARK` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVMASTERNAME` | 2 | 第 2 参留空或省略均取 0（表示非 SP 角色） |
| `CSVNAME` | 2 | 第 2 参留空或省略均取 0（表示非 SP 角色） |
| `CSVNICKNAME` | 2 | 第 2 参留空或省略均取 0（表示非 SP 角色） |
| `CSVRELATION` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `CSVTALENT` | 3 | 第 3 参留空或省略均取 0（表示非 SP 角色） |
| `DT_CELL_SET` | 4 | 留空/省略第 4 槽都写 DBNull；asId 省略按行号、留空报错 |
| `DT_COLUMN_NAMES` | 2 | 省略或留空第 2 实参都改写 RESULTS；表不存在返回 -1 |
| `DT_ROW_ADD` | 3 | 成对形式的值槽留空写 DBNull；列名槽与数组形式各槽留空报 CodeEE |
| `DT_SELECT` | 2、3 | 槽2、3 省略或留空＝不筛选/不排序；槽4 省略则写 RESULT:1 起 |
| `ENCODETOUNI` | 2 | 槽2 省略或显式留空都取 0（位置 0） |
| `EXISTCSV` | 2 | 槽 2（SP 标志）省略或留空均按 0（非 SP）处理 |
| `FINDCHARA` | 3、4 | 槽 3 省略/留空取 0（起点），槽 4 取 CHARANUM（终点） |
| `FINDELEMENT` | 3、4、5 | 槽3→0；槽4→数组长度；槽5→0（部分匹配） |
| `FINDLASTCHARA` | 3、4 | 槽 3 省略/留空取 0（起点），槽 4 取 CHARANUM（终点） |
| `FINDLASTELEMENT` | 3、4、5 | 槽3→0；槽4→数组长度；槽5→0（部分匹配） |
| `FIND_CHARADATA` | 1 | 槽1省略或留空均按 *（搜全部 chara_*.dat） |
| `GDRAWG` | 11 | 第11实参可省略或留空 = 不使用颜色矩阵 |
| `GETCHARA` | 2 | 第2实参可整省或显式留空（均按 0/false 处理）；第1实参不可空 |
| `GETMETH` | 2、3 | 省略/留空槽2=无兜底（未找到即抛CodeEE）；槽3起留空原样转发给目标 |
| `GETMETHS` | 2、3 | 省略/留空槽2=无兜底（未找到即抛CodeEE）；槽3起留空原样转发给目标 |
| `INRANGEARRAY` | 4、5 | 留空与少写尾实参同义：start=0、end=数组长度 |
| `INRANGECARRAY` | 4、5 | 留空与少写尾实参同义：start=0、end=CHARANUM |
| `MATCH` | 3、4 | 留空与少写尾实参同义：start=0、end=数组长度 |
| `MAXARRAY` | 2、3 | 留空与少写尾实参同义：start=0、end=数组长度 |
| `MAXCARRAY` | 2、3 | 留空与少写尾实参同义：start=0、end=CHARANUM |
| `MINARRAY` | 2、3 | 留空与少写尾实参同义：start=0、end=数组长度 |
| `MINCARRAY` | 2、3 | 留空与少写尾实参同义：start=0、end=CHARANUM |
| `MONEYSTR` | 2 | 第 2 实参省略或留空：不做格式转换，直接拼接数字与单位 |
| `STRFIND` | 3 | start 省略/留空取 0 |
| `STRFINDU` | 3 | start 省略/留空取 0 |
| `STRJOIN` | 2、3、4 | 槽2=“,”；槽3=0；槽4=余长（一直连到数组末尾） |
| `SUBSTRING` | 2、3 | start 省略/留空取 0；length 省略/留空取 -1（取到末尾） |
| `SUBSTRINGU` | 2、3 | start 省略/留空取 0；length 省略/留空取 -1（取到末尾） |
| `SUMARRAY` | 2、3 | 槽2=0；槽3=末维长度（省略与留空同义，均合计到末尾） |
| `SUMCARRAY` | 2、3 | 留空与少写尾实参同义：start=0、end=CHARANUM |
| `TOSTR` | 2 | 槽2 省略或留空 = i.ToString()，即无格式的十进制 |
| `VARSIZE` | 2 | 省略或留空均取 0，即最左维（EM+EE 选项下 1 亦为最左维） |
