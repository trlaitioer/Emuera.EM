"""arg-contract/issues/03 的逐槽清单生成器。

口径(与票内「范围」一致)：
  · 站点 = `Creator.Method.cs` 内 `arguments.Required(<字面量>)` 的出现处数（归并到 (类,槽) 后由 A/B 表「站点」列按方法名列出，同一方法里多处记 `×N`）；
  · 「可省略」= 该槽在某段规则里满足 OmitStart >= 0 且 OmitStart <= i < ArgTypes.Count；
  · 「未证」= 该槽不存在 i < OmitStart 的段（即检查器不拒该槽的 null）；
  · 「null 处理」在**剥注释后**的文本上做，识别三种写法：
      `arguments[i] == null` / `!= null` / `is null`、`arguments[i]?.`、
      列表模式 `arguments is [… not null …]`（`not null` 在模式里的位置即槽号）
    —— 只认第一种时，16 个用列表模式判空的「有意留空」槽会被误判进 A 表；
  · 「仅注释命中」= 该槽仅在含注释的文本里有 `== null`（旧手工检查器残留，如 `RandMethod` 构造器旁那处）。
  · 「已带标志」= 该槽出现的**全部**可省略段都在**该槽位**上带 `ArgType.NotNull`（arg-contract/issues/03 片 3 把
    `ArgType.DisallowVoid` 改名为 `ArgType.NotNull`；只标一段仍会从别的段放行 null；逐槽位判定，不是整块有就算）。
  · 本表 A/B 两段**只建模「被 `Required`(字面量) 读取」这一条车道**（76 项）。全集 96 项里另有 20 项
    不由字面量下标读取（helper 传参、变长下标 `Required(i)`、`arguments[i]` 硬转型、纯转发），
    归 E 段：机械列出读取痕迹，裁定与「数量省略 → 下标越界」那把尺子的结论见 E 段的裁定表
    （键为 (类,槽)，由人工核查函数体读取路径填入；缺项/多余项/与代码现状不符时脚本报警）。
  · 「数量省略 → 下标越界」（规则表把函数体无条件读取的槽划进可省略区间，于是少写尾实参也让
    列表短于该下标）是另一把尺子：看该站点读该槽时是否有保证 `Count >= i + 1` 的守卫。
    A 表逐站点核过（结果记在 D 段），E 段 20 项同样逐项核过（结果记在 E 段「尺子」列）。
剥注释是逐行朴素实现（不识别字符串字面量里的 `//`），命中处仍需人工确认。

用法：python3.14 .scratch/arg-contract/slots-worklist.py
产物：.scratch/arg-contract/slots-worklist.md（UTF-8 BOM，与本脚本的写出编码一致，故可逐字节复现）
"""

import re

SRC = r"Emuera/Runtime/Script/Statements/Function/Creator.Method.cs"
DST = r".scratch/arg-contract/slots-worklist.md"

src = open(SRC, encoding="utf-8-sig").read()
lines = src.split("\n")


def strip_keep_lines(text):
    out = list(text)
    i, n = 0, len(text)
    while i < n:
        if text.startswith("//", i):
            j = text.find("\n", i)
            j = n if j == -1 else j
            for k in range(i, j):
                out[k] = " "
            i = j
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)
            j = n if j == -1 else j + 2
            for k in range(i, j):
                if out[k] != "\n":
                    out[k] = " "
            i = j
        else:
            i += 1
    return "".join(out)


code = strip_keep_lines(src)

cls_re = re.compile(r"^\t(?:public |private |internal )?sealed class (\w+)\s*:", re.M)
marks = [(m.start(), m.group(1)) for m in cls_re.finditer(src)]
classes = []
for idx, (pos, name) in enumerate(marks):
    end = marks[idx + 1][0] if idx + 1 < len(marks) else len(src)
    classes.append((name, pos, end))

line_of = lambda pos: src.count("\n", 0, pos) + 1

