"""arg-contract/issues/04 阶段 A 片 0 的消费面重新基线生成器。

口径：
  · 扫描范围 = `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs`（**规则表族**：183 个子类
    经 `FunctionMethod.CheckArgumentTypeEx` 校验）。另三族绕过规则表，不在本表：运算符族在
    `OperatorMethod.cs`（`CheckArgumentType` 抛 ExeEE）、StrForm 族在 `StrForm.cs`、TOSTR 包装在
    `Process.CalledFunction.cs`（都由调用点担保）。
  · live = 剥注释后的文本（沿用 slots-worklist.py 的朴素逐行剥离；本文件无逐字字符串/原始字符串，
    3 处插值字符串不含 `arguments`）。「含注释」= 原始文本。
  · 「可空」= 存在一个规则段满足 `OmitStart >= 0` 且 `OmitStart <= i < ArgTypes.Count` 且该槽位
    **未**标 `ArgType.NotNull`——检查器「任一段通过即通过」，只要有一段落空即可。`OmitStart == 0`
    且块内含 `SameAsFirst` 的 0 号槽按 `CheckArgumentTypeEx` 的 slot0Required 特例判为不可空。
  · 「可能缺失」= 存在一个规则段，其最小实参数（`OmitStart < 0` 时为 `ArgTypes.Count`）<= i：数量省略
    即可让列表短于 `i + 1`，读 `arguments[i]` 就是越界。这是与 null 正交的另一把尺子，机械只给候选，
    「读该槽时是否保证 `Count >= i + 1`」须逐点人工核（票内片 1 第 2 条）。
  · 读取形态按 live 文本的字面量下标 + helper 偏移统计（helper 偏移由 helper 定义体内的
    `Required(argNo)` / `Required(argNo + k)` 机械推出，改 helper 即自动跟随）。非字面量下标、
    整表转发、`Count` 循环等无法归到具体槽位的形态单列在 D 段。
  · 可达 null 候选 = 可空 ∧ 读取形态含硬解引用（`Int`/`Str`/`IsInt`/`IsStr`/`as Var`/`cast Var`/
    `term`/`index`/helper）∧ 类内该槽无判空写法。有判空写法的槽单列在 B 段（判空未必守住解引用，
    须人工核；issues/03 的 B 表 34 项即此类）。
  · 裁定表 `RULINGS` 是人写的处置列（键 = (类, 槽)），机械预筛给不出「这个 null 是什么意思」。
    候选集与裁定表不一致时脚本报警（缺裁定 = WARN 未裁定；裁定引用了不再出现的槽 = WARN 过期）。
    已带 `ArgType.NotNull` 的槽由脚本机械判定，不进裁定表。4 个硬转型槽的裁定取自跨脚本单一来源
    `_rulings.py`（slots-worklist.py 的 E 段引用同一条），避免两边文案各自漂移。
  · 自校验（复跑即报警，逐项见产物 E 段）：本文件 live `arguments.Required(` = 404；`HasUniqueRestructure = true;`
    语句 17 / 类 13；`UniqueRestructure` override 14；全仓 `[.. Arguments]` 1；候选集与裁定表一致；
    以及**与 [slots-worklist.py](slots-worklist.py) 同函数同口径**的 `ArgType.NotNull` 计数
    （含注释 87 处 / 76 行，剥注释 81 处 / 70 行；计数函数在 `_scan.not_null_counts`，预期值在
    `_scan.NOT_NULL_EXPECTED`——两处同源，改 `Creator.Method.cs` 时同步这两处即可同时约束两个产物）。
用法：python3.14 .scratch/arg-contract/arg-slots.py
产物：.scratch/arg-contract/slots-consumption.md（UTF-8 BOM，与本脚本写出编码一致，可逐字节复现）
"""

import os
import re

from _rulings import consumption_rulings
from _scan import (NOT_NULL_EXPECTED, class_spans, methods_of, not_null_counts,
                   null_handling_points, parse_blocks, strip_keep_lines)

SRC = r"Emuera/Runtime/Script/Statements/Function/Creator.Method.cs"
DST = r".scratch/arg-contract/slots-consumption.md"
REPO = r"Emuera"

src = open(SRC, encoding="utf-8-sig").read()

code = strip_keep_lines(src)
lines = src.split("\n")

# ── 类 ────────────────────────────────────────────────────────────────────────────
classes = class_spans(src)
span_of_class = {name: (pos, end) for name, pos, end in classes}


# ── 规则段 ────────────────────────────────────────────────────────────────────────


