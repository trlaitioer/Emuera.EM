"""arg-contract 的实参消费面生成器：issues/04 阶段 A 片 0 的重新基线 + issues/05 阶段 B 的验收口径。

口径：
  · 扫描范围 = `Emuera/Runtime/Script/Statements/Function/BuiltinMethod/*.cs`（**规则表族**：183 个子类
    经 `FunctionMethod.CheckArgumentTypeEx` 校验；阶段 A 之前这些类都在单文件 `Creator.Method.cs` 里，
    现已按原区域顺序分居该目录，扫描顺序见 `_scan.BUILTIN_METHOD_SRC`）。另三族绕过规则表，不在本表：运算符族在
    `OperatorMethod.cs`（`CheckArgumentType` 抛 ExeEE）、StrForm 族在 `StrForm.cs`、TOSTR 包装在
    `Process.CalledFunction.cs`（都由调用点担保，见 G 段的 `Trusted` 计数）。
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
    整表转发、`Count` 循环等无法归到具体槽位的形态单列在 D 段。切片④ 后 helper 形参已换成
    `ArgumentSlots slots`，故 helper 偏移只在阶段 A 快照里有意义；G 段另按函数体收齐非字面下标站点。
  · 可达 null 候选 = 可空 ∧ 读取形态含硬解引用（`Int`/`Str`/`IsInt`/`IsStr`/`as Var`/`cast Var`/
    `term`/`index`/helper）∧ 类内该槽无判空写法。有判空写法的槽单列在 B 段（判空未必守住解引用，
    须人工核；issues/03 的 B 表 34 项即此类）。
  · 裁定表 `RULINGS` 是人写的处置列（键 = (类, 槽)），机械预筛给不出「这个 null 是什么意思」。
    候选集与裁定表不一致时脚本报警（缺裁定 = WARN 未裁定；裁定引用了不再出现的槽 = WARN 过期）。
    已带 `ArgType.NotNull` 的槽由脚本机械判定，不进裁定表。4 个硬转型槽的裁定取自跨脚本单一来源
    `_rulings.py`（slots-worklist.py 的 E 段引用同一条），避免两边文案各自漂移。
  · 自校验（复跑即报警，逐项见产物 E 段）：本文件 live `arguments.Required(` <= 404（404 是阶段 A 基线，
    阶段 B 迁移中递减，超出即 WARN）；`HasUniqueRestructure = true;`
    语句 17 / 类 13；`UniqueRestructure` override 14；全仓整表复制（`[.. Arguments]` / 切片④ 后的
    `[.. Slots.FoldTarget]`）1、`ArgumentSlots` 快照的 ctor 复制 1；候选集与裁定表一致；
    以及**与 [slots-worklist.py](slots-worklist.py) 同函数同口径**的 `ArgType.NotNull` 计数
    （计数函数在 `_scan.not_null_counts`，现行预期值是 `_scan.NOT_NULL_EXPECTED` 这一个常量——
    数字不写在这里，改 `BuiltinMethod/*.cs` 时只同步 `_scan.py` 一处即可同时约束两个产物）。
  · G 段（issues/05 阶段 B 的验收口径，见产物 G 段）：站点全集 = 全文件 live `arguments.Required(<任意下标>)`
    （含文件作用域 helper 内部的读取，如 `ReadPoint`）。**形态**按站点后随成员归类（词表与 `read_forms`
    一致，前缀窗 24 字符以容纳 `(VariableTerm)arguments.Required(n)` 这一写法）；**面**按花括号配对归属：
    落在 `UniqueRestructure` / `CheckArgumentType` / `Get(Int|Str)Value` / `GetReturnValue` 体内的分别是
    折叠面 / 检查面 / 消费面，类内其余方法与文件作用域 helper 都算消费面。阶段 B 只迁消费面，
    折叠面保持旧签名 ⇒ **门禁目标是消费面归零，不是全文件归零**。基线（HEAD 30686cf4）：
    全文件 372/404 = 92.1%（`Int` 188 + `Str` 152 + `IsInt` 15 + `IsStr` 17，复现 issues/04 的叙述值），
    消费面 369/379 = 97.4%；消费面 `Var` 家族 32（硬转型 29）、旧签名 override 229
    （规则表族 185 + Trusted 三族 44）、全库 `FunctionMethodTerm` 构造点 15。
用法：python3.14 tracker/arg-contract/arg-slots.py            # 只写阶段 B 的进度工件
      python3.14 tracker/arg-contract/arg-slots.py --phase-a  # 另写阶段 A 快照（只应在阶段 A 的 HEAD 上跑）
产物：tracker/arg-contract/slots-progress.md（默认：G 段 + 阶段 B 自校验；UTF-8 BOM，可逐字节复现）
      tracker/arg-contract/slots-consumption.md（--phase-a：阶段 A 的 A–F 与 E 段快照。阶段 B 重写消费面后
      那批表的「读取形态」扫描不到访问器调用、必然漂移，故该文件已冻结在基线 HEAD 30686cf4，不再默认重写；
      产物 .md 头部的冻结声明由生成器写出（`_scan.FREEZE_NOTE_CONSUMPTION`），不手工维护）
"""

import os
import re
import sys
from collections import Counter

from _rulings import consumption_rulings
from _scan import (FREEZE_NOTE_CONSUMPTION, NOT_NULL_EXPECTED, class_spans, load_sources, methods_of,
                   not_null_counts, null_handling_points, parse_blocks, strip_keep_lines)