# 产物里的定位一律写「类内方法名」，不写行号：行号会随任何插入而整体失效（AGENTS.md 要求
# 「引用代码位置用文件路径 + 函数名」）。同一方法里有多处时用 ×N 保留「几处」这一事实。
# 只认「<\t\t> [attribute] 访问修饰符 [其他修饰符] [返回类型] 名字 (」形态——否则
# `throw new CodeEE(`、`arguments.Required(` 这类语句也会被当成方法声明。
_method_sig = re.compile(
    r"^\t\t(?:\[[^\]]*\]\s*)?(?:public|private|protected|internal)\s+"
    r"(?:(?:static|override|virtual|sealed|async|partial|new|unsafe|extern)\s+)*"
    r"(?:[\w<>\[\],?.]+\s+)?(\w+)\s*\("
)
_method_kw = {"if", "for", "while", "switch", "return", "throw", "catch", "using", "lock",
              "foreach", "else", "new", "get", "set"}


def method_of(pos):
    """pos 所属的类内方法名（本文件所有类成员均为两个 Tab 缩进）。"""
    for k in range(line_of(pos) - 1, -1, -1):
        m = _method_sig.match(lines[k])
        if m and m.group(1) not in _method_kw:
            return m.group(1)
    return "—"


def methods_of(positions):
    """把若干位置折成「方法名(×N)」，按首次出现排序。"""
    names = [method_of(p) for p in positions]
    return "、".join(
        "%s×%d" % (nm, names.count(nm)) if names.count(nm) > 1 else nm
        for nm in dict.fromkeys(names)
    ) or "—"



def null_handling_points(text, base_pos, i):
    """text 内针对槽 i 的 null 处理点（文件内绝对字符位置）；口径见文件头。"""
    hits = []
    for pat in (
        rf"arguments\[{i}\]\s*(?:==|!=)\s*null",
        rf"arguments\[{i}\]\s+is\s+(?:not\s+)?null",
        rf"arguments\[{i}\]\s*\?\.",
    ):
        hits += [base_pos + m.start() for m in re.finditer(pat, text)]

    for m in re.finditer(r"arguments is \[([^\]]*)\]", text):
        parts = [p.strip() for p in m.group(1).split(",")]
        if i < len(parts) and parts[i] == "not null":
            hits.append(base_pos + m.start())

    return sorted(set(hits))


def parse_blocks(pos, end):
    text = src[pos:end]
    out = []
    for m in re.finditer(r"new ArgTypeList\s*\{", text):
        i = m.end() - 1
        d = 0
        while i < len(text):
            if text[i] == "{":
                d += 1
            elif text[i] == "}":
                d -= 1
                if d == 0:
                    break
            i += 1
        body = text[m.end():i]
        om = re.search(r"OmitStart\s*=\s*(-?\d+)", body)
        om = int(om.group(1)) if om else -1
        am = re.search(r"ArgTypes\s*=\s*\{", body)
        argc = 0
        entries = []
        if am:
            j = am.end() - 1
            dd = 0
            while j < len(body):
                if body[j] == "{":
                    dd += 1
                elif body[j] == "}":
                    dd -= 1
                    if dd == 0:
                        break
                j += 1
            entries = [x.strip() for x in body[am.end():j].split(",") if x.strip()]
            argc = len(entries)
        # entries 是逐槽位的 ArgType 表达式；NotNull 只对写出了它的那个槽位生效，
        # 故必须逐槽位判定（整块级的 "任何一处有" 会把同块其他槽一起算成已标）。
        out.append({"om": om, "argc": argc, "entries": entries, "line": line_of(pos + m.start())})
    return out