blocks_by_class = {}
all_omittable = set()
for name, pos, end in classes:
    blocks = parse_blocks(src, pos, end)
    blocks_by_class[name] = blocks
    for b in blocks:
        if b["om"] >= 0 and b["argc"] > b["om"]:
            for i in range(b["om"], b["argc"]):
                all_omittable.add((name, i))


def variadic_entries(b):
    """规则段尾部连续的 Variadic 位（`ArgTypeList.LastVariadics` 的等价物）。"""
    vs = []
    for e in reversed(b["entries"]):
        if "Variadic" in e:
            vs.append(e)
        else:
            break
    return list(reversed(vs))


def min_count(b):
    return b["om"] if b["om"] >= 0 else b["argc"]


def rule_at(b, i):
    """槽 i 落在该段的哪条规则上（变长尾按下标取模，与 CheckArgumentTypeEx 同式）。"""
    if i < b["argc"]:
        return b["entries"][i]
    vs = variadic_entries(b)
    if not vs:
        return None
    return vs[(i - b["argc"]) % len(vs)]


def may_be_empty(cls, i):
    """检查器是否会放行槽 i 的显式空槽（null 元素）。"""
    for b in blocks_by_class.get(cls, []):
        rule = rule_at(b, i)
        if rule is None or "NotNull" in rule:
            continue
        if b["om"] >= 0 and b["om"] <= i < b["argc"]:
            if "SameAsFirst" in " ".join(b["entries"]) and b["om"] == 0 and i == 0:
                continue  # slot0Required 特例
            return True
    return False


def may_be_missing(cls, i):
    return any(min_count(b) <= i for b in blocks_by_class.get(cls, []))


def flagged(cls, i):
    segs = [b for b in blocks_by_class.get(cls, []) if b["om"] >= 0 and b["om"] <= i < b["argc"]]
    if not segs:
        return None
    return all("NotNull" in (b["entries"][i] if i < b["argc"] else "") for b in segs)


# ── helper 偏移（从定义体机械推出）────────────────────────────────────────────────
helpers = {}  # name -> sorted offsets
_helper_def = re.compile(
    r"private static [\w<>\[\],?.]+ (\w+)\(string Name, ExpressionMediator exm, List<AExpression\?> arguments, int argNo\)"
)
for m in _helper_def.finditer(src):
    i = src.find("{", m.end())
    d, j = 0, i
    while j < len(src):
        if src[j] == "{":
            d += 1
        elif src[j] == "}":
            d -= 1
            if d == 0:
                break
        j += 1
    body = code[i:j]
    offs = set()
    for mm in re.finditer(r"arguments\.Required\(argNo\)", body):
        offs.add(0)
    for mm in re.finditer(r"arguments\.Required\(argNo\s*\+\s*(\d+)\)", body):
        offs.add(int(mm.group(1)))
    helpers[m.group(1)] = sorted(offs)

# ── 读取形态 ─────────────────────────────────────────────────────────────────────
DEREF = {"Int", "Str", "IsInt", "IsStr", "as Var", "cast Var", "term", "index"}
FORM_ORDER = ["Int", "Str", "IsInt", "IsStr", "as Var", "cast Var", "is Var", "is Single",
              "判空", "term", "index", "折叠"]


def read_forms(cls, slot):
    pos, end = span_of_class[cls]
    t = code[pos:end]
    out = []
    for m in re.finditer(rf"arguments\.Required\(\s*{slot}\s*\)", t):
        tail = t[m.end():m.end() + 32]
        before = t[max(0, m.start() - 16):m.start()]
        if tail.startswith(".GetIntValue("):
            out.append("Int")
        elif tail.startswith(".GetStrValue("):
            out.append("Str")
        elif tail.startswith(".IsInteger"):
            out.append("IsInt")
        elif tail.startswith(".IsString"):
            out.append("IsStr")
        elif tail.startswith(".Restructure("):
            out.append("折叠")
        elif "as VariableTerm" in tail[:24]:
            out.append("as Var")
        elif "(VariableTerm)" in before or "(VariableTerm)" in tail[:16]:
            out.append("cast Var")
        else:
            out.append("term")
    for m in re.finditer(rf"arguments\[\s*{slot}\s*\]", t):
        after = t[m.end():m.end() + 28]
        before = t[max(0, m.start() - 16):m.start()]
        if "as VariableTerm" in after[:20]:
            out.append("as Var")
        elif "(VariableTerm)" in before:
            out.append("cast Var")
        elif "is VariableTerm" in after[:20]:
            out.append("is Var")
        elif re.match(r"\s*(?:==|!=)\s*null", after) or re.match(r"\s+is\s+(?:not\s+)?null", after) or after.startswith("?."):
            out.append("判空")
        elif "is SingleTerm" in after[:24]:
            out.append("is Single")
        else:
            out.append("index")
    for m in re.finditer(r"(\w+)\(Name, exm, arguments, (\d+)\)", t):
        h, base = m.group(1), int(m.group(2))
        if h in helpers:
            for off in helpers[h]:
                if base + off == slot:
                    out.append(f"helper")
    return [f for f in FORM_ORDER if f in out] + sorted({o for o in out if o.startswith("helper")})