# 扫描源 = `_scan.BUILTIN_METHOD_SRC`（阶段 A 之后 183 个子类分居 BuiltinMethod/*.cs；此前是单文件
# Creator.Method.cs，拼接顺序与拆分前的区域顺序一致）。
DST = r"tracker/arg-contract/slots-consumption.md"
PROGRESS_DST = r"tracker/arg-contract/slots-progress.md"
REPO = r"Emuera"
# 阶段 A 的快照（slots-consumption.md 的 A–F 与 E 段）只在迁移开始前可复现：阶段 B 重写消费面后，
# 那批表的「读取形态」扫描不到访问器调用，必然漂移。故默认只再生阶段 B 的进度工件（G 段），
# 需要重生成阶段 A 快照时显式加 --phase-a（只应在阶段 A 的 HEAD 上跑）。
PHASE_A = "--phase-a" in sys.argv

src = load_sources()

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
# 候选集/裁定表的一致性只对阶段 A 快照有意义：阶段 B 迁移后「类内读取形态」认不出来，
# 候选集会漂移（这正是快照要冻结的原因），故这批 WARN 只在 --phase-a 下打。
if PHASE_A:
    for k in unruled:
        print("WARN 候选未裁定：", k)
    for k in stale:
        print("WARN 裁定表引用了已消失的槽（类改名 / 不再可省略）：", k)
unruled2 = sorted(k for k in unseen if k not in RULINGS_A2)
stale2 = sorted(k for k in RULINGS_A2 if k not in unseen)
if PHASE_A:
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
copy_snapshot = 0
for root, _dirs, files in os.walk(REPO):
    for fn in files:
        if fn.endswith(".cs"):
            try:
                txt = open(os.path.join(root, fn), encoding="utf-8-sig").read()
            except UnicodeDecodeError:
                continue
            # unique 分支的整表副本只许有一处：`UniqueRestructure` 收到副本，故它内部的写回被丢弃（issues/04 #3）。
            # 切片④ 后该副本的源是 `Slots.FoldTarget`，形态从 `[.. Arguments]` 变成 `[.. arguments]`，
            # 故这里按「谁收到整表副本」定形，而不是按实参表的变量名。
            copy_arguments += live_count(r"UniqueRestructure\(exm, \[\.\. \w+\]\)", strip_keep_lines(txt))
            # 另一处整表复制在槽位类型自己的 ctor（检查期快照），它按定义就该有且只有一处——故单独立一条不变量，
            # 不再混进上面那条「折叠面不变量」的计数（两者的理由不同：那条是「折叠写回被丢弃」，这条是快照语义）。
            copy_snapshot += live_count(r"terms = \[\.\. arguments\]", strip_keep_lines(txt))

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

# ── 阶段 B（issues/05）的验收口径：访问器覆盖与「消费面 / 折叠面」拆分 ─────────────────────
# 站点全集 = 全文件 live `arguments.Required(<任意下标>)`，含文件作用域 helper（ReadPoint 等）内部的读取。
ACCESSOR_FORMS = ("Int", "Str", "IsInt", "IsStr")
OLD_SIG = (r"override\s+(?:long|string|SingleTerm)\s+"
           r"(?:Get(?:Int|Str)Value|GetReturnValue)\s*\(ExpressionMediator exm, List<AExpression\?> arguments\)")
TRUSTED_SRC = [
    r"Emuera/Runtime/Script/Statements/Expression/OperatorMethod.cs",
    r"Emuera/Runtime/Script/Data/StrForm.cs",
    r"Emuera/Runtime/Script/Process.CalledFunction.cs",
]
FACE_PATTERNS = [
    ("折叠面", r"(?:override|public|private|protected|internal|static)[\w<>?,\[\].\s]*?\bUniqueRestructure\s*\([^;{}]*\)\s*\{"),
    ("检查面", r"(?:override|public|private|protected|internal|static)[\w<>?,\[\].\s]*?\bCheckArgumentType(?:Ex)?\s*\([^;{}]*\)\s*\{"),
    ("消费面", r"(?:override|public|private|protected|internal|static)[\w<>?,\[\].\s]*?\bGet(?:Int|Str)Value\s*\([^;{}]*\)\s*\{"),
    ("消费面", r"(?:override|public|private|protected|internal|static)[\w<>?,\[\].\s]*?\bGetReturnValue\s*\([^;{}]*\)\s*\{"),
]