rows = []
block_rows = []
total_blocks = 0
all_omittable = set()
blocks_by_class = {}
span_of_class = {}
for name, pos, end in classes:
    blocks = parse_blocks(pos, end)
    blocks_by_class[name] = blocks
    span_of_class[name] = (pos, end)
    total_blocks += len(blocks)
    for b in blocks:
        if b["om"] >= 0 and b["argc"] > b["om"]:
            block_rows.append((name, b))
            for i in range(b["om"], b["argc"]):
                all_omittable.add((name, i))
    if not blocks:
        continue
    cls_code = code[pos:end]
    for m in re.finditer(r"arguments\.Required\((\d+)\)", cls_code):
        i = int(m.group(1))
        omittable = [b for b in blocks if b["om"] >= 0 and b["om"] <= i < b["argc"]]
        if not omittable:
            continue
        # 另有段要求该槽必填（i < OmitStart）时不能就此认为检查器已挡住：
        # 检查器「任一段通过即通过」，可省略段仍会放行 null（见票内机制事实 1）。
        mandatory = [b for b in blocks if b["om"] >= 0 and i < b["om"]]
        ln = line_of(pos + m.start())
        null_hits = null_handling_points(cls_code, pos, i)
        raw_hits = null_handling_points(src[pos:end], pos, i)
        helpers = sorted(set(re.findall(r"(\w+)\(Name,\s*exm,\s*arguments,\s*\d+", cls_code)))
        rows.append({"cls": name, "slot": i, "line": ln, "abs": pos + m.start(),
                     "segs": omittable, "must": mandatory,
                     "null": null_hits, "raw": raw_hits, "helpers": helpers})

# 归并到 (类,槽)：一个槽可被多个站点读取
merged = {}
for r in rows:
    key = (r["cls"], r["slot"])
    if key not in merged:
        merged[key] = dict(r, sites=[r["line"]], abs=[r["abs"]])
    else:
        merged[key]["sites"].append(r["line"])
        merged[key]["abs"].append(r["abs"])
        merged[key]["null"] = sorted(set(merged[key]["null"]) | set(r["null"]))
        merged[key]["raw"] = sorted(set(merged[key]["raw"]) | set(r["raw"]))
        merged[key]["must"] = merged[key]["must"] or r["must"]
rows = [merged[k] for k in sorted(merged)]
for r in rows:
    r["sites"] = sorted(set(r["sites"]))

need = [r for r in rows if not r["null"]]
intent = [r for r in rows if r["null"]]
misreport = [r for r in need if r["raw"]]

# ── E 段：补车道（未被 Required(字面量) 读取的可省略槽）──────────────────────────────
covered = {(r["cls"], r["slot"]) for r in rows}
supplement = sorted(all_omittable - covered)