def null_guard_points(cls, slot):
    """类内针对槽 slot 的判空点（文件内绝对字符位置）；写法见 `_scan.null_handling_points`。"""
    pos, end = span_of_class[cls]
    return null_handling_points(code[pos:end], pos, slot)


# ── 逐 (类,槽) 汇总：可省略槽全集 + 有读取形态的槽 ────────────────────────────────────
literal_slots = {}   # cls -> {slot: [abs positions]}
nonliteral = {}      # cls -> dict of class-level forms
for name, pos, end in classes:
    t = code[pos:end]
    d = {}
    for m in re.finditer(r"arguments\.Required\(\s*(\d+)\s*\)", t):
        d.setdefault(int(m.group(1)), []).append(pos + m.start())
    for m in re.finditer(r"arguments\[\s*(\d+)\s*\]", t):
        d.setdefault(int(m.group(1)), []).append(pos + m.start())
    for m in re.finditer(r"(\w+)\(Name, exm, arguments, (\d+)\)", t):
        h, base = m.group(1), int(m.group(2))
        if h in helpers:
            for off in helpers[h]:
                d.setdefault(base + off, []).append(pos + m.start())
    literal_slots[name] = d
    nl = {}
    n_req = len(re.findall(r"arguments\.Required\((?!\s*\d)", t))
    if n_req:
        nl["变长下标 `Required(i)`"] = n_req
    if "arguments.Skip(" in t:
        nl["整段转发 `Skip(k)`"] = t.count("arguments.Skip(")
    if ".Cast<" in t:
        nl["`Cast<>`"] = t.count(".Cast<")
    if re.search(r"foreach \([^)]* in arguments\b", t):
        nl["`foreach (… in arguments)`"] = len(re.findall(r"foreach \([^)]* in arguments\b", t))
    n_loop = len(re.findall(r"for \([^)]*arguments\.Count[^)]*\)", t))
    if n_loop:
        nl["`for(… arguments.Count …)`"] = n_loop
    n_cnt = len(re.findall(r"arguments\.Count", t))
    if n_cnt:
        nl["`arguments.Count`"] = n_cnt
    n_lp = len(re.findall(r"arguments is \[", t))
    if n_lp:
        nl["列表模式 `arguments is […]`"] = n_lp
    n_fold = len(re.findall(r"\.Restructure\(exm\)", t))
    if n_fold:
        nl["折叠站点 `.Restructure(exm)`"] = n_fold
    nonliteral[name] = nl

slots_of_interest = sorted(set(all_omittable) | {
    (cls, s) for cls, d in literal_slots.items() for s in d
})

rows = {}
for cls, s in slots_of_interest:
    forms = read_forms(cls, s)
    empty = may_be_empty(cls, s)
    missing = may_be_missing(cls, s)
    guard = null_guard_points(cls, s)
    rows[(cls, s)] = {
        "cls": cls, "slot": s, "forms": forms, "empty": empty, "missing": missing,
        "guard": guard, "flag": flagged(cls, s), "in_omittable": (cls, s) in all_omittable,
        "positions": literal_slots.get(cls, {}).get(s, []),
    }

# ── 裁定表（人工；键 = (类, 槽)）────────────────────────────────────────────────────
# 列：[类别, 处置, 数量省略尺子]；只填机械判不出语义的行（可空 ∧ 有硬解引用且未标 NotNull）。
# 已标 NotNull 的槽由脚本判定，不进本表。
# 4 个硬转型槽的裁定取自跨脚本单一来源 `_rulings.py`（slots-worklist.py 的 E 段引用同一条）。
RULINGS = {
    **consumption_rulings(),
    # 以下两项是机械预筛的假阳性，均已在 issues/03 裁定为「有意可空」，本片不改：
    ("DataTableCellSetMethod", 3): ("有意可空", "假阳性：`AExpression? v = arguments.Count > 3 ? arguments[3] : null` 把 null 原样交给 DBNull 分支（issues/03 片 2-D 已补注释 + 用例）", "过（`Count > 3` 才读）"),
    ("DataTableColumnManagementMethod", 1): ("有意可空", "假阳性（跨构造器）：`Required(1).GetStrValue` 在 `op == Names` 早退之后，属 Check/Create/Remove 的定长规则；Names 段只读 `arguments[1] is VariableTerm`，null = 写 RESULTS（issues/03 片 2-A 已裁定不标 + 放行用例）", "过（Names 段 `Count > 1`；其余段无 `OmitStart`）"),
}
CATEGORIES = ["硬转型解引用", "无意且可达", "有意可空", "转发槽", "消费型变长尾"]