def brace_end(text, open_idx):
    """open_idx 处是 `{`，返回配对 `}` 之后的位置（找不到则到文末）。"""
    depth = 0
    for i in range(open_idx, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return i + 1
    return len(text)


def face_ranges(text):
    """(方法体起, 方法体止, 面) 三元组；同一站点取最内层。"""
    out = []
    for face, pat in FACE_PATTERNS:
        for m in re.finditer(pat, text):
            out.append((m.start(), brace_end(text, text.index("{", m.end() - 1)), face))
    return out


def required_form(text, start, end):
    """`arguments.Required(...)` 站点的形态；词表与 read_forms 一致（前缀窗放宽到 24）。"""
    tail, before = text[end:end + 32], text[max(0, start - 24):start]
    if tail.startswith(".GetIntValue("):
        return "Int"
    if tail.startswith(".GetStrValue("):
        return "Str"
    if tail.startswith(".IsInteger"):
        return "IsInt"
    if tail.startswith(".IsString"):
        return "IsStr"
    if tail.startswith(".Restructure("):
        return "折叠"
    if "as VariableTerm" in tail[:24]:
        return "as Var"
    if "(VariableTerm)" in before or "(VariableTerm)" in tail[:24]:
        return "cast Var"
    return "term"


REQUIRED_SITE = r"arguments\.Required\(\s*[^)]*\)"


def required_sites():
    """(面, 形态, 绝对位置) 的站点全集：类内按方法体归面，文件作用域 helper 一律消费面。"""
    out = []
    for _name, pos, cend in classes:
        span = code[pos:cend]
        rng = face_ranges(span)
        for m in re.finditer(REQUIRED_SITE, span):
            face, best = "消费面", None
            for s, e, f in rng:
                if s <= m.start() < e and (best is None or e - s < best[1] - best[0]):
                    best = (s, e, f)
            if best:
                face = best[2]
            out.append((face, required_form(span, m.start(), m.end()), pos + m.start()))
    for m in re.finditer(REQUIRED_SITE, code):
        if any(pos <= m.start() < cend for _n, pos, cend in classes):
            continue
        out.append(("消费面", required_form(code, m.start(), m.end()), m.start()))
    return out


cov_sites = required_sites()
cov_forms = Counter(f for _fa, f, _p in cov_sites)
cov_faces = Counter(fa for fa, _f, _p in cov_sites)
cov_total = len(cov_sites)
cov_file_covered = sum(cov_forms[f] for f in ACCESSOR_FORMS)
cov_cons_total = cov_faces["消费面"]
cov_cons_covered = sum(1 for fa, f, _p in cov_sites if fa == "消费面" and f in ACCESSOR_FORMS)
# 基线（HEAD 30686cf4，issues/04 的叙述值：372/404 = 92.1%）：只做「不增」检查，递减是阶段 B 的进度。
COV_BASELINE_FILE = {"Int": 188, "Str": 152, "IsInt": 15, "IsStr": 17}
COV_BASELINE_FILE_TOTAL = 404
COV_BASELINE_CONS = 379


def var_family_form(text, start, end):
    """`Var` 访问器覆盖的站点形态；None = 与变量实体无关。"""
    tail, before = text[end:end + 32], text[max(0, start - 24):start]
    if "as VariableTerm" in tail[:24]:
        return "as Var"
    if "(VariableTerm)" in before or "(VariableTerm)" in tail[:24]:
        return "cast Var"
    if "is VariableTerm" in tail[:24]:
        return "is Var"
    return None


var_family = Counter()
for _m in re.finditer(rf"(?:{REQUIRED_SITE}|arguments\[\s*[^\]]*\])", code):
    _f = var_family_form(code, _m.start(), _m.end())
    if _f:
        var_family[_f] += 1
var_family_total = sum(var_family.values())

# `Var` 家族（`as VariableTerm` / `(VariableTerm)` / `is VariableTerm`）按面拆分：阶段 B 要把消费面的
# 变量实体站点全部改成 `Var(i)`，故消费面硬转型数是切片④的门禁之一。
INDEX_SITE = rf"(?:{REQUIRED_SITE}|arguments\[\s*[^\]]*\])"


def index_sites():
    """(面, 形态, 绝对位置)：`arguments.Required(...)` 与 `arguments[...]` 的并集。"""
    out = []
    for _name, pos, cend in classes:
        span = code[pos:cend]
        rng = face_ranges(span)
        for m in re.finditer(INDEX_SITE, span):
            face, best = "消费面", None
            for s, e, f in rng:
                if s <= m.start() < e and (best is None or e - s < best[1] - best[0]):
                    best = (s, e, f)
            if best:
                face = best[2]
            out.append((face, var_family_form(span, m.start(), m.end()), pos + m.start()))
    for m in re.finditer(INDEX_SITE, code):
        if any(pos <= m.start() < cend for _n, pos, cend in classes):
            continue
        out.append(("消费面", var_family_form(code, m.start(), m.end()), m.start()))
    return out


idx_sites = index_sites()
cons_var_sites = sum(1 for fa, f, _p in idx_sites if fa == "消费面" and f)
cons_hard_cast = sum(1 for fa, f, _p in idx_sites if fa == "消费面" and f in ("as Var", "cast Var"))
fold_var_sites = sum(1 for fa, f, _p in idx_sites if fa == "折叠面" and f)
BASELINE_CONS_VAR = 32
BASELINE_CONS_HARD_CAST = 29


def is_empty_test(text, start, end):
    """槽位的判空写法（同 `_scan.null_handling_points` 的三组模式，下标不限字面量）。"""
    tail = text[end:end + 28]
    for kind, pat in (("`==/!= null`", r"\s*(?:==|!=)\s*null"),
                      ("`is null`", r"\s+is\s+(?:not\s+)?null"),
                      ("`?.`", r"\s*\?\.")):
        if re.match(pat, tail):
            return kind
    return None


empty_tests = Counter()
for _name, _pos, _cend in classes:
    _span = code[_pos:_cend]
    for _m in re.finditer(INDEX_SITE, _span):
        _k = is_empty_test(_span, _m.start(), _m.end())
        if _k:
            empty_tests[_k] += 1
for _m in re.finditer(INDEX_SITE, code):
    if any(pos <= _m.start() < cend for _n, pos, cend in classes):
        continue
    _k = is_empty_test(code, _m.start(), _m.end())
    if _k:
        empty_tests[_k] += 1
empty_test_total = sum(empty_tests.values())

# Trusted 三族（绕过规则表、由调用点担保）与 FunctionMethodTerm 构造点计数
trusted_live = {}
for p in TRUSTED_SRC:
    t = strip_keep_lines(open(p, encoding="utf-8-sig").read())
    trusted_live[p.split("/")[-1]] = {
        "Required": live_count(r"arguments\.Required\(", t),
        "字面量下标": live_count(r"arguments\[\s*\d+\s*\]", t),
        "硬转型": live_count(r"as VariableTerm", t) + live_count(r"\(VariableTerm\)", t),
        "构造点": live_count(r"new FunctionMethodTerm\(", t),
        "旧签名 override": live_count(OLD_SIG, t),
    }
trusted_ctor_total = sum(v["构造点"] for v in trusted_live.values())
literal_ctor_total = 0
target_typed_ctor = 0
trusted_entry_total = 0
checked_entry_total = 0
for _root, _dirs, _files in os.walk(REPO):
    for _fn in _files:
        if not _fn.endswith(".cs"):
            continue
        try:
            _t = strip_keep_lines(open(os.path.join(_root, _fn), encoding="utf-8-sig").read())
        except UnicodeDecodeError:
            continue
        literal_ctor_total += live_count(r"new FunctionMethodTerm\(", _t)
        target_typed_ctor += live_count(r"FunctionMethodTerm\s+\w+\s*=\s*new\(", _t)
        trusted_entry_total += live_count(r"ArgumentSlots\.Trusted\(", _t)
        checked_entry_total += live_count(r"ArgumentSlots\.Checked\(", _t)
old_sig_rule_table = live_count(OLD_SIG, code)
old_sig_total = old_sig_rule_table + sum(v["旧签名 override"] for v in trusted_live.values())
trusted_required_total = sum(v["Required"] for v in trusted_live.values())

# ── 访问器前置核对（issues/05 切片④）：迁移后的函数体对「规则表允许为空」的槽取值时，体内必须有守卫 ──
# 判据同票面：`Int`/`Str`/`IsInteger`/`IsString`/`Var` 只在 role 含 Required 的槽上可直接读；规则表放行显式
# 空槽时，体内要先 `Has`/`IsEmpty`/`IsMissing` 把空槽挡掉。`slots.Count > n` **不算守卫**——数量省略与
# 显式空槽正交，元素在而值为 null 时 Count 守卫照样放行（票面 B 段那 7 处 `Count` 守卫因此只在规则表已把
# 该槽判为 Required 的类里成立，不在这张表里）。
SLOT_BODY = re.compile(r"override\s+(?:long|string|SingleTerm)\s+(?:Get(?:Int|Str)Value|GetReturnValue)\s*"
                       r"\(ExpressionMediator exm, ArgumentSlots slots\)\s*\{")
ACCESSOR_READ = re.compile(r"slots\.(Int|Str|IsInteger|IsString|Var|Term)\(\s*(?:exm,\s*)?(\w+)\s*\)")
CLAIM_ACCESSORS = ("Int", "Str", "IsInteger", "IsString", "Var")
accessor_rows = []
for _name, _pos, _cend in classes:
    _span = code[_pos:_cend]
    for _m in SLOT_BODY.finditer(_span):
        _body_open = _m.end() - 1
        _body = _span[_body_open:brace_end(_span, _body_open)]
        for _am in ACCESSOR_READ.finditer(_body):
            _acc, _idx = _am.group(1), _am.group(2)
            if not _idx.isdigit():
                continue
            _k = int(_idx)
            if not may_be_empty(_name, _k):
                continue
            _guard = bool(re.search(rf"slots\.(?:Has|IsEmpty|IsMissing)\(\s*{_k}\s*\)", _body))
            accessor_rows.append((_name, _k, _acc, _guard))
accessor_unproven = [r for r in accessor_rows if not r[3] and r[2] in CLAIM_ACCESSORS]

# 非字面下标站点：`may_be_empty` 按槽号算，算不出变量/偏移表达式落在哪一槽，故上面那张表覆盖不到它们。
# 这里按「函数体」收齐并给一条**弱判据**：体内出现 `Count`/`Has`/`IsEmpty`/`IsMissing` 之一即算有守卫
# （票面那 5 处 helper 偏移读取由调用点的常量 argNo + 规则表担保，单列、不计入判定——它们所在的函数体
# 连一个守卫都不写，正因如此不能拿同一条尺子量）。
SLOTS_FUNC = re.compile(
    r"(override\s+)?(?:long|string|SingleTerm|[\w<>\[\],?.]+)\s+(\w+)\s*\([^;{}]*ArgumentSlots\s+\w+[^;{}]*\)\s*\{")
ACCESSOR_READ_ANY = re.compile(r"slots\.(Int|Str|IsInteger|IsString|Var|Term|TermOrNull|Tail)\(\s*(?:exm\s*,\s*)?([^)]*?)\s*\)")


def class_at(pos):
    """pos 处所在的（最内层）规则表族子类名；文件作用域 helper 返回 None。"""
    best = None
    for _n, _p, _e in classes:
        if _p <= pos < _e and (best is None or _p > best[1]):
            best = (_n, _p)
    return best[0] if best else None


accessor_dyn = []        # (类, 函数名, 访问器, 下标表达式, 体内有守卫) —— override 的槽位入口
accessor_dyn_helper = []  # (helper 名, 访问器, 下标表达式) —— 文件作用域 helper（调用点担保）
for _m in SLOTS_FUNC.finditer(code):
    _ob = _m.end() - 1
    _func_body = code[_ob:brace_end(code, _ob)]
    for _am in ACCESSOR_READ_ANY.finditer(_func_body):
        _idx = _am.group(2).strip()
        if _idx.isdigit():
            continue
        _guard = bool(re.search(r"slots\.(?:Count|Has|IsEmpty|IsMissing)\b", _func_body))
        if _m.group(1):
            accessor_dyn.append((class_at(_m.start()), _m.group(2), _am.group(1), _idx, _guard))
        else:
            accessor_dyn_helper.append((_m.group(2), _am.group(1), _idx))
dyn_all = [m for m in ACCESSOR_READ_ANY.finditer(code) if not m.group(2).strip().isdigit()]
dyn_unproven = [r for r in accessor_dyn if not r[4] and r[2] in CLAIM_ACCESSORS]

# `arguments.Count` 按面拆分：消费面要归零，折叠面（UniqueRestructure 收旧签名）保留。
count_faces = Counter()
for _name, _pos, _cend in classes:
    _span = code[_pos:_cend]
    _rng = face_ranges(_span)
    for _m in re.finditer(r"arguments\.Count", _span):
        _face, _best = "消费面", None
        for _s, _e, _f in _rng:
            if _s <= _m.start() < _e and (_best is None or _e - _s < _best[1] - _best[0]):
                _best = (_s, _e, _f)
        count_faces[_best[2] if _best else "消费面"] += 1


# 与 slots-worklist.py 同函数同口径地数 `ArgType.NotNull`（issues/04 承诺的自校验项）
nn = not_null_counts(src)
nn_actual = (nn["raw_occ"], nn["raw_lines"], nn["live_occ"], nn["live_lines"])
nn_ok = nn_actual == NOT_NULL_EXPECTED

checks = []          # 阶段 A 快照（slots-consumption.md）的自校验：只在 --phase-a 再生时才有意义
checks_b = []        # 阶段 B 进度工件（slots-progress.md）的自校验：每次都跑
if live_required != COV_BASELINE_FILE_TOTAL:
    checks.append(f"本文件 live `arguments.Required(` = {live_required}，预期 404"
                  "（阶段 A 快照的基线；阶段 B 迁移后重跑必然报警，属预期，进度看 slots-progress.md）")
if repo_live < live_required:
    checks.append("全仓 live 计数小于本文件，扫描口径有误")
if has_unique_stmts != 17:
    checks.append(f"`HasUniqueRestructure = true;` 语句数 = {has_unique_stmts}，预期 17")
if has_unique_classes != 13:
    checks.append(f"带该标志的类数 = {has_unique_classes}，预期 13")
if unique_overrides != 14:
    checks.append(f"`UniqueRestructure` override 数 = {unique_overrides}，预期 14")
if copy_arguments != 1:
    checks.append(f"全仓整表复制 `[.. Arguments]`/`[.. Slots.FoldTarget]` = {copy_arguments}，"
                  "预期 1（FunctionMethodTerm.cs 的 unique 分支）")
if copy_snapshot != 1:
    checks.append(f"`ArgumentSlots` 快照的整表复制 `terms = [.. arguments]` = {copy_snapshot}，"
                  "预期 1（FunctionMethod.cs 的 ArgumentSlots ctor）")
if not nn_ok:
    checks.append(f"`ArgType.NotNull` 计数（含注释 处/行、剥注释 处/行）= {nn_actual}，预期 {NOT_NULL_EXPECTED}"
                  "（与 slots-worklist.py 同函数同口径）")

# ── issues/18 的不变量：规则含 `AllowConstRef` 的引用位 ⇒ 该类必须设 HasUniqueRestructure ─────────
# 理由：常量数组实参会被 `VariableTerm.Restructure` 折成 `SingleTerm`，函数体的 `(VariableTerm)` 转换随后失效；
# 该类的 `UniqueRestructure`（收到的是整表副本）是仓库既有的规避手法。违例进 b_rows，故默认产物也报警。
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
    ("SumArrayMethod", 0): "角色数据段（`CharacterData \\| RefIntArray \\| AllowConstRef`）：常量数组不可能是角色数组变量",
}
invariant_rows = []
for name, i, om, argc in allow_const:
    exempt = INVARIANT_EXEMPT.get((name, i))
    invariant_rows.append((name, i, om, argc, name in unique_classes, exempt))