# 裁定表（票内片 2-D 的结论）：键 (类, 槽) → (类别, 处置, 数量省略尺子, 是否须在全部可省略段带 NotNull)。
# 机械扫描给不出「这一槽空着是什么意思」，故逐槽人工核查函数体读取路径后填入；缺项/多余项/与代码现状
# 不符时脚本报警（与 A 表的 RULED_NULLABLE 校验同一思路）。
SUPPLEMENT_RULINGS = {
    ("DataTableSelectMethod", 1): ("有意可空", "规则段注释已写（空槽 = 不筛）", "过（Count > 1）", False),
    ("DataTableSelectMethod", 2): ("有意可空", "规则段注释已写（空槽 = 不排序）", "过（Count > 2）", False),
    ("DataTableSelectMethod", 3): ("硬转型解引用", "归 arg-contract/issues/04 片②（输出数组）", "过（Count != 4 的否支）", False),
    ("GraphicsDrawGMethod", 10): ("有意可空", "规则段注释已写（空槽 = 不带颜色矩阵）", "过（Count != 10 时判空短路）", False),
    ("GetMethMethod", 2): ("转发槽", "规则段注释已写（交给下游解释）", "过（Skip(2) 原样转发）", False),
    ("GetMethsMethod", 2): ("转发槽", "规则段注释已写（交给下游解释）", "过（Skip(2) 原样转发）", False),
    ("EnumNameMethod", 1): ("硬转型解引用", "归 arg-contract/issues/04 片②（输出数组）", "过（Count == 2）", False),
    ("EnumFilesMethod", 3): ("硬转型解引用", "归 arg-contract/issues/04 片②（输出数组）", "过（Count == 4）", False),
    ("DataTableToXmlMethod", 1): ("硬转型解引用", "归 arg-contract/issues/04 片②（输出数组）", "过（Count > 1）", False),
    ("MaxMethod", 1): ("消费型变长尾", "片 2-C 已标 NotNull", "过（循环 i < Count）", True),
    ("ArrayMultiSortMethod", 1): ("消费型变长尾", "片 2-C 已标 NotNull", "过（Cast 整表）", True),
    ("XmlAddNodeMethod", 4): ("无意且可达", "本次标 NotNull + 用例（Node 段 setAllPos、Attribute 段 methodPos 都读它）", "过（Count == 5 / Count >= 5）", True),
    ("XmlAddNodeMethod", 5): ("无意且可达", "本次标 NotNull + 用例（Attribute 段 setAllPos）", "过（Count == 6）", True),
    ("DataTableRowSetMethod", 2): ("有意可空", "本次补注释 + 用例（变长对 value 位 = DBNull；4 实参数组形态共用该槽，但由第二段 OmitStart == -1 拒空）", "过（Count == b + 4 / 变长对）", False),
    ("DataTableCellSetMethod", 3): ("有意可空", "本次补注释 + 用例（空槽 = 写 DBNull）", "过（Count > 3）", False),
    ("GraphicsDrawStringMethod", 2): ("无意且可达", "本次标 NotNull + 用例（ReadPoint 无判空）", "过（Count == 4）", True),
    ("GraphicsDrawStringMethod", 3): ("无意且可达", "本次标 NotNull + 用例（ReadPoint 无判空）", "过（Count == 4）", True),
    ("GraphicsDrawGWithRotateMethod", 3): ("无意且可达", "本次标 NotNull + 用例（ReadPoint 无判空）", "过（Count != 3 的否支）", True),
    ("GraphicsDrawGWithRotateMethod", 4): ("无意且可达", "本次标 NotNull + 用例（ReadPoint 无判空）", "过（Count != 3 的否支）", True),
    ("GraphicsDrawSpriteMethod", 6): ("无意且可达", "本次标 NotNull + 用例（ReadColormatrix 无判空）", "过（2/4/6 分支返回后即 Count == 7）", True),
}
SUPPLEMENT_CATEGORIES = ["有意可空", "转发槽", "消费型变长尾", "无意且可达", "硬转型解引用"]


def supplement_segments(cls, slot):
    return [b for b in blocks_by_class.get(cls, []) if b["om"] >= 0 and b["om"] <= slot < b["argc"]]


def supplement_flagged(cls, slot):
    segs = supplement_segments(cls, slot)
    return bool(segs) and all(slot < len(b["entries"]) and "NotNull" in b["entries"][slot] for b in segs)


def supplement_evidence(cls, slot):
    pos, end = span_of_class[cls]
    cls_code = code[pos:end]
    ev = []
    for m in re.finditer(rf"arguments\[{slot}\]", cls_code):
        ev.append(f"arguments[{slot}] @ {method_of(pos + m.start())}")
    if re.search(r"arguments\.Required\((?![0-9])", cls_code):
        ev.append("Required(非字面量)")
    if "arguments.Skip(" in cls_code:
        ev.append("arguments.Skip(...)")
    for m in re.finditer(rf"\w+\(Name, exm, arguments, {slot}\b", cls_code):
        ev.append(f"{m.group(0).split('(')[0]}(argNo={slot})")
    if slot > 0:
        # helper 内部还会读 argNo + 1（ReadPoint 的 y 座標等），机械痕迹补在这里，避免该槽显示成「未读」
        for m in re.finditer(rf"\w+\(Name, exm, arguments, {slot - 1}\b", cls_code):
            ev.append(f"{m.group(0).split('(')[0]}(argNo={slot - 1}+1)")
    return "、".join(dict.fromkeys(ev)) or "—"