cand = {}     # A 段：可空 ∧ 无判空 ∧ 有硬解引用 ⇒ 需本片处置的候选
guarded = {}  # B 段：可空 ∧ 类内该槽有判空写法
unseen = {}   # A2 段：可空 ∧ 无判空 ∧ 机械未见硬解引用（读取走非字面量/整表形态，或函数体不读）
for key, r in rows.items():
    if not (r["empty"] and not r["flag"]):
        continue
    deref = [f for f in r["forms"] if f in DEREF or f.startswith("helper") or f == "折叠"]
    if r["guard"]:
        guarded[key] = r
    elif deref:
        cand[key] = r
    else:
        unseen[key] = r

# A2 段的裁定（issues/03 片 2-C/2-D 已落地的结论；机械给不出「这个 null 交给谁解释」）
RULINGS_A2 = {
    ("DataTableRowSetMethod", 2): ("有意可空", "变长对 value 位 = 写 DBNull（规则段注释 + 用例）"),
    ("GetMethMethod", 2): ("转发槽", "`arguments.Skip(2)` 交给下游，null 由被调方解释"),
    ("GetMethsMethod", 2): ("转发槽", "同上"),
}

unruled = sorted(k for k in cand if k not in RULINGS)
# 「已落地」= 裁定表里那些现在已带上 ArgType.NotNull 的槽（本片 1 的产物）；既不待办也不已落地才是真过期。
done = {k: rows[k] for k in RULINGS if k not in cand and k in rows and rows[k]["flag"]}
stale = sorted(k for k in RULINGS if k not in cand and k not in done)
for k in unruled:
    print("WARN 候选未裁定：", k)
for k in stale:
    print("WARN 裁定表引用了已消失的槽（类改名 / 不再可省略）：", k)
unruled2 = sorted(k for k in unseen if k not in RULINGS_A2)
stale2 = sorted(k for k in RULINGS_A2 if k not in unseen)
for k in unruled2:
    print("WARN A2 段未裁定：", k)
for k in stale2:
    print("WARN A2 段裁定表引用了不再出现的槽：", k)

summary = {c: sum(1 for k in set(cand) | set(done) if RULINGS.get(k, ("",))[0] == c) for c in CATEGORIES}

# ── 自校验与计数 ──────────────────────────────────────────────────────────────────
def live_count(pattern, text):
    return len(re.findall(pattern, text))


has_unique_stmts = len(re.findall(r"HasUniqueRestructure = true;", code))
has_unique_classes = sum(1 for name, pos, end in classes if "HasUniqueRestructure = true;" in code[pos:end])
unique_overrides = len(re.findall(r"public override bool UniqueRestructure\(", code))
copy_arguments = 0
for root, _dirs, files in os.walk(REPO):
    for fn in files:
        if fn.endswith(".cs"):
            try:
                txt = open(os.path.join(root, fn), encoding="utf-8-sig").read()
            except UnicodeDecodeError:
                continue
            copy_arguments += live_count(r"\.\. Arguments\]", strip_keep_lines(txt))

repo_live = 0
for root, _dirs, files in os.walk(REPO):
    for fn in files:
        if fn.endswith(".cs"):
            p = os.path.join(root, fn)
            try:
                txt = open(p, encoding="utf-8-sig").read()
            except UnicodeDecodeError:
                continue
            repo_live += live_count(r"arguments\.Required\(", strip_keep_lines(txt))

raw_required = live_count(r"arguments\.Required\(", src)
live_required = live_count(r"arguments\.Required\(", code)
live_literal_required = live_count(r"arguments\.Required\(\s*\d+\s*\)", code)
live_literal_index = live_count(r"arguments\[\s*\d+\s*\]", code)
live_var_index = live_count(r"arguments\[\s*[A-Za-z_]\w*\s*\]", code)
live_count_expr = live_count(r"arguments\.Count", code)
live_as_var = live_count(r"as VariableTerm", code)
live_paren_var = live_count(r"\(VariableTerm\)", code)
live_is_var = live_count(r"is VariableTerm", code)
live_fold = live_count(r"\.Restructure\(exm\)", code)
live_list_pattern = live_count(r"arguments is \[", code)
live_skip = live_count(r"arguments\.Skip\(", code)
live_cast_loop = live_count(r"arguments\.Cast<", code)