invariant_violations = sorted({(n, i) for n, i, _om, _argc, ok, ex in invariant_rows if not ok and not ex})

# 测试侧的槽位构造点：`REPO` 只含 `Emuera/`，故直调工厂的用例单列计数——否则它就是第五条无人看管的构造路径。
test_factory_total = 0
for root, _dirs, files in os.walk("Emuera.Tests"):
    for fn in files:
        if fn.endswith(".cs"):
            try:
                txt = open(os.path.join(root, fn), encoding="utf-8-sig").read()
            except UnicodeDecodeError:
                continue
            test_factory_total += live_count(r"ArgumentSlots\.(?:Trusted|Checked)\(", strip_keep_lines(txt))

# 阶段 B 的自校验：结构不变量 + 相对基线不增 + 构造路径不增 + 终态一致性。
# 这里也重申「折叠面不动」的三项（17/13/14/1/NotNull）——迁移不该碰它们，故两边都查。
# b_rows 同时喂「报警」与进度工件的自校验表，避免两处各写一份判据。
b_rows = [
    ("G 段站点全集 == 本文件 live `arguments.Required(`",
     cov_total == live_required),
    ("G 段形态划分和 == 站点全集", sum(cov_forms.values()) == cov_total),
    ("G 段面划分和（消费 + 检查 + 折叠）== 站点全集", sum(cov_faces.values()) == cov_total),
    ("G 段基线不增（全文件覆盖 372 / 消费面 379 / 消费面 `Var` 32 / 硬转型 29）",
     cov_file_covered <= 372 and cov_cons_total <= COV_BASELINE_CONS
     and cons_var_sites <= BASELINE_CONS_VAR and cons_hard_cast <= BASELINE_CONS_HARD_CAST),
    ("Trusted 三族构造点 == 13，全库字面 == 14，target-typed == 1",
     (trusted_ctor_total, literal_ctor_total, target_typed_ctor) == (13, 14, 1)),
    ("`ArgumentSlots.Trusted(` == 15（担保族 13 + 工厂的无声明分支 2），"
     "`ArgumentSlots.Checked(` == 2（工厂：命中段 / 定数表）",
     (trusted_entry_total, checked_entry_total) == (15, 2)),
    ("阶段 B 终态一致性（规则表族：消费面归零 ⟺ 旧签名 override 归零）",
     (cov_cons_total == 0) == (old_sig_rule_table == 0)),
    ("阶段 B 终态一致性（Trusted 三族：`Required(` 归零 ⟺ 旧签名 override 归零）",
     (trusted_required_total == 0) == (old_sig_total - old_sig_rule_table == 0)),
    ("访问器前置：可空槽上的 Int/Str/IsInteger/IsString/Var 读取都有体内守卫（`Count` 不算守卫）",
     not accessor_unproven),
    ("访问器前置：动态下标站点所在的函数体都有守卫（弱判据：体内出现 `Count`/`Has`/`IsEmpty`/`IsMissing`）",
     not dyn_unproven),
    ("动态下标站点划分 == 全文件扫描（函数体内 + helper 内）",
     len(accessor_dyn) + len(accessor_dyn_helper) == len(dyn_all)),
    ("`ArgumentSlots` 快照的整表复制 == 1（ctor 的 `terms = [.. arguments]`）", copy_snapshot == 1),
    ("折叠面不变量（17/13、override 14、整表复制 1）",
     (has_unique_stmts, has_unique_classes, unique_overrides, copy_arguments) == (17, 13, 14, 1)),
    (f"`ArgType.NotNull` 计数 == {NOT_NULL_EXPECTED}（与 slots-worklist.py 同函数同口径）", nn_ok),
    ("全仓 live 计数 >= 本文件", repo_live >= live_required),
    ("issues/18 不变量（`AllowConstRef` ⇒ `HasUniqueRestructure`）无违例", not invariant_violations),
    ("测试侧的槽位构造点 == 7（ArgumentSlotsTests 直调 `Trusted(`；门禁扫描根只含 `Emuera/`）",
     test_factory_total == 7),
]
checks_b = [label for label, ok in b_rows if not ok]