unruled = [k for k in supplement if k not in SUPPLEMENT_RULINGS]
stale_rulings = [k for k in SUPPLEMENT_RULINGS if k not in supplement]
if unruled:
    print("WARN E 段有未裁定的可省略槽：", unruled)
if stale_rulings:
    print("WARN E 段裁定表引用了不在全集里的槽：", stale_rulings)
for cls, slot in supplement:
    ruling = SUPPLEMENT_RULINGS.get((cls, slot))
    if ruling is None:
        continue
    if ruling[3] and not supplement_flagged(cls, slot):
        print(f"WARN E 段 {cls}[{slot}] 裁定为需标 NotNull，但可省略段未标全")
    if not ruling[3] and supplement_flagged(cls, slot):
        print(f"WARN E 段 {cls}[{slot}] 裁定为可空，却在可省略段带了 NotNull")

supplement_counts = {c: sum(1 for k in supplement if SUPPLEMENT_RULINGS.get(k, ("",))[0] == c)
                     for c in SUPPLEMENT_CATEGORIES}

# 「已带标志」= 该槽出现的**全部**可省略段都在**该槽位**上带 ArgType.NotNull。只标一段不够：
# 检查器「任一段通过即通过」，未标的段仍会放行 null（见票内机制事实 1）。
for r in rows:
    # 注意用 r["slot"]，不要用 i —— i 是本文件更早的循环残留变量
    r["flagged"] = bool(r["segs"]) and all(
        r["slot"] < len(b["entries"]) and "NotNull" in b["entries"][r["slot"]] for b in r["segs"]
    )
flagged_need = [r for r in need if r["flagged"]]
flagged_intent = [r for r in intent if r["flagged"]]

# A 表的「站点」合计：每个字面量站点各计一次（票内「范围」引用的站点数即此口径）。
site_total = sum(len(r["abs"]) for r in need)

# 唯一不带标志的 A 表行是裁定为「有意可空」的例外（票内片 2-A 第 4 行），不是待办；
# 出现别的未带标志行时提示，避免翻转/新增规则时静默漏标。
RULED_NULLABLE = {("DataTableColumnManagementMethod", 1)}
unflagged_need = {(r["cls"], r["slot"]) for r in need if not r["flagged"]}
if unflagged_need != RULED_NULLABLE:
    print("WARN 需标而未带标志的集合与裁定例外不符：", sorted(unflagged_need))

out = []
out.append("# arg-contract/issues/03 逐槽清单（机械预筛 + 片 2 裁定结果）")
out.append("")
out.append("本文件由 [slots-worklist.py](slots-worklist.py) 生成，口径见脚本头注释；票见 [arg-contract/issues/03](issues/03-required-on-omittable-slot.md)。")
out.append("")
out.append("## 计数")
out.append("")
out.append("| 量 | 数 |")
out.append("| --- | --- |")
out.append(f"| 规则块 / 含可省略槽的块 / 可省略槽总数 | {total_blocks} / {len(block_rows)} / {sum(b['argc'] - b['om'] for _, b in block_rows)} |")
out.append(f"| `ArgType.NotNull` 处数（含注释 / 剥注释） | {src.count('ArgType.NotNull')} / {code.count('ArgType.NotNull')} |")
out.append(f"| └ 含 `ArgType.NotNull` 的行数（含注释 / 剥注释） | {sum('ArgType.NotNull' in ln for ln in src.split(chr(10)))} / {sum('ArgType.NotNull' in ln for ln in code.split(chr(10)))} |")
out.append(f"| 规则段旁的空槽语义注释行数（`empty slot`/`omitted slot`，大小写不敏感） | {sum(1 for ln in src.split(chr(10)) if re.search(r'(?i)(?:empty|omitted) slots?', ln))} |")
out.append(f"| `arguments.Required(` 总处数（含注释） | {len(re.findall(r'arguments[.]Required[(]', src))} |")
out.append(f"| 可省略 (类,槽) 全集（不论是否被读取） | {len(all_omittable)} |")
out.append(f"| └ 被 `Required`(字面量) 读取——本表口径 | {len(rows)} |")
out.append(f"| 　├ 剥注释后该槽无 null 处理 ⇒ 需标 `NotNull` | {len(need)}（已带标志 {len(flagged_need)}、裁定可空 {len(need) - len(flagged_need)}，见 D 段） |")
out.append(f"| 　├ 剥注释后该槽有 null 处理 ⇒ 有意候选 | {len(intent)}（片 2-B 裁定：全部为「显式留空」） |")
out.append(f"| 　└ 其中仅注释里有 `== null`（旧手工检查器残留，仍归 `NotNull`） | {len(misreport)} |")
out.append(f"| └ 未被 `Required`(字面量) 读取——E 段补车道 | {len(supplement)} |")
for _i, _c in enumerate(SUPPLEMENT_CATEGORIES):
    out.append(f"| 　{'└' if _i == len(SUPPLEMENT_CATEGORIES) - 1 else '├'} {_c}（E 段逐槽裁定） | {supplement_counts[_c]} |")