# 与 slots-worklist.py 同函数同口径地数 `ArgType.NotNull`（issues/04 承诺的自校验项）
nn = not_null_counts(src)
nn_actual = (nn["raw_occ"], nn["raw_lines"], nn["live_occ"], nn["live_lines"])
nn_ok = nn_actual == NOT_NULL_EXPECTED

checks = []
if live_required != 404:
    checks.append(f"本文件 live `arguments.Required(` = {live_required}，预期 404（口径变更时同步票面）")
if repo_live < live_required:
    checks.append("全仓 live 计数小于本文件，扫描口径有误")
if has_unique_stmts != 17:
    checks.append(f"`HasUniqueRestructure = true;` 语句数 = {has_unique_stmts}，预期 17")
if has_unique_classes != 13:
    checks.append(f"带该标志的类数 = {has_unique_classes}，预期 13")
if unique_overrides != 14:
    checks.append(f"`UniqueRestructure` override 数 = {unique_overrides}，预期 14")
if copy_arguments != 1:
    checks.append(f"全仓 `[.. Arguments]` = {copy_arguments}，预期 1（FunctionMethodTerm.cs 的 unique 分支）")
if not nn_ok:
    checks.append(f"`ArgType.NotNull` 计数（含注释 处/行、剥注释 处/行）= {nn_actual}，预期 {NOT_NULL_EXPECTED}"
                  "（与 slots-worklist.py 同函数同口径）")

unflagged_need = sorted(k for k, r in rows.items()
                        if r["in_omittable"] and r["empty"] and not r["flag"] and not r["guard"])

# ── issues/18 的不变量：规则含 `AllowConstRef` 的引用位 ⇒ 该类必须设 HasUniqueRestructure ─────────
# 理由：常量数组实参会被 `VariableTerm.Restructure` 折成 `SingleTerm`，函数体的 `(VariableTerm)` 转换随后失效；
# 该类的 `UniqueRestructure`（收到的是 `[.. Arguments]` 副本）是仓库既有的规避手法。
allow_const = []
for name, pos, end in classes:
    for b in blocks_by_class[name]:
        for i, e in enumerate(b["entries"]):
            if "AllowConstRef" in e and "Ref" in e:
                allow_const.append((name, i, b["om"], b["argc"]))
unique_classes = {name for name, pos, end in classes if "HasUniqueRestructure = true;" in code[pos:end]}
# 人工豁免（逐槽）：`CONST` 与 `CHARADATA` 互斥，故角色数据段上的常量数组实参不可能存在，
# 折叠路径不可达 ⇒ 该槽不受不变量约束（nullable-migration/issues/18 的「修法约束」已核过此处）。
INVARIANT_EXEMPT = {
    ("SumArrayMethod", 0): "角色数据段（`CharacterData | RefIntArray | AllowConstRef`）：常量数组不可能是角色数组变量",
}
invariant_rows = []
for name, i, om, argc in allow_const:
    exempt = INVARIANT_EXEMPT.get((name, i))
    invariant_rows.append((name, i, om, argc, name in unique_classes, exempt))
invariant_violations = sorted({(n, i) for n, i, _om, _argc, ok, ex in invariant_rows if not ok and not ex})

if checks:
    for c in checks:
        print("WARN 自校验：", c)
else:
    print("自校验通过")