unflagged_need = sorted(k for k, r in rows.items()
                        if r["in_omittable"] and r["empty"] and not r["flag"] and not r["guard"])

if PHASE_A:
    if checks:
        for c in checks:
            print("WARN 阶段 A 快照自校验：", c)
    else:
        print("阶段 A 快照自校验通过")
if checks_b:
    for c in checks_b:
        print("WARN 阶段 B 自校验：", c)
else:
    print("阶段 B 自校验通过")

# ── 产物 ─────────────────────────────────────────────────────────────────────────
out = []
out.append("# arg-contract/issues/04 阶段 A 片 0：实参消费面重新基线")
out.append("")
out.append("本文件由 [arg-slots.py](arg-slots.py) 生成，口径见脚本头注释；票见 [arg-contract/issues/04](issues/04-argument-slots.md)。")
out.append("")
out.extend(FREEZE_NOTE_CONSUMPTION)
out.append("")
out.append("扫描范围是**规则表族**（`BuiltinMethod/*.cs` 的 183 个子类，经 `FunctionMethod.CheckArgumentTypeEx` 校验）。")
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
out.append(f"| unique 分支的整表副本（`UniqueRestructure(exm, [.. …])`） | {copy_arguments} | — |")
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
out.append("裁定来自 [arg-contract/issues/03](issues/03-required-on-omittable-slot.md) 的片 2-C/2-D，本片不改。")
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
out.append("本段与 [arg-contract/issues/03](issues/03-required-on-omittable-slot.md) 的 B 表逐项对应：34 项「有意可空」")
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
# G 段单独成篇（阶段 B 进度工件）：先记起点，段末整体摘出。
g_begin = len(out)
out.append("## G. 阶段 B 验收口径：访问器覆盖（issues/05）")
out.append("")
out.append(f"站点全集 = 本文件 live `arguments.Required(<任意下标>)` **{cov_total}** 处（含文件作用域 helper 内部的读取，")
out.append("如 `ReadPoint`/`ReadColormatrix`）。形态按站点后随成员归类（词表同 `read_forms`，前缀窗 24 字符），")
out.append("面按花括号配对归属到方法体。**阶段 B 只迁消费面**：14 个 `UniqueRestructure` 保持旧签名")
out.append("（旧签名收 `List<AExpression?>`，写回被丢弃），故门禁目标是**消费面归零**，不是全文件归零。")
out.append("")
g_forms = ["Int", "Str", "IsInt", "IsStr", "term", "as Var", "cast Var", "折叠"]
out.append("| 面 | " + " | ".join(f"`{f}`" for f in g_forms) + " | 合计 | 四访问器覆盖 |")
out.append("| --- | " + " | ".join("---" for _ in g_forms) + " | --- | --- |")
for _face in [f for f in ("消费面", "检查面", "折叠面") if f in cov_faces]:
    _c = Counter(f for fa, f, _p in cov_sites if fa == _face)
    _cov = sum(_c[f] for f in ACCESSOR_FORMS)
    out.append(f"| {_face} | " + " | ".join(str(_c[f]) if _c[f] else "—" for f in g_forms)
               + f" | {sum(_c.values())} | {_cov} |")