out.append("")
out.append(f"A/B 两段的 {len(rows)} 项 + E 段的 {len(supplement)} 项 = 可省略 (类,槽) 全集 {len(all_omittable)} 项。E 段的读取痕迹由脚本机械列出，类别与处置逐槽人工核查（见票内片 2-D）。")
out.append("")
out.append("`null 处理` 识别三种写法（均在剥注释后的文本上统计）：`arguments[i] == null` / `arguments[i]?.` / 列表模式 `arguments is [… not null …]`。")
out.append("")
out.append("`必填段` 非空的槽出现在多个段里：某段要求它必填、另一段允许省略。检查器「任一段通过即通过」（`FunctionMethod.CheckArgumentTypeEx`），故**可省略段仍会放行 null**，`NotNull` 必须加在可省略段上。")
out.append("")
out.append("## A. 需标 `NotNull` 的槽")
out.append("")
out.append("`可省略段` 列出该槽出现的**全部**可省略段（`OmitStart/ArgTypes`）——标志必须铺满这些段。")
out.append("")
out.append("`已带标志` = 该槽的全部可省略段都已带 `ArgType.NotNull`（只标一段仍会从别的段放行 null）。")
out.append("")
out.append("| 类[槽] | 站点（类内方法） | 可省略段 | 必填段 | 类内 helper | 仅注释命中 | 已带标志 |")
out.append("| --- | --- | --- | --- | --- | --- | --- |")
for r in need:
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in r["segs"])
    must = "、".join(f"{b['om']}/{b['argc']}" for b in r["must"]) or "—"
    flag = "是" if r["raw"] else "—"
    flag2 = "是" if r["flagged"] else "—"
    sites = methods_of(r["abs"])
    out.append(f"| `{r['cls']}[{r['slot']}]` | {sites} | {segs} | {must} | {', '.join(r['helpers']) or '—'} | {flag} | {flag2} |")
out.append("")
out.append("## B. 有意候选（片 2-B 裁定：34 项全部为「显式留空」）")
out.append("")
out.append("判空若是防御性写法或注释残留，归 A 表；片 2-B 逐槽取证后**无一项**归 A——34 项的函数体都把 null 与「不传该实参」读成同一个默认值（或走明确分支，如 `GETMETH` 无兜底值时抛 `CodeEE`）。")
out.append("")
out.append("各槽的空槽语义写在 `Creator.Method.cs` 对应规则段旁，由 `CheckArgumentTypeEx.IntentionalEmptySlot_IsAccepted` 逐槽钉住；A 表里的 `DataTableColumnManagementMethod[1]` 同判（NAMES 的空槽 = 写入 RESULTS 数组），故它是 A 表唯一不带 `NotNull` 的行。")
out.append("")
out.append("| 类[槽] | 站点（类内方法） | 可省略段 | 必填段 | 类内 null 处理（方法） |")
out.append("| --- | --- | --- | --- | --- |")
for r in intent:
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in r["segs"])
    must = "、".join(f"{b['om']}/{b['argc']}" for b in r["must"]) or "—"
    sites = methods_of(r["abs"])
    out.append(f"| `{r['cls']}[{r['slot']}]` | {sites} | {segs} | {must} | {methods_of(r['null'])} |")