# ── 产物 ─────────────────────────────────────────────────────────────────────────
out = []
out.append("# arg-contract/issues/04 阶段 A 片 0：实参消费面重新基线")
out.append("")
out.append("本文件由 [arg-slots.py](arg-slots.py) 生成，口径见脚本头注释；票见 [arg-contract/issues/04](issues/04-argument-slots.md)。")
out.append("")
out.append("扫描范围是**规则表族**（`Creator.Method.cs` 的 183 个子类，经 `FunctionMethod.CheckArgumentTypeEx` 校验）。")
out.append("运算符族（`OperatorMethod.cs`）、StrForm 族（`StrForm.cs`）、TOSTR 包装（`Process.CalledFunction.cs`）")
out.append("都绕过规则表、由调用点担保，不在本表。")
out.append("")
out.append("## 计数（live = 剥注释；含注释 = 原始文本）")
out.append("")
out.append("| 量 | live | 含注释 |")
out.append("| --- | --- | --- |")
out.append(f"| `arguments.Required(` | {live_required} | {raw_required} |")
out.append(f"| ├ 字面量下标 `Required(<n>)` | {live_literal_required} | {live_count(r'arguments\.Required\(\s*\d', src)} |")
out.append(f"| └ 非字面量 `Required(i)` 等 | {live_required - live_literal_required} | — |")
out.append(f"| `arguments[<字面量>]` | {live_literal_index} | {live_count(r'arguments\[\s*\d', src)} |")
out.append(f"| `arguments[<变量>]` | {live_var_index} | {live_count(r'arguments\[\s*[A-Za-z_]', src)} |")
out.append(f"| `arguments.Count` | {live_count_expr} | {live_count(r'arguments[.]Count', src)} |")
out.append(f"| `as VariableTerm` | {live_as_var} | {live_count(r'as VariableTerm', src)} |")
out.append(f"| `(VariableTerm)` | {live_paren_var} | {live_count(r'[(]VariableTerm[)]', src)} |")
out.append(f"| `is VariableTerm` | {live_is_var} | {live_count(r'is VariableTerm', src)} |")
out.append(f"| `.Restructure(exm)` | {live_fold} | {live_count(r'[.]Restructure[(]exm[)]', src)} |")
out.append(f"| 列表模式 `arguments is […]` | {live_list_pattern} | {live_count(r'arguments is \[', src)} |")
out.append(f"| `arguments.Skip(` / `arguments.Cast<` | {live_skip} / {live_cast_loop} | — |")
out.append(f"| `HasUniqueRestructure = true;` | {has_unique_stmts} 条语句 / {has_unique_classes} 个类 | — |")
out.append(f"| `UniqueRestructure` override | {unique_overrides} 个 | — |")
out.append(f"| `[.. Arguments]`（全仓） | {copy_arguments} | — |")
out.append(f"| 全仓 `arguments.Required(` | {repo_live} | — |")
out.append(f"| 可省略 (类,槽) 全集 | {len(all_omittable)} | — |")
out.append(f"| 其中已带 `ArgType.NotNull` | {sum(1 for k in all_omittable if flagged(*k))} | — |")
out.append("")
out.append(f"可空的槽共 **{len(cand) + len(guarded) + len(unseen)}** 项 = A 段（无判空 + 硬解引用，需本片处置）**{len(cand)}** "
           f"+ B 段（类内有判空写法）**{len(guarded)}** + A2 段（无判空但机械未见硬解引用）**{len(unseen)}**；"
           f"其余 {len(all_omittable) - len(cand) - len(guarded) - len(unseen)} 项已在全部可省略段带 `ArgType.NotNull`。")
out.append("")
out.append(f"A 段 {len(cand) + len(done)} 项里，本片（阶段 A 片 1）标了 **{len(done)}** 个 `ArgType.NotNull`，"
           f"另 **{len(cand)}** 个经逐点核查为「有意可空」（读取路径把 null 当默认值用，标了会拒掉可用写法）。")
out.append("")
out.append("## A. 可达 null 候选与裁定")
out.append("")
out.append("判据：**可空**（存在规则段 `OmitStart >= 0` 且 `OmitStart <= i < ArgTypes.Count` 且该槽未标 `ArgType.NotNull`）")
out.append("∧ 读取形态含硬解引用 ∧ 类内该槽无判空写法。`状态` 列的「已标」= 本片已在其全部可省略段加 `ArgType.NotNull`，")
out.append("槽因此不再可空（机械判定）；「裁定有意可空」= 不标，理由见 `处置` 列。")
out.append("")
out.append("`数量省略尺子` 是机械预筛给不出的那一列——须人工核「读该槽时是否保证 `Count >= i + 1`」；")
out.append("`可能缺失` 列是该尺子的机械候选（存在一个规则段的最小实参数 <= i）。")
out.append("")
out.append("| 状态 | 类[槽] | 读取形态 | 读取点（方法） | 可省略段 | 可能缺失 | 已带标志 | 类别 | 处置 | 数量省略尺子 |")
out.append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
for key in sorted(set(cand) | set(done)):
    r = rows[key]
    state = "已标（片 1）" if key in done else "裁定有意可空"
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in blocks_by_class[r["cls"]]
                     if b["om"] >= 0 and b["om"] <= r["slot"] < b["argc"]) or "—"
    ruling = RULINGS.get(key, ("（未裁定）", "—", "—"))
    flag = "是" if r["flag"] else "—"
    out.append(f"| {state} | `{r['cls']}[{r['slot']}]` | {'/'.join(r['forms'])} | {methods_of(src, lines, r['positions'])} | {segs} | "
               f"{'是' if r['missing'] else '—'} | {flag} | {ruling[0]} | {ruling[1]} | {ruling[2]} |")
out.append("")
if summary:
    out.append("类别分布：" + "、".join(f"{c} {summary[c]}" for c in CATEGORIES if summary[c]) + "。")
    out.append("")