out.append(f"| 合计 | " + " | ".join(str(cov_forms[f]) if cov_forms[f] else "—" for f in g_forms)
           + f" | {cov_total} | {cov_file_covered} |")
out.append("")
out.append(f"全文件四访问器覆盖 **{cov_file_covered}/{cov_total}**"
           + (f"（= {cov_file_covered / cov_total * 100:.1f}%）" if cov_total else "")
           + f"：`Int` {cov_forms['Int']} + `Str` {cov_forms['Str']}"
           f" + `IsInt` {cov_forms['IsInt']} + `IsStr` {cov_forms['IsStr']}；"
           f"剔除折叠面后的**消费面覆盖 {cov_cons_covered}/{cov_cons_total}**"
           + (f"（= {cov_cons_covered / cov_cons_total * 100:.1f}%）" if cov_cons_total else "")
           + "。基线见「归零目标」表——迁移中站点数递减，故这两个比值只描述当前未迁移的部分。")
out.append("")
out.append("| 访问器 | 站点数（当前） | 说明 |")
out.append("| --- | --- | --- |")
out.append(f"| `Int`/`Str`/`IsInt`/`IsStr` | {cov_file_covered} | 全文件；消费面 {cov_cons_covered} |")
out.append(f"| `Var` | {var_family_total} | "
           + " + ".join(f"{k} {v}" for k, v in sorted(var_family.items()))
           + f"（全文件；其中消费面 {cons_var_sites}、折叠面 {fold_var_sites}，切片④只归零消费面） |")