if flagged_intent:
    out.append("")
    out.append("**冲突**：以下 B 表槽的可省略段已带 `ArgType.NotNull`，与「有意放行 null」相反，须先撤标志或改裁定："
               + "、".join(f"`{r['cls']}[{r['slot']}]`" for r in flagged_intent))
out.append("")
out.append("## C. 多段类（标注时勿漏段）")
out.append("")
multi = {}
for name, b in block_rows:
    multi.setdefault(name, []).append(b)
out.append("| 类 | 可省略段数 | 段（OmitStart/ArgTypes） |")
out.append("| --- | --- | --- |")
for name, bs in sorted(multi.items()):
    if len(bs) > 1:
        segs = "、".join(f"{b['om']}/{b['argc']}" for b in bs)
        out.append(f"| `{name}` | {len(bs)} | {segs} |")
out.append("")
out.append("## E. 补车道：未被 `Required`(字面量) 读取的可省略槽（全集补齐 20 项）")
out.append("")
out.append("这些槽由别的方式读取：helper 传参（`ReadPoint(Name, exm, arguments, argNo)` 之类）、变长下标 `Required(i)`、")
out.append("`arguments[i]` 硬转型、`arguments.Skip(k)` 转发，或函数体根本不读。「读取痕迹」列由脚本机械扫描，")
out.append("「类别 / 处置 / 尺子」三列是逐槽人工核查结论（票内片 2-D）；尺子是「读该槽时是否保证 `Count >= i + 1`」。")
out.append("")
out.append("`已带标志` = 该槽的全部可省略段都在该槽位带 `ArgType.NotNull`；裁定为可空的槽若带标志，脚本会报警。")
out.append("")
out.append("| 类[槽] | 可省略段 | 读取痕迹（机械） | 已带标志 | 类别 | 处置 | 尺子 |")
out.append("| --- | --- | --- | --- | --- | --- | --- |")
for cls, slot in supplement:
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in supplement_segments(cls, slot)) or "—"
    ruling = SUPPLEMENT_RULINGS.get((cls, slot), ("（未裁定）", "—", "—", False))
    mark = "是" if supplement_flagged(cls, slot) else "—"
    out.append(f"| `{cls}[{slot}]` | {segs} | {supplement_evidence(cls, slot)} | {mark} | {ruling[0]} | {ruling[1]} | {ruling[2]} |")
