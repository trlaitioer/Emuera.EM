"""arg-contract/issues/03 片 6 的机械预筛 + 裁定表生成器。

口径（与票内片 6 一致）：
  · 「两轴模型」= `ArgumentBuilder.checkArgumentType` 解释的两条声明轴：
      - 数量轴 `minArg`（未设 = -1 时，`arguments.Count < argumentTypeArray.Length` 报错，即整表必填）；
        另有 `argAny = true`（数量不限，超出部分取类型表末位）。
      - 槽位轴 `argumentTypeArray[i]`：`null!` = 该槽可省**且不做类型检查**（`continue` 跳过），
        `typeof(void)` = 必填、类型由 builder 自检，具体 Type = 必填且类型固定。
    不走 `checkArgumentType` 的 builder（自校验）不受这两轴支配，其逐槽结论引
    `tracker/nullable-migration/issues/15-omitted-args-null-consumers.md` 的决策表。
  · 「读取点」= builder 类体内对 `terms[i]`/`terms.Required(i)`/`args[...]`/`param[...]` 的字面量下标读取；
    `getChangeableVariable(terms, i, line)` 读的是 `terms[i-1]`，按下标 `i-1` 记账。
  · 「数量轴结论」由脚本机械判定：保证最少实参数 `min` 与最大**无守卫**读取下标比较
    （`Count > i` / `Count >= i+1` / `Count != 0` 一类守卫按出现即计，具体判定见人工裁定表）。
  · 人工裁定表 `RULINGS` 键为子类名；缺项/多项/与代码现状不符（如把走模型的类裁成自校验）即报警。
  · 对齐结论（与规则表 `OmitStart`/`ArgType.NotNull` 的关系）写在票内片 6，脚本只负责把
    「指令侧的编码形状」与「读取点」摆出来供复核。

用法：python3.14 tracker/arg-contract/instruction-args.py
产物：tracker/arg-contract/instruction-args.md（UTF-8 BOM，可逐字节复现）
"""

import re

from _scan import METHOD_SIG_RE_PLAIN, class_spans, method_marks, strip_keep_lines

SRC = r"Emuera/Runtime/Script/Statements/ArgumentBuilder.cs"
DST = r"tracker/arg-contract/instruction-args.md"

src = open(SRC, encoding="utf-8-sig").read()

# 与另两个脚本不同：本文件剥 `#region` 一类 `#` 指令行（口径差异见 _scan.py 头注释）
code = strip_keep_lines(src, strip_regions=True)


def create_argument_body(class_body):
    """类体内 `CreateArgument` 方法体的文本；找不到时抛错（避免静默漏判）。"""
    spans = method_marks(class_body, METHOD_SIG_RE_PLAIN)
    for idx, (pos, name) in enumerate(spans):
        if name != "CreateArgument":
            continue
        end = spans[idx + 1][0] if idx + 1 < len(spans) else len(class_body)
        return class_body[pos:end]
    raise AssertionError("未找到 CreateArgument 方法体")


builders = [(name, code[pos:end]) for name, pos, end in class_spans(code)]

# ── 逐 builder 提取声明与读取点 ──────────────────────────────────────────────────
READ_PATTERNS = [
    (re.compile(r"\bterms\s*\[\s*(\d+)\s*\]"), 1),
    (re.compile(r"\bterms\s*[.]Required\(\s*(\d+)\s*\)"), 1),
    (re.compile(r"\bargs\s*\[\s*(\d+)\s*\]"), 1),
    (re.compile(r"\bargs\s*[.]Required\(\s*(\d+)\s*\)"), 1),
    (re.compile(r"\bparam\s*\[\s*(\d+)\s*\]"), 1),
]
GETCHANGE_RE = re.compile(r"getChangeableVariable\(\s*terms\s*,\s*(\d+)\s*,")
VARIDX_RE = re.compile(r"\b(?:terms|args|param)\s*\[\s*(?!\d+\s*\])[^\]\s]+\s*\]")

