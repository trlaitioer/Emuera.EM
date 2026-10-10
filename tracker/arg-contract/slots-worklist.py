"""arg-contract/issues/03 的逐槽清单生成器。

口径(与票内「范围」一致)：
  · 站点 = `BuiltinMethod/*.cs` 内 `arguments.Required(<字面量>)` 的出现处数（归并到 (类,槽) 后由 A/B 表「站点」列按方法名列出，同一方法里多处记 `×N`；阶段 A 之前这些类都在单文件 `Creator.Method.cs` 里）；
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

用法：python3.14 tracker/arg-contract/slots-worklist.py
产物：tracker/arg-contract/slots-worklist.md（UTF-8 无 BOM，与本脚本的写出编码一致，故可逐字节复现）

**冻结提示**：本表建模的是阶段 A 的「被 `Required`(字面量) 读取」这条车道。arg-contract/issues/05 的阶段 B
把这些消费点全换成了 `ArgumentSlots` 访问器，迁移后再跑本脚本会**覆盖冻结产物**：口径数字必然漂移
（`arguments.Required(` 405 → 26、A/B 两段 76 项 → 2 项、E 段 20 → 94、`ArgType.NotNull` 含注释口径 87/76 → 88/77），
并打出「E 段存在未裁定的可省略槽」等 WARN。产物 .md 头部的冻结声明与上列漂移清单由生成器写出
（`_scan.FREEZE_NOTE_WORKLIST`），不手工维护。故只在阶段 A 的 HEAD 上跑；阶段 B 的验收口径改用 [arg-slots.py](arg-slots.py) 的默认产物
[slots-progress.md](slots-progress.md)。
"""

import re
import sys

from _rulings import DISPOSITION as HARDCAST_DISPOSITION, worklist_rulings
from _scan import (FREEZE_NOTE_WORKLIST, class_spans, line_of, load_sources, method_of, methods_of,
                   not_null_counts, null_handling_points, parse_blocks, strip_keep_lines)

# 扫描源 = `_scan.BUILTIN_METHOD_SRC`（阶段 A 之后 183 个子类分居 BuiltinMethod/*.cs；此前是单文件
# Creator.Method.cs，拼接顺序与拆分前的区域顺序一致）。
DST = r"tracker/arg-contract/slots-worklist.md"
# 片 5 的用例数从测试文件机械数出（不把数字硬编码进产物：用例增删要跟着变；数不到即报警并以非零码退出）。
TEST_SRC = r"Emuera.Tests/Runtime/Script/Statements/Function/IntentionalEmptySlotExecutionTests.cs"
problems = []
try:
    test_src = open(TEST_SRC, encoding="utf-8-sig").read()
except FileNotFoundError:
    test_src = None
    problems.append(f"{TEST_SRC} 缺失，片 5 的用例数无法核对")
case_n = None if test_src is None else (len(re.findall(r"\[Fact\]", test_src))
                                       + len(re.findall(r"\[InlineData\(", test_src)))
if test_src is not None and not case_n:
    problems.append(f"{TEST_SRC} 里数不到用例（[Fact]/[InlineData] 形态变了？）")
    case_n = None
# 成文：核到就写数字，否则写明未核（problems 非空时脚本末尾以非零码退出）
case_txt = f"{case_n} 条用例" if case_n else "用例数未核"

src = load_sources()
lines = src.split("\n")

code = strip_keep_lines(src)
# 与 arg-slots.py 同函数同口径（issues/04 的自校验项之一）
nn = not_null_counts(src)

# 产物里的定位一律写「类内方法名」，不写行号：行号会随任何插入而整体失效。同一方法里有多处时用 ×N 保留「几处」这一事实。
classes = class_spans(src)