out.append("")
out.append("## D. 状态与余项")
out.append("")
out.append("- [x] **A 表逐槽铺标志（片 2-A + 片 3，已落地）**：`ArgType.DisallowVoid` → `ArgType.NotNull`（改名面 5 处；片 1 新增标注的 3 处写的就是新名，不属改名面），A 表除 `DataTableColumnManagementMethod[1]` 外全部在**每一段**可省略段的该槽位带上标志（只标一段会被别的段放行 null，见机制事实 1）")
out.append("- [x] **B 表 34 项裁定（片 2-B，已落地）**：全部「有意可空」，不标 `NotNull`；规则段旁写明空槽语义，另有逐槽放行用例")
out.append("- [x] 片 1（3 处可达 NRE + 两处数量省略越界 + 用例）已落地")
out.append(f"- [x] **另一把尺子（数量省略越界）**已对 A 表逐点核过：只有 `GraphicsSetFontMethod`、`HotkeyStateMethod` 两处属「少写尾实参即越界」且标 `NotNull` 修不掉（检查器循环只走 `i < Count`）——已抬 `OmitStart` 修掉并补用例；其余站点读 `Required(i)` 时都有保证 `Count >= i+1` 的守卫（`Count > i` / `== i+1` / `>= i+1`，含 `Count < k || …`、`Count == 1 || …` 的短路形态）。本表为 {len(need)} 项 / {site_total} 个站点（站点数按上表「站点」列合计）")
out.append(f"- [x] **同一把尺子对 E 段 {len(supplement)} 项也逐项核过**：无一处「数量省略即越界」，故没有第三处 `OmitStart` 附修；逐项结论见 E 段「尺子」列")
out.append("- [x] 片 2 的三处更正（票面「片 2-A」有记）：① `ReplaceMethod[3]` 被预筛漏在批次外——它同样可达（`REPLACE(\"a\",\"b\",\"c\",,)` 由段 1 的 `OmitStart = 3` 放行 null），已补标 + 补用例；② `DataTableRowSetMethod[1]`（脚本按类内可省略段与站点做笛卡尔匹配时多出的那条）的站点确属 Set 分支（跨构造器假阳性），但 `DT_ROW_ADD` 的槽 1 经 `arguments.Required(pos)`（变量下标，不在本表车道）可达、修前 NRE，已标 `NotNull`；③ `MapGetStrMethod[1]` 只有 `MAP_GETKEYS` 那一段可省略（`MAP_GET`/`MAP_TOXML` 走定长 `argumentTypeArray`），已按名绑定标注。`DataTableColumnManagementMethod[1]` 裁定为可空")
out.append("- [x] **变长尾部按转发型 / 消费型定案（片 2-C，已落地）**：`MaxMethod`（`MAX`/`MIN`）的 `VariadicInt` 与 `ArrayMultiSortMethod`（`ARRAYMSORT`）的变长尾标 `ArgType.NotNull`（函数体逐个循环 / 整表 `Cast` 消费，空槽无默认值可代）；`GetMethMethod`/`GetMethsMethod` 保持可空（`arguments.Skip(2)` 转发给下游）。两型的首槽都由 `i < OmitStart` 拒空，故标志只铺尾部。这三处在「字面量 `Required(`」车道之外，故不影响本表的行数与计数")
out.append(f"- [x] **本表已从 {len(rows)} 扩到 {len(all_omittable)}**（可省略 (类,槽) 全集）：补车道的 {len(supplement)} 项见 E 段——其中「有意可空」的 {supplement_counts['有意可空']} 项已按完成标准第 1 类在规则段旁写明空槽语义（`DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]` 为本次补写），转发槽保持可空不标 `NotNull`，「无意且可达」的 {supplement_counts['无意且可达']} 项已标 `NotNull` 并补用例，「硬转型解引用」的 {supplement_counts['硬转型解引用']} 项归 arg-contract/issues/04 片②")
out.append("- [x] UDF 侧 `UserDefinedRefMethodTerm` 已确认为死代码（见票内「UDF 族」），**不是**本表工作面；活路径的「省略实参 → 取 `FunctionLabelLine.Def` 默认值」已由 `CalledFunctionTests` 一条用例钉住")
out.append("- [ ] 仍未纳入本表：活 `arguments[i]` 站点里落在**非可省略槽**上的那些（硬转型解引用共 24 处，落在可省略槽上的 4 项已见 E 段，其余归 arg-contract/issues/04 片②）。变量下标 `Required(i)` 与 helper 传参路径已随 E 段核过")
out.append("- [ ] 本票仍未做的三件（见票内「余项」4/6/7）：B 表执行期语义抽查、指令侧 `ArgumentBuilder` 的两轴模型定案、`docs/` 侧的内建函数契约表")

open(DST, "w", encoding="utf-8-sig", newline="\n").write("\n".join(out) + "\n")
print("rows", len(rows), "need", len(need), "flagged_need", len(flagged_need),
      "intent", len(intent), "misreport", len(misreport))