rows = []
for name, body in builders:
    create = create_argument_body(body)
    arr = None
    m = re.search(r"argumentTypeArray\s*=\s*(\[[^\]]*\]|null!|types)\s*;", body)
    if m:
        arr = m.group(1)
    minarg = None
    m = re.search(r"^\s*minArg\s*=\s*([^;]+);", body, re.M)
    if m:
        minarg = m.group(1).strip()
    arg_any = bool(re.search(r"argAny\s*=\s*true", body))
    # 只看 CreateArgument:类体末尾的 #region 注释里也会出现 checkArgumentType,会误判
    uses_model = "checkArgumentType(" in create

    null_slots, void_slots, typed_slots = [], 0, 0
    if arr and arr.startswith("["):
        items = [x.strip() for x in arr[1:-1].split(",")]
        for i, it in enumerate(items):
            if it == "null!":
                null_slots.append(i)
            elif it == "typeof(void)":
                void_slots += 1
            else:
                typed_slots += 1

    reads = set()
    for pat, _ in READ_PATTERNS:
        reads |= {int(x) for x in pat.findall(create)}
    for x in GETCHANGE_RE.findall(create):
        if int(x) - 1 >= 0:
            reads.add(int(x) - 1)
    var_idx = bool(VARIDX_RE.search(create))

    # 保证最少实参数：minArg 已设则取之；未设且类型表存在则 = 数组长度（checkArgumentType 的第二条判据）
    if minarg is not None and minarg.lstrip("-").isdigit():
        ensure = int(minarg)
        if ensure < 0:
            ensure = len(null_slots) + void_slots + typed_slots if arr and arr.startswith("[") else None
    elif arr and arr.startswith("["):
        ensure = len(null_slots) + void_slots + typed_slots
    else:
        ensure = None

    if not uses_model:
        verdict = "—（自校验，两轴不生效）"
    elif ensure is None:
        verdict = "需人工核（无类型表与 minArg）"
    elif reads and max(reads) >= ensure:
        verdict = f"需人工核（读取下标 {max(reads)} ≥ 保证最少 {ensure}）"
    else:
        verdict = f"构造上安全（最小 {ensure} > 最大读取 {max(reads) if reads else '—'}）"

    rows.append({
        "name": name, "arr": arr, "minarg": minarg, "arg_any": arg_any, "model": uses_model,
        "null": null_slots, "void": void_slots, "typed": typed_slots, "reads": sorted(reads),
        "var_idx": var_idx, "ensure": ensure, "verdict": verdict,
    })