out.append(f"| `Term`/`TermOrNull` | {cov_forms['term']} | 消费面里的裸句柄逃生口 |")
out.append(f"| `Tail` | {live_skip} | `arguments.Skip(k)` 整段转发（GETMETH/GETMETHS） |")
out.append(f"| `Count` | {live_count_expr + live_list_pattern + live_cast_loop} | "
           f"`arguments.Count` {live_count_expr} + 列表模式 {live_list_pattern} + `Cast<>` {live_cast_loop} |")
out.append(f"| `Has`/`IsEmpty`/`IsMissing` | {empty_test_total + live_list_pattern} | 判空写法 {empty_test_total}（"
           + " + ".join(f"{k} {v}" for k, v in sorted(empty_tests.items()))
           + f"） + 列表模式 {live_list_pattern} |")
out.append("")
out.append("### Trusted 三族（绕过规则表、由调用点担保，issues/05 的 A 边界）")
out.append("")
out.append("| 文件 | `arguments.Required(` | `arguments[<字面量>]` | 硬转型 | `new FunctionMethodTerm(` | 旧签名 override |")
out.append("| --- | --- | --- | --- | --- | --- |")
for _fn, _v in trusted_live.items():
    out.append(f"| `{_fn}` | {_v['Required']} | {_v['字面量下标']} | {_v['硬转型']} | {_v['构造点']} | {_v['旧签名 override']} |")
out.append(f"| 合计（Trusted 三族） | {sum(v['Required'] for v in trusted_live.values())} | "
           f"{sum(v['字面量下标'] for v in trusted_live.values())} | "
           f"{sum(v['硬转型'] for v in trusted_live.values())} | **{trusted_ctor_total}** | "
           f"{sum(v['旧签名 override'] for v in trusted_live.values())} |")
out.append("")
out.append(f"全库构造点 **{literal_ctor_total + target_typed_ctor}** = Trusted 三族 {trusted_ctor_total} "
           f"+ 规则表族 2（`IdentifierDictionary.cs` 字面 1 + `ArgumentBuilder.cs` target-typed {target_typed_ctor}）；"
           f"字面 `new FunctionMethodTerm(` 全库 {literal_ctor_total}。`ArgumentSlots.Trusted(` 调用点 {trusted_entry_total}"
           f"（调用点担保族 13 + `FunctionMethod.ResolveSlots` 的无声明分支 2）、`ArgumentSlots.Checked(` "
           f"{checked_entry_total}（同一个工厂：命中段 / 定数表）。测试侧另有 {test_factory_total} 处直调工厂"
           "（`Emuera.Tests` 不在门禁扫描根内，故单列）。")
out.append("")
out.append(f"阶段 B 进度：消费面 `arguments.Required(` 剩余 **{cov_cons_total}**（基线 379 → 目标 0）；"
           f"旧签名 override 剩余 **{old_sig_total}**（规则表族 {old_sig_rule_table} + Trusted 三族 "
           f"{old_sig_total - old_sig_rule_table}，目标 0，见 issues/05 切片④）。")
out.append("")
out.append("### 阶段 B 的归零目标（切片④的门禁）")
out.append("")
out.append("| 量 | 基线（HEAD 30686cf4） | 当前 | 目标 |")
out.append("| --- | --- | --- | --- |")
out.append(f"| 消费面 `arguments.Required(` | 379 | {cov_cons_total} | 0 |")
out.append(f"| 消费面 `Var` 家族（`as`/cast/`is VariableTerm`） | {BASELINE_CONS_VAR} | {cons_var_sites} | 0（迁到 `Var(i)`） |")
out.append(f"| 消费面硬转型（`as` + cast） | {BASELINE_CONS_HARD_CAST} | {cons_hard_cast} | 0 |")
out.append(f"| 列表模式 `arguments is […]` | 16 | {live_list_pattern} | 0（迁到 `Has`/`IsEmpty`/`IsMissing`） |")
out.append(f"| `arguments.Count` | 119 | {count_faces.get('消费面', 0)} | 0（消费面）；"
           + "、".join(f"{k} {v} 处" for k, v in sorted(count_faces.items()) if k != "消费面")
           + " 保留 |")
out.append(f"| `arguments.Skip(` / `Cast<` | 2 / 1 | {live_skip} / {live_cast_loop} | 0 / 0 |")
out.append(f"| 旧签名 override（规则表族 {old_sig_rule_table} + Trusted 三族 "
           f"{old_sig_total - old_sig_rule_table}） | 229 | {old_sig_total} | 0 |")
out.append("| 折叠面 `arguments.Required(` | 25 | " + str(cov_faces["折叠面"]) + " | 25（**不变**，旧签名保留） |")
out.append(f"| `ArgumentSlots` 快照整表复制（ctor 的 `terms = [.. arguments]`） | 0 | {copy_snapshot} | 1（类型落地时即应有；与上面那条折叠面副本理由不同） |")
out.append("")
out.append("### 访问器前置核对（切片④）")
out.append("")
out.append("扫描迁移后的槽位入口函数体：对**规则表允许显式空槽**的槽（`OmitStart` 之后且未标 `ArgType.NotNull`），")
out.append("`Int`/`Str`/`IsInteger`/`IsString`/`Var` 的读取必须由同体的 `Has`/`IsEmpty`/`IsMissing` 守卫兜住。")
out.append("`Term` 是原始句柄逃生口，列在表内只作观察、不计入判定。")
out.append("")
out.append("**判据的边界（据实写清）**：守卫判据是「同体内出现过该下标的一次判空」，既不限读取点先后，")
out.append("也不覆盖**非字面下标**（下标是变量/偏移时算不出落在哪一槽，`may_be_empty` 无从使用）——后者见下一张表。")
out.append("")
out.append("| 类 | 槽 | 访问器 | 体内有守卫 | 判定 |")
out.append("| --- | --- | --- | --- | --- |")
for _name, _k, _acc, _guard in sorted(accessor_rows):
    _verdict = "通过" if (_guard or _acc not in CLAIM_ACCESSORS) else "**未证明**"
    out.append(f"| `{_name}` | {_k} | `{_acc}` | {'是' if _guard else '—'} | {_verdict} |")