out.append("## A2. 可空、无判空、机械未见硬解引用")
out.append("")
out.append("这些槽的读取路径不在「字面量下标」车道（走非字面量下标 / 整表转发），或函数体根本不读；")
out.append("裁定来自 [arg-contract/issues/03](03-required-on-omittable-slot.md) 的片 2-C/2-D，本片不改。")
out.append("")
out.append("| 类[槽] | 可省略段 | 读取形态 | 类级读取路径（D 段） | 类别 | 裁定依据 |")
out.append("| --- | --- | --- | --- | --- | --- |")
for key in sorted(unseen):
    r = unseen[key]
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in blocks_by_class[r["cls"]]
                     if b["om"] >= 0 and b["om"] <= r["slot"] < b["argc"]) or "—"
    nl = nonliteral[r["cls"]]
    lane = "、".join(f"{k}×{v}" if v > 1 else k for k, v in nl.items()) or "—"
    ruling = RULINGS_A2.get(key, ("（未裁定）", "—"))
    out.append(f"| `{r['cls']}[{r['slot']}]` | {segs} | {'/'.join(r['forms']) or '—'} | {lane} | {ruling[0]} | {ruling[1]} |")
out.append("")
out.append("## B. 可空但有判空写法（人工核「判空是否守住解引用」）")
out.append("")
out.append("本段与 [arg-contract/issues/03](03-required-on-omittable-slot.md) 的 B 表逐项对应：34 项「有意可空」")
out.append("（裁定见该票片 2-B） + 片 2-D 补判的 `DT_SELECT[1]`/`[2]`、`GDRAWG[10]`（有意可空），共 37 项。")
out.append("另两条「有意可空」按本工件 A 段的判据（无判空写法 ∧ 读取形态在解引用车道）落在 A 段，不在此列：")
out.append("`DataTableCellSetMethod[3]`、`DataTableColumnManagementMethod[1]`（跨构造器，Names 段空槽 = 写 RESULTS）。")
out.append("本片只复核判空是否守住解引用，不重复裁定。")
out.append("")
out.append("| 类[槽] | 读取形态 | 判空点（方法） | 可省略段 | 已带标志 |")
out.append("| --- | --- | --- | --- | --- |")
for key in sorted(guarded):
    r = guarded[key]
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in blocks_by_class[r["cls"]]
                     if b["om"] >= 0 and b["om"] <= r["slot"] < b["argc"]) or "—"
    out.append(f"| `{r['cls']}[{r['slot']}]` | {'/'.join(r['forms'])} | {methods_of(src, lines, r['guard'])} | {segs} | "
               f"{'是' if r['flag'] else '—'} |")
out.append("")
out.append("## C. 可省略槽全集的消费形态（类 × 槽）")
out.append("")
out.append("`判据` 列：`可空` = 检查器会放行该槽的显式空槽；`缺失` = 数量省略即可让列表短于该下标。")
out.append("`已带标志` = 该槽出现的**全部**可省略段都在该槽位带 `ArgType.NotNull`。")
out.append("")
out.append("| 类[槽] | 可省略段 | 已带标志 | 读取形态 | 可空 | 缺失 |")
out.append("| --- | --- | --- | --- | --- | --- |")
for key in sorted(all_omittable):
    r = rows[key]
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in blocks_by_class[r["cls"]]
                     if b["om"] >= 0 and b["om"] <= r["slot"] < b["argc"]) or "—"
    out.append(f"| `{r['cls']}[{r['slot']}]` | {segs} | {'是' if r['flag'] else '—'} | "
               f"{'/'.join(r['forms']) or '—'} | {'是' if r['empty'] else '—'} | {'是' if r['missing'] else '—'} |")
out.append("")
out.append("## D. 类级形态（无法归到具体槽位）")
out.append("")
out.append("| 类 | 变长下标 | 整段转发 | Cast/foreach | `Count` 循环 | `Count` 引用 | 列表模式 | 折叠站点 |")
out.append("| --- | --- | --- | --- | --- | --- | --- | --- |")
for name, _pos, _end in classes:
    nl = nonliteral[name]
    if not nl:
        continue

    def g(k):
        return nl.get(k, "—")

    out.append(f"| `{name}` | {g('变长下标 `Required(i)`')} | {g('整段转发 `Skip(k)`')} | "
               f"{g('`Cast<>`')}/{g('`foreach (… in arguments)`')} | {g('`for(… arguments.Count …)`')} | "
               f"{g('`arguments.Count`')} | {g('列表模式 `arguments is […]`')} | {g('折叠站点 `.Restructure(exm)`')} |")