# ── 人工裁定表（片 6 逐 builder 核查结论；键=子类名） ─────────────────────────────
# 字段：类别 / 数量轴 / null 轴 / 依据
RULINGS = {
    # ── 走 checkArgumentType：受两轴模型支配（28） ────────────────────────────────
    "INT_EXPRESSION_ArgumentBuilder": (
        "走模型", "min 0；`Count == 0` 分支填入 `SingleLongTerm(0)`，`Required(0)` 只在 Count ≥ 1 时读",
        "类型位非空 ⇒ 槽 0 拒 null", "本片逐点核；issues/15 非目标「省略时以 `SingleLongTerm(0)` 填充」"),
    "INT_ANY_ArgumentBuilder": (
        "走模型", "min 0；`Count == 0` 早退/补默认值，`terms[0]` 只在 Count ≥ 1 时读",
        "`argAny` ⇒ 每个下标都取末位类型位（非空）⇒ 拒 null", "本片逐点核；issues/22 对 `ExpressionArrayArgument` 的说明"),
    "STR_EXPRESSION_ArgumentBuilder": (
        "走模型", "同 `INT_EXPRESSION`（`Count == 0` 分支填空串）",
        "类型位非空 ⇒ 槽 0 拒 null", "本片逐点核；issues/15 非目标"),
    "EXPRESSION_ArgumentBuilder": (
        "走模型", "min 0；`Count == 0` 早退，`terms[0]` 只在 Count ≥ 1 时读",
        "`typeof(void)` 是非空位 ⇒ 槽 0 拒 null", "本片逐点核；issues/15 非目标"),
    "SP_BAR_ArgumentBuilder": ("走模型", "min 3 > 最大读取 2 ⇒ 构造上安全", "三槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_SWAP_ArgumentBuilder": (
        "走模型", "min 1；`Count == 1` 时补成 `[terms[0], null]`，随后读 0/1 都在补齐后的表内",
        "槽 0 类型位非空 ⇒ 拒 null；槽 1 的 null 是本类**主动构造**的省略形，`SpSwapCharaArgument` 当前无指令引用、分支不可达",
        "本片逐点核；issues/15 非目标（`SP_COPYCHARA` 的可省略变体无指令引用）"),
    "SP_SAVEDATA_ArgumentBuilder": ("走模型", "min 2 > 最大读取 1 ⇒ 构造上安全", "两槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_TINPUT_ArgumentBuilder": (
        "走模型", "min 2；槽 2..5 各由 `Count > k` 守卫", "六槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_TINPUTS_ArgumentBuilder": (
        "走模型", "min 2；槽 2..5 各由 `Count > k` 守卫", "六槽类型位非空（槽 1 为 string）⇒ 拒 null", "本片逐点核"),
    "SP_FOR_NEXT_ArgumentBuilder": (
        "走模型", "min 3；`Required(2)` 落在保证区间内，槽 3 由 `Count > 3 && != null` 守卫",
        "槽 1 是**全库唯一 `null!` 位**，调用点判空后取默认 0（`start`）；其余槽类型位非空 ⇒ 拒 null",
        "本片逐点核；票面风险 8 的同类形状（可省槽被函数体先判空）"),
    "SP_POWER_ArgumentBuilder": ("走模型", "min 3 > 最大读取 2 ⇒ 构造上安全", "三槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_SWAPVAR_ArgumentBuilder": ("走模型", "min 2 > 最大读取 1 ⇒ 构造上安全", "`void` 位非空 ⇒ 拒 null", "本片逐点核"),
    "VAR_INT_ArgumentBuilder": (
        "走模型", "min 0，但 `Count == 0` 在 `checkArgumentType` 之前早退 ⇒ 读槽 0 时 Count ≥ 1",
        "类型位非空 ⇒ 槽 0 拒 null", "本片逐点核"),
    "VAR_STR_ArgumentBuilder": (
        "走模型", "min 0，但 `Count == 0` 早退（填 `RESULTS:0`）⇒ 读槽 0 时 Count ≥ 1",
        "类型位非空 ⇒ 槽 0 拒 null", "本片逐点核"),
    "BIT_ARG_ArgumentBuilder": (
        "走模型", "min 2；`getChangeableVariable(terms, 1)` 只读槽 0", "`argAny` ⇒ 尾部取末位类型位（long）⇒ 拒 null", "本片逐点核"),
    "SP_VAR_SET_ArgumentBuilder": (
        "走模型", "min 1；槽 1..3 各由 `Count > k` 守卫", "四槽类型位非空（前两槽 `void`）⇒ 拒 null", "本片逐点核"),
    "SP_CVAR_SET_ArgumentBuilder": (
        "走模型", "min 1；槽 1..4 各由 `Count > k` 守卫", "五槽类型位非空（前三槽 `void`）⇒ 拒 null", "本片逐点核"),
    "SP_BUTTON_ArgumentBuilder": ("走模型", "min 2 > 最大读取 1 ⇒ 构造上安全", "两槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_COLOR_ArgumentBuilder": (
        "走模型", "min 1；`Count == 2` 直接报错，`Count == 1` 只读槽 0，其余（≥3）才读 0..2",
        "三槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_SPLIT_ArgumentBuilder": (
        "走模型", "min 3；槽 3 由 `Count >= 4` 守卫", "类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_HTMLSPLIT_ArgumentBuilder": (
        "走模型", "min 1；槽 1/2 各由 `Count >= 2`/`>= 3` 守卫", "三槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_GETINT_ArgumentBuilder": (
        "走模型", "min 0，但 `Count == 0` 早退（填 `RESULT`）⇒ 读槽 0 时 Count ≥ 1",
        "类型位非空 ⇒ 槽 0 拒 null", "本片逐点核"),
    "SP_CONTROL_ARRAY_ArgumentBuilder": ("走模型", "min 3 > 最大读取 2 ⇒ 构造上安全", "三槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_SHIFT_ARRAY_ArgumentBuilder": (
        "走模型", "min 3；槽 3/4 各由 `Count >= 4`/`>= 5` 守卫", "类型位非空 ⇒ 拒 null", "本片逐点核"),
    "SP_SAVEVAR_ArgumentBuilder": (
        "走模型", "min 3；`Required(0)`/`Required(1)` 在保证区间内，尾部的 `getChangeableVariable(terms, i+1)` 由循环上界 `i < Count` 保证",
        "`argAny` ⇒ 尾部按末位类型位（long）判 null，且循环内另有 `terms[i] == null ⇒ CanNotOmitArg` 双保险",
        "本片逐点核"),
    "SP_SAVECHARA_ArgumentBuilder": (
        "走模型", "min 3；尾部只经循环上界 `i < Count` 读取", "`argAny` ⇒ 尾部按末位类型位（long）判 null；读取前有 `is SingleTerm` 前置",
        "本片逐点核"),
    "SP_COPY_ARRAY_Arguments": ("走模型", "min 2 > 最大读取 1 ⇒ 构造上安全", "两槽类型位非空 ⇒ 拒 null", "本片逐点核"),
    "Expressions_ArgumentBuilder": (
        "走模型", "数量轴 = 构造它的 `GetNormalArgumentBuilder(argstr, minArg)` 的 `minArg`（未设时 = `argstr.Length`）",
        "逐槽类型位（I/S，非空）⇒ 拒 null；`argAny == false` 时**超出类型表长度的位置不经判据**（只报 level 1 的 `TooManyArg`）——见 C 段边界",
        "本片逐点核；`ArgumentParser.GetNormalArgumentBuilder`"),
    # ── 自校验：两轴不生效，逐槽处置引 nullable-migration/issues/15 ────────────────
    "SP_PRINT_IMG_ArgumentBuilder": (
        "自校验", "自建 `popWords` 循环 + `argCount` 自检，两轴不生效",
        "只允许 `Nameb`/`Namem` 缺席（`continue`）；坐标槽位省略报 `CanNotOmitArg`",
        "issues/15 决策 7 + 决策记录（审查后收紧坐标槽位）"),
    "SP_PRINT_SHAPE_ArgumentBuilder": (
        "自校验", "自建循环 + `param.Count != 1 && != 4` 报 `DifferentArgsCount`",
        "循环内遇 null ⇒ `CanNotOmitArg(param.Count)`", "issues/15 决策 6"),
    "SP_HTML_PRINT_ArgumentBuilder": (
        "自校验", "自建 `PopArgumentPrimitive` 解析，两轴不生效",
        "第 2 实参 null ⇒ `SpHtmlPrint.Opt = null`（缺席即合法）；首实参已判空",
        "issues/15 决策 8 + 非目标（首实参已判空）"),
    "SP_DT_COLUMN_OPTIONS_ArgumentBuilder": (
        "自校验", "自建词法循环解析关键字/值，不读实参表下标", "关键字/值逐项自检（缺列报错）",
        "本片逐点核（缺列报错见 `Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction`）"),
    "SP_PRINTV_ArgumentBuilder": (
        "自校验", "自建解析 + `Required(` 前判空", "显式判空（首实参）", "issues/15 非目标「已显式判空、归宿已落实」"),
    "SP_TIMES_ArgumentBuilder": ("自校验", "自建解析（`popWords` + FORM 串），不读实参表下标", "无实参表 null 可进入", "本片逐点核"),
    "FORM_STR_ANY_ArgumentBuilder": ("自校验", "自建非空 term（空则报错），不读实参表下标", "构造 `ExpressionArrayArgument` 的两条路径之一，自建 term 非空", "issues/22 对 `ExpressionArrayArgument` 构造路径的说明"),
    "VOID_ArgumentBuilder": ("自校验", "自建解析，不读实参表下标", "无实参表 null 可进入", "本片逐点核"),
    "STR_ArgumentBuilder": ("自校验", "自建解析（含颜色名特判），不读实参表下标", "无实参表 null 可进入", "本片逐点核"),
    "FORM_STR_ArgumentBuilder": ("自校验", "`ExpressionParser.ToStrFormTerm` 全函数，不读实参表下标", "无实参表 null 可进入", "issues/15 非目标"),
    "SP_VAR_ArgumentBuilder": (
        "自校验", "自建解析，不读实参表下标（A 表读取点为空）", "对自建 term 判空", "本片逐点核"),
    "SP_SORTCHARA_ArgumentBuilder": (
        "自校验", "自建 `popWords` 解析，不读实参表下标", "对自建 term/变量 token 判空", "本片逐点核"),
    "SP_SORT_ARRAY_ArgumentBuilder": (
        "自校验", "自建 `popWords` 解析，不读实参表下标", "对自建 term/变量 token 判空", "本片逐点核"),
    "SP_CALL_ArgumentBuilder": (
        "自校验", "自建解析（`${}` 形式），两轴不生效",
        "`subNames` 的 `Required(i)` **元素不可能为 null**（`[...]` 形式整体被拒，D1）；CALL 一族 `SubNames` 无其它读取点",
        "issues/15 决策 4/5 + D1"),
    "SP_CALLSHARP_ArgumentBuilder": (
        "自校验", "自建解析（`${}` 形式），两轴不生效",
        "`args[i] == null ⇒ CanNotOmitArg(i + 1)`；`subNames` 同 `SP_CALL`（不可达）",
        "issues/15 决策 3/4"),
    "CASE_ArgumentBuilder": ("自校验", "自建 `popWords` 解析，不读实参表下标", "无实参表 null 可进入", "本片逐点核"),
    "SP_SET_ArgumentBuilder": (
        "自校验", "自建解析（赋值左值与右值分开取），两轴不生效", "自带显式判空", "issues/15 影响段（`SP_SET`/`SP_SETS` 自带判空）"),
    "METHOD_ArgumentBuilder": (
        "自校验", "另一套判据：`line.Function.Method.CheckArgumentType`（`arguments` 列表元素可空，由该方法自判）",
        "同上（`FunctionMethod` 侧判据）", "本片逐点核；票面「唯一 override 检查的类」即此路径"),
    "SP_INPUTS_ArgumentBuilder": (
        "自校验", "自建 `ReduceArguments` + `terms.Count > 0` 守卫；两轴不生效",
        "`terms[0] == null` 判空后按「非整数 ⇒ 忽略该实参」处理；`INPUTS \"d\", , 1` 静默丢弃 `CanSkip` 是既有行为",
        "issues/15 非目标 + 本片逐点核"),
    "SP_REF_ArgumentBuilder": (
        "自校验", "自建 `popWords` 解析，两轴不生效", "ref 分支自解析起不可达（死代码）", "arg-contract/issues/03「UDF 族」"),
    "SP_INPUT_ArgumentBuilder": (
        "自校验", "自建 `popTerms` + 循环，`Count > k` 守卫",
        "`CanSkip != null` ⇒ 槽 1/2 必填（`CanNotOmitArg`）；第 4 个及以后实参被静默丢弃（D3）",
        "issues/15 决策 2 + D3"),
    "STR_DOUBLE_ArgumentBuilder": (
        "自校验", "自建 `PopArgumentPrimitive` + `ReadDouble`，不读实参表下标", "无实参表 null 可进入", "本片逐点核"),
}

# 对齐结论（片 6 的定案正文；写进产物 C 段，同一段落也抄进票内片 6）
ALIGNMENT = """
两侧是**同一问题的两套编码，极性相反**——指令侧实现的正是函数侧在 arg-contract/issues/03 片 3 被否决的「选项 A」（默认拒绝、显式声明才放行）：

| 函数侧（`ArgTypeList`） | 指令侧（`argumentTypeArray` + `minArg`） |
| --- | --- |
| `OmitStart = k`：最少传 k 个 | `minArg = k`；未设（-1）= 全部必填（判据表长度） |
| 无 `OmitStart`：全槽必填 | `minArg` 未设 |
| `ArgType.Variadic*`：变长尾 | `argAny = true`：尾部取末位类型位 |
| `i >= OmitStart` 的槽**默认放行** null | 仅类型位 `null!` 才放行 null；其余类型位（含 `typeof(void)`）一律 `CanNotRecognizeArg` 拒绝 |
| `ArgType.NotNull`：逐槽拒绝 null（放行的例外） | 无需此标志：拒绝是默认 |
| 逐槽 `ArgTypes`（标志位按位或） | 逐槽 `Type`（`typeof(void)` = 类型由 builder 自检） |
| 显式空槽 = null 元素，由 `FunctionMethod.CheckArgumentTypeEx` 判 | 显式空槽 = null 元素，由 `ArgumentBuilder.checkArgumentType` 判 |

**定案：不对齐、不改极性、不引入新标志位。** 依据：

1. 函数侧取 B（保持默认放行）的理由是「翻转会误拒现存脚本，而罚则是 `CodeEE` 硬错误、没有警告级过渡」。指令侧不存在这个前提——50 个 builder 里只有 1 个槽位声明为「可省**且**不检查类型」（`SP_FOR_NEXT` 槽 1），其余槽位要么由 `checkArgumentType` 按「已声明必填」拒绝 null，要么由自校验 builder 在解析期三选一处置（代入默认值 / `CanNotOmitArg` / 缺席即合法，issues/15 定案）。也就是说指令侧**早已是严格侧**，没有「默认放行 + 靠枚举 96 个槽位兜底」这种脆弱结构。
2. 两侧的声明语法、解释者与失败文案（`CanNotRecognizeArg` vs `ArgCanNotBeNull`）、失败时机都不同，名词统一只带来改名成本与行为风险，不增加安全性。映射关系记在此处，作为 arg-contract/issues/05 设计槽位类型时的输入（若将来真要统一，A 极性在指令侧已是既成事实）。
3. **边界（本片不裁）**：`argAny == false` 的 builder 收到多于类型表长度的实参时，`checkArgumentType` 只报 **level 1 的 `TooManyArg`（警告，非错误）** 并把判据循环截断到类型表长度，故**超出声明长度的位置不经 null 判据**（`Restructure` 仍会折叠它们）。该位置是否被消费点读取属指令处理器（`Process.ScriptProc` / `Instraction.Child`）一侧；issues/15 决策 10 只点了若干具名类。本片不据此改行为，记为待核项。
"""

model_rows = [r for r in rows if r["model"]]
self_rows = [r for r in rows if not r["model"]]
need_manual = [r for r in rows if r["verdict"].startswith("需人工核")]

missing = [r["name"] for r in rows if r["name"] not in RULINGS]
extra = [k for k in RULINGS if k not in {r["name"] for r in rows}]
if missing:
    print("WARN 人工裁定表缺项：", missing)
if extra:
    print("WARN 人工裁定表多余项：", extra)
for r in rows:
    ruling = RULINGS.get(r["name"])
    if ruling and r["model"] and ruling[0] == "自校验":
        print("WARN 判定与代码不符（走模型却裁成自校验）：", r["name"])
    if ruling and not r["model"] and ruling[0] == "走模型":
        print("WARN 判定与代码不符（自校验却裁成走模型）：", r["name"])

out = []
out.append("# arg-contract/issues/03 片 6：指令侧实参契约（`ArgumentBuilder`）机械预筛 + 裁定")
out.append("")
out.append("本文件由 [instruction-args.py](instruction-args.py) 生成，口径见脚本头注释；票见 "
           "[arg-contract/issues/03](issues/03-required-on-omittable-slot.md)。")
out.append("")
out.append("## 计数")
out.append("")
out.append("| 量 | 数 |")
out.append("| --- | --- |")
out.append(f"| `ArgumentBuilder` 子类 | {len(rows)} |")
out.append(f"| └ 走 `checkArgumentType`（受两轴模型支配） | {len(model_rows)} |")
out.append(f"| └ 自校验（不读 `minArg`/类型表） | {len(self_rows)} |")
out.append(f"| 类型位 `null!`（该槽可省且不检查类型） | {sum(len(r['null']) for r in rows)} |")
out.append(f"| 类型位 `typeof(void)`（必填、类型自检） | {sum(r['void'] for r in rows)} |")
out.append(f"| 数量轴需人工核的 builder | {len(need_manual)} |")
out.append("")
out.append("## A. 两轴编码与读取点（机械）")
out.append("")
out.append("`保证最少实参数` = `minArg`（已设）或类型表长度（`minArg` 未设时 `checkArgumentType` 的"
           "第二条判据 `arguments.Count < argumentTypeArray.Length && minArg < 0`）；自校验的 builder 记 `—`。")
out.append("")
out.append("`读取点` = 类体内字面量下标读取（`terms[i]`/`terms.Required(i)`/`getChangeableVariable(terms,i)` 记 `i-1`）；"
           "`变量下标` 表示存在非字面量下标读取。")
out.append("")
out.append("`机械判据` 只做粗筛：读取下标 `>= 保证最少实参数` 时标「需人工核」，其结论在 B 段（守卫形态多种多样，"
           "机械判不出 `Count > i` 一类守卫），**不代表缺陷**。")
out.append("")
out.append("| 子类 | 保证最少实参数 | `argAny` | `null!` 槽 | `void` 位 | 类型位 | 走模型 | 读取点 | 变量下标 | 机械判据 |")
out.append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
for r in rows:
    nulls = "、".join(str(i) for i in r["null"]) or "—"
    out.append(f"| `{r['name']}` | {r['ensure'] if r['ensure'] is not None else '—'} | {'是' if r['arg_any'] else '—'} | {nulls} | "
               f"{r['void']} | {r['typed']} | {'是' if r['model'] else '—'} | "
               f"{'、'.join(str(i) for i in r['reads']) or '—'} | {'是' if r['var_idx'] else '—'} | {r['verdict']} |")
out.append("")
out.append("## B. 人工裁定")
out.append("")
out.append("A 段是机械预筛，本段是逐 builder 的裁定（片 6）。键为子类名，脚本校验其与代码现状一致"
           "（走模型/自校验分类、缺项与多项）；`数量轴` 一列即 A 段「需人工核」那些行的结论。")
out.append("")
out.append("| 子类 | 类别 | 数量轴 | null 轴 | 依据 |")
out.append("| --- | --- | --- | --- | --- |")
for r in rows:
    ruling = RULINGS.get(r["name"], ("（未裁定）", "—", "—", "—"))
    out.append(f"| `{r['name']}` | {ruling[0]} | {ruling[1]} | {ruling[2]} | {ruling[3]} |")
out.append("")
out.append("## C. 与规则表 `OmitStart` / `ArgType.NotNull` 的对齐结论")
out.append("")
out.append(ALIGNMENT.strip())
open(DST, "w", encoding="utf-8-sig", newline="\n").write("\n".join(out) + "\n")
print("builders", len(rows), "model", len(model_rows), "self", len(self_rows), "need_manual", len(need_manual))