rows = []
block_rows = []
total_blocks = 0
all_omittable = set()
blocks_by_class = {}
span_of_class = {}
for name, pos, end in classes:
    blocks = parse_blocks(src, pos, end)
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
        ln = line_of(src, pos + m.start())
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
# 4 个硬转型槽的裁定取自跨脚本单一来源 `_rulings.py`（arg-slots.py 的 A 段引用同一条），故不在此重复。
SUPPLEMENT_RULINGS = {
    ("DataTableSelectMethod", 1): ("有意可空", "规则段注释已写（空槽 = 不筛）", "过（Count > 1）", False),
    ("DataTableSelectMethod", 2): ("有意可空", "规则段注释已写（空槽 = 不排序）", "过（Count > 2）", False),
    ("GraphicsDrawGMethod", 10): ("有意可空", "规则段注释已写（空槽 = 不带颜色矩阵）", "过（Count != 10 时判空短路）", False),
    ("GetMethMethod", 2): ("转发槽", "规则段注释已写（交给下游解释）", "过（Skip(2) 原样转发）", False),
    ("GetMethsMethod", 2): ("转发槽", "规则段注释已写（交给下游解释）", "过（Skip(2) 原样转发）", False),
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
    **worklist_rulings(),
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
        ev.append(f"arguments[{slot}] @ {method_of(src, lines, pos + m.start())}")
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
out.append("本文件由 [slots-worklist.py](slots-worklist.py) 生成，口径见脚本头注释；票见 `arg-contract-03`。")
out.append("")
out.extend(FREEZE_NOTE_WORKLIST)
out.append("")
out.append("## 计数")
out.append("")
out.append("| 量 | 数 |")
out.append("| --- | --- |")
out.append(f"| 规则块 / 含可省略槽的块 / 可省略槽总数 | {total_blocks} / {len(block_rows)} / {sum(b['argc'] - b['om'] for _, b in block_rows)} |")
out.append(f"| `ArgType.NotNull` 处数（含注释 / 剥注释） | {nn['raw_occ']} / {nn['live_occ']} |")
out.append(f"| └ 含 `ArgType.NotNull` 的行数（含注释 / 剥注释） | {nn['raw_lines']} / {nn['live_lines']} |")
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
    sites = methods_of(src, lines, r["abs"])
    out.append(f"| `{r['cls']}[{r['slot']}]` | {sites} | {segs} | {must} | {', '.join(r['helpers']) or '—'} | {flag} | {flag2} |")
out.append("")
out.append("## B. 有意候选（片 2-B 裁定：34 项全部为「显式留空」）")
out.append("")
out.append("判空若是防御性写法或注释残留，归 A 表；片 2-B 逐槽取证后**无一项**归 A——34 项的函数体都把 null 与「不传该实参」读成同一个默认值（或走明确分支，如 `GETMETH` 无兜底值时抛 `CodeEE`）。")
out.append("")
out.append("各槽的空槽语义写在 `BuiltinMethod/*.cs` 对应规则段旁，由 `CheckArgumentTypeEx.IntentionalEmptySlot_IsAccepted` 逐槽钉住；A 表里的 `DataTableColumnManagementMethod[1]` 同判（NAMES 的空槽 = 写入 RESULTS 数组），故它是 A 表唯一不带 `NotNull` 的行。")
out.append("")
out.append("| 类[槽] | 站点（类内方法） | 可省略段 | 必填段 | 类内 null 处理（方法） |")
out.append("| --- | --- | --- | --- | --- |")
for r in intent:
    segs = "、".join(f"{b['om']}/{b['argc']}" for b in r["segs"])
    must = "、".join(f"{b['om']}/{b['argc']}" for b in r["must"]) or "—"
    sites = methods_of(src, lines, r["abs"])
    out.append(f"| `{r['cls']}[{r['slot']}]` | {sites} | {segs} | {must} | {methods_of(src, lines, r['null'])} |")
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
out.append(f"- [x] **本表已从 {len(rows)} 扩到 {len(all_omittable)}**（可省略 (类,槽) 全集）：补车道的 {len(supplement)} 项见 E 段——其中「有意可空」的 {supplement_counts['有意可空']} 项已按完成标准第 1 类在规则段旁写明空槽语义（`DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]` 为本次补写），转发槽保持可空不标 `NotNull`，「无意且可达」的 {supplement_counts['无意且可达']} 项已标 `NotNull` 并补用例，「硬转型解引用」的 {supplement_counts['硬转型解引用']} 项{HARDCAST_DISPOSITION}")
out.append("- [x] UDF 侧 `UserDefinedRefMethodTerm` 已确认为死代码（见票内「UDF 族」），**不是**本表工作面；活路径的「省略实参 → 取 `FunctionLabelLine.Def` 默认值」已由 `CalledFunctionTests` 一条用例钉住")
out.append("- [ ] 仍未纳入本表：活 `arguments[i]` 站点里落在**非可省略槽**上的那些（硬转型解引用共 24 处，落在可省略槽上的 4 项已见 E 段，其余归 arg-contract/issues/05（cast 的类型化移除））。变量下标 `Required(i)` 与 helper 传参路径已随 E 段核过")
out.append(f"- [x] **片 5（B 表执行期语义）与片 6（指令侧两轴模型定案）已落地**：片 5 的用例在 `{TEST_SRC}`（{case_txt}；覆盖了 B 表哪些槽、各项钉住的默认值、未覆盖槽的理由均见 issues/03 片 5，逐槽证据留在票内而不在此处硬编码）；片 6 的工件是 [instruction-args.py](instruction-args.py) → [instruction-args.md](instruction-args.md)（不并入本表——它扫的是 `ArgumentBuilder.cs`，口径与本站点清单不同）")
out.append("- [ ] 本票仍未做的一件（见票内「余项」7）：`docs/` 侧的内建函数契约表")

open(DST, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("rows", len(rows), "need", len(need), "flagged_need", len(flagged_need),
      "intent", len(intent), "misreport", len(misreport))
# 自校验失败：产物照写（人工仍可看），但脚本以非零码退出，别让复跑看起来"通过"
for p in problems:
    print("warning:", p, file=sys.stderr)
if problems:
    sys.exit(1)