out.append("")
out.append("## F. 与邻票对齐")
out.append("")
out.append("### issues/03（`ArgType.NotNull` 的既有铺开）")
out.append("")
out.append(f"本表机械复核：可省略 (类,槽) 全集 **96**，其中**全部**可省略段都带 `ArgType.NotNull` 的 **{sum(1 for k in all_omittable if flagged(*k))}** 个")
out.append(f"= issues/03 的「拒绝侧共 50 个 (类,槽)」 + 本片（阶段 A 片 1）新增的 **{len(done)}** 个。")
out.append("A/B/A2 三段引用的裁定均来自该票（片 2-A/2-B/2-C/2-D），本片不重复裁定。")
out.append("")
out.append("### nullable-migration/issues/18（`AllowConstRef` 与折叠的不变量）")
out.append("")
out.append("该票立的不变量：**规则段里出现 `AllowConstRef` 的引用位 ⇒ 该类必须设 `HasUniqueRestructure`**，")
out.append("否则常量数组实参被折成 `SingleTerm`、函数体的 `(VariableTerm)` 转换失效。机械复核结果：")
out.append("")
out.append("| 类[槽] | 可省略段 | 已设 `HasUniqueRestructure` | 结论 |")
out.append("| --- | --- | --- | --- |")
for name, i, om, argc, ok, exempt in sorted(invariant_rows):
    if ok:
        verdict = "受不变量保护"
    elif exempt:
        verdict = f"豁免：{exempt}"
    else:
        verdict = "**违例**"
    out.append(f"| `{name}[{i}]` | {om}/{argc} | {'是' if ok else '否'} | {verdict} |")
out.append("")
out.append(("不变量全部成立或已豁免。" if not invariant_violations
            else "**违例**：" + "、".join(f"`{n}[{i}]`" for n, i in invariant_violations)))
out.append("")
out.append("该不变量覆盖的是「引用型实参被折叠成 `SingleTerm`」这条 null 来源，与 A 段的「显式空槽」正交；")
out.append("两类都不在 A 段里重复列出（A 段判据只看检查器是否放行 null 元素）。")
out.append("")
out.append("## E. 自校验")
out.append("")
out.append("| 检查 | 结果 |")
out.append("| --- | --- |")
out.append(f"| 本文件 live `arguments.Required(` == 404 | {'通过' if live_required == 404 else '**不符：' + str(live_required) + '**'} |")
out.append(f"| 与 [slots-worklist.py](slots-worklist.py) 同函数同口径的 `ArgType.NotNull` 计数"
           f"（含注释 {NOT_NULL_EXPECTED[0]} 处/{NOT_NULL_EXPECTED[1]} 行、剥注释 {NOT_NULL_EXPECTED[2]} 处/{NOT_NULL_EXPECTED[3]} 行） | "
           f"{'通过' if nn_ok else '**不符：%d 处/%d 行、%d 处/%d 行**' % nn_actual} |")
out.append(f"| `HasUniqueRestructure = true;` 语句/类 == 17/13 | {'通过' if (has_unique_stmts, has_unique_classes) == (17, 13) else '**不符：%d/%d**' % (has_unique_stmts, has_unique_classes)} |")
out.append(f"| `UniqueRestructure` override == 14 | {'通过' if unique_overrides == 14 else '**不符：' + str(unique_overrides) + '**'} |")
out.append(f"| 全仓 `[.. Arguments]` == 1 | {'通过' if copy_arguments == 1 else '**不符：' + str(copy_arguments) + '**'} |")
out.append(f"| 候选集裁定（未裁定 {len(unruled)} / 已落地 {len(done)} / 过期 {len(stale)}） | {'通过' if not unruled and not stale and len(done) == 4 else '**不符**'} |")
out.append(f"| A2 段与裁定表一致 | {'通过' if not unruled2 and not stale2 else '**未裁定 %d / 过期 %d**' % (len(unruled2), len(stale2))} |")
out.append(f"| A + A2 + B + 已标 = 可省略全集 96 | {'通过' if len(cand) + len(unseen) + len(guarded) + sum(1 for k in all_omittable if flagged(*k)) == 96 else '**不符**'} |")
out.append(f"| issues/18 不变量（`AllowConstRef` ⇒ `HasUniqueRestructure`） | {'通过' if not invariant_violations else '**违例 ' + '、'.join(invariant_violations) + '**'} |")
out.append(f"| helper 偏移（机械推出） | {', '.join(f'{k}={v}' for k, v in helpers.items())} |")
out.append("")

open(DST, "w", encoding="utf-8-sig", newline="\n").write("\n".join(out) + "\n")
print("rows", len(rows), "omittable", len(all_omittable), "cand", len(cand), "guarded", len(guarded))