out.append("")
out.append(f"可空槽上的字面下标取值站点共 **{len(accessor_rows)}** 处，其中未证明 **{len(accessor_unproven)}** 处。")
out.append("")
out.append("### 访问器前置核对：非字面下标（弱判据）")
out.append("")
out.append("下标是变量/偏移表达式的读取。判据只到「同体内出现过 `Count`/`Has`/`IsEmpty`/`IsMissing` 之一」")
out.append("这一量级（弱于上表），故逐点仍需人工核对；列全是为了让「覆盖了多少」不再只是叙述。")
out.append("")
out.append("| 类 | 函数 | 访问器 | 下标表达式 | 体内有守卫 | 判定 |")
out.append("| --- | --- | --- | --- | --- | --- |")
for _cls, _func, _acc, _idx, _guard in sorted(set(accessor_dyn), key=lambda r: (r[0] or "", r[2], r[3])):
    _verdict = "通过（弱）" if (_guard or _acc not in CLAIM_ACCESSORS) else "**未证明**"
    out.append(f"| `{_cls}` | `{_func}` | `{_acc}` | `{_idx}` | {'是' if _guard else '—'} | {_verdict} |")
out.append("")
out.append(f"槽位入口函数体里的非字面下标站点去重后 **{len(set(accessor_dyn))}** 处（原始读取 {len(accessor_dyn)} 次），"
           f"其中未证明 **{len(dyn_unproven)}** 处；")
out.append(f"另有 **{len(set(accessor_dyn_helper))}** 处落在文件作用域 helper 内"
           "（`GraphicsDrawG` 族的 `ReadGraphics`/`ReadColor`/`ReadPoint`/`ReadRectangle`/`ReadColormatrix`：")
out.append("调用点传常量 `argNo`、规则表把那些槽标成必填，故这些 helper 体内不写守卫，由调用点担保）。")
out.append("helper 内明细：")
out.append("")
out.append("| helper | 访问器 | 下标表达式 |")
out.append("| --- | --- | --- |")
for _func, _acc, _idx in sorted(set(accessor_dyn_helper)):
    out.append(f"| `{_func}` | `{_acc}` | `{_idx}` |")
out.append("")
g_section = out[g_begin:]
del out[g_begin:]
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
out.append(f"| unique 分支的整表副本 == 1 | {'通过' if copy_arguments == 1 else '**不符：' + str(copy_arguments) + '**'} |")
out.append(f"| 候选集裁定（未裁定 {len(unruled)} / 已落地 {len(done)} / 过期 {len(stale)}） | {'通过' if not unruled and not stale and len(done) == 4 else '**不符**'} |")
out.append(f"| A2 段与裁定表一致 | {'通过' if not unruled2 and not stale2 else '**未裁定 %d / 过期 %d**' % (len(unruled2), len(stale2))} |")
out.append(f"| A + A2 + B + 已标 = 可省略全集 96 | {'通过' if len(cand) + len(unseen) + len(guarded) + sum(1 for k in all_omittable if flagged(*k)) == 96 else '**不符**'} |")
out.append(f"| issues/18 不变量（`AllowConstRef` ⇒ `HasUniqueRestructure`） | {'通过' if not invariant_violations else '**违例 ' + '、'.join(invariant_violations) + '**'} |")
out.append(f"| helper 偏移（机械推出） | {', '.join(f'{k}={v}' for k, v in helpers.items())} |")
out.append("")

# ── 产物 ─────────────────────────────────────────────────────────────────────────
# 默认只再生阶段 B 的进度工件；阶段 A 的快照（A–F + E 段）只在 --phase-a 时重写，
# 因为阶段 B 重写消费面后那批表必然漂移（快照只在其基线 HEAD 上可复现）。
b_out = []
b_out.append("# arg-contract/issues/05 阶段 B 进度：访问器覆盖与归零目标")
b_out.append("")
b_out.append("本文件由 [arg-slots.py](arg-slots.py) 生成（默认产物；`--phase-a` 才另写阶段 A 快照")
b_out.append("[slots-consumption.md](slots-consumption.md)，口径见脚本头注释）。票见")
b_out.append("[arg-contract/issues/05](issues/05-argument-slots-type.md)。")
b_out.append("")
b_out.extend(g_section)
b_out.append("## 自校验")
b_out.append("")
b_out.append("| 检查 | 结果 |")
b_out.append("| --- | --- |")
for _label, _ok in b_rows:
    b_out.append(f"| {_label} | {'通过' if _ok else '**不符**'} |")
b_out.append("")
open(PROGRESS_DST, "w", encoding="utf-8-sig", newline="\n").write("\n".join(b_out) + "\n")
if PHASE_A:
    open(DST, "w", encoding="utf-8-sig", newline="\n").write("\n".join(out) + "\n")
    print("wrote", DST, "(phase A snapshot)")
print("wrote", PROGRESS_DST)
print("rows", len(rows), "omittable", len(all_omittable), "cand", len(cand), "guarded", len(guarded),
      "| 消费面 Required", cov_cons_total, "| 旧签名 override", old_sig_total)
