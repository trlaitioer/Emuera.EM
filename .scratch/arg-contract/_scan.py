"""三个 arg-contract 生成器（`slots-worklist.py` / `arg-slots.py` / `instruction-args.py`）共用的扫描原语。

用途：把三份重复的「剥注释 / 保留行」扫描、类与方法签名正则、方法归因、规则段（`ArgTypeList`）切分、
槽位判空点识别、`ArgType.NotNull` 计数收在一处。三份实现此前各自漂移，收拢时**不统一语义**，
有差异的地方一律做成显式参数，产物才能逐字节不变。

口径差异（写在参数上，改这里会改产物字节）：
  · `strip_keep_lines(text, strip_regions=False)`：只剥 `//` 与 `/* */`；`#` 开头的行
    （`#region`/`#endregion`/`#pragma`）仅在 `strip_regions=True` 时连内容抹成空格。
    `instruction-args.py` 传 `True`（它要在 `ArgumentBuilder` 子类的类体文本里切方法体，
    `#region` 行会干扰）；`slots-worklist.py` 与 `arg-slots.py` 传 `False`（原实现就只剥注释）。
  · `METHOD_SIG_RE` / `METHOD_SIG_RE_PLAIN`：前者允许行首属性前缀 `[...]`，供 `slots-worklist.py`
    与 `arg-slots.py` 逐行 `match` 用；后者不许属性前缀，供 `instruction-args.py` 在类体上
    `finditer`（`^` 锚点 + `re.M`）用。两者对两族现行文本结果一致，仍分开放置以免被无意统一。
  · 剥注释是逐行朴素实现（不识别字符串字面量里的 `//`），命中处仍需人工确认。
  · 所有函数的字符位置都指向**传入文本**的坐标系（剥注释不改长度，故位置可与原文互认）。
"""

import re

# ── 类签名：三族的嵌套类都是 `\t` 缩进的 `sealed class` ──────────────────────────────
CLASS_RE = re.compile(r"^\t(?:public |private |internal )?sealed class (\w+)\s*:", re.M)

# ── 方法签名：只认「<\t\t> 访问修饰符 … 名字 (」形态，否则 `throw new CodeEE(` 一类语句也会命中 ──
_METHOD_SIG_BODY = (
    r"(?:(?:static|override|virtual|sealed|async|partial|new|unsafe|extern)\s+)*"
    r"(?:[\w<>\[\],?.]+\s+)?(\w+)\s*\("
)
METHOD_SIG_RE = re.compile(
    r"^\t\t(?:\[[^\]]*\]\s*)?(?:public|private|protected|internal)\s+" + _METHOD_SIG_BODY
)
# 供 `finditer` 在类体上扫（`^` 需 `re.M` 才逐行生效；上面那份是逐行 `match`，不需要）
METHOD_SIG_RE_PLAIN = re.compile(
    r"^\t\t(?:public|private|protected|internal)\s+" + _METHOD_SIG_BODY, re.M
)
METHOD_KEYWORDS = {"if", "for", "while", "switch", "return", "throw", "catch", "using", "lock",
                   "foreach", "else", "new", "get", "set"}


def strip_keep_lines(text, strip_regions=False):
    """把注释（可选：`#` 指令行）抹成空格，保留长度与换行——剥注释文本的位置可与原文互认。"""
    out = list(text)
    if strip_regions:
        # `#region`/`#endregion`/`#pragma` 不是注释语法，却会被当成类体内容
        for m in re.finditer(r"(?m)^[ \t]*#[^\n]*", text):
            for k in range(m.start(), m.end()):
                if out[k] != "\n":
                    out[k] = " "
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


def line_of(text, pos):
    """pos 的行号（1 起）。"""
    return text.count("\n", 0, pos) + 1


def class_spans(text):
    """`sealed class` 的 (名字, 起点, 终点)：终点 = 下一个类起点或文本末尾。"""
    marks = [(m.start(), m.group(1)) for m in CLASS_RE.finditer(text)]
    return [(name, pos, marks[idx + 1][0] if idx + 1 < len(marks) else len(text))
            for idx, (pos, name) in enumerate(marks)]


def method_marks(text, regex=METHOD_SIG_RE):
    """方法签名的 (位置, 方法名)；剔掉语言关键字（`if`/`throw` 等语句也满足上面的正则）。"""
    return [(m.start(), m.group(1)) for m in regex.finditer(text)
            if m.group(1) not in METHOD_KEYWORDS]


def method_of(text, lines, pos):
    """pos 所属的类内方法名（本族文件的类成员均为两个 Tab 缩进）。

    产物里的定位一律写方法名而不写行号：行号会随任何插入整体失效。
    """
    for k in range(line_of(text, pos) - 1, -1, -1):
        m = METHOD_SIG_RE.match(lines[k])
        if m and m.group(1) not in METHOD_KEYWORDS:
            return m.group(1)
    return "—"


def methods_of(text, lines, positions):
    """把若干位置折成「方法名×N」，按首次出现排序（`lines` = `text.split("\\n")`）。"""
    names = [method_of(text, lines, p) for p in positions]
    return "、".join(
        "%s×%d" % (nm, names.count(nm)) if names.count(nm) > 1 else nm
        for nm in dict.fromkeys(names)
    ) or "—"


def null_handling_points(text, base_pos, slot):
    """text 内针对槽 slot 的判空点（返回**原始文本**坐标系的位置，故加上 base_pos）。

    识别三种写法：`arguments[i] == null` / `!= null` / `is null`、`arguments[i]?.`、
    列表模式 `arguments is [… not null …]`（`not null` 在模式里的位置即槽号）。
    只认第一种时，用列表模式判空的「有意留空」槽会被误判。
    """
    hits = []
    for pat in (
        rf"arguments\[{slot}\]\s*(?:==|!=)\s*null",
        rf"arguments\[{slot}\]\s+is\s+(?:not\s+)?null",
        rf"arguments\[{slot}\]\s*\?\.",
    ):
        hits += [base_pos + m.start() for m in re.finditer(pat, text)]

    for m in re.finditer(r"arguments is \[([^\]]*)\]", text):
        parts = [p.strip() for p in m.group(1).split(",")]
        if slot < len(parts) and parts[slot] == "not null":
            hits.append(base_pos + m.start())

    return sorted(set(hits))


def parse_blocks(src, pos, end):
    """`src[pos:end]` 内的 `new ArgTypeList { … }` 段（类体范围）。

    返回 dict：`om` = `OmitStart`（未写 = -1）、`entries` = 逐槽位 `ArgType` 表达式、
    `argc` = 槽位数、`line` = 该段在 src 里的行号。
    `entries` 是逐槽位的表达式——`NotNull` 只对写出它的那个槽位生效，必须逐槽位判定
    （整块级的「任何一处有」会把同块其他槽一起算成已标）。
    """
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
        out.append({"om": om, "argc": argc, "entries": entries, "line": line_of(src, pos + m.start())})
    return out


# ── `ArgType.NotNull` 计数（issues/04 的自校验项之一）─────────────────────────────────
NOT_NULL_FLAG = "ArgType.NotNull"
# `Creator.Method.cs` 的期望值：(含注释 处数, 含注释 行数, 剥注释 处数, 剥注释 行数)。
# 两套口径不同是因为注释里也提到过该标志；数值变化只在改 `Creator.Method.cs` 时发生，届时同步此处与票面。
NOT_NULL_EXPECTED = (88, 77, 81, 70)


def not_null_counts(text, strip_regions=False):
    """`ArgType.NotNull` 的计数：含注释与剥注释两套，各给 (处数, 行数)。

    剥注释口径 = `strip_keep_lines(text, strip_regions)`。「处数」按子串出现次数（`count`），
    「行数」按含该子串的行数。`arg-slots.py` 与 `slots-worklist.py` 必须都走这个函数，
    否则两边口径会各自漂移（本项正是 issues/04 承诺的自校验点）。
    """
    live = strip_keep_lines(text, strip_regions)
    return {
        "raw_occ": text.count(NOT_NULL_FLAG),
        "raw_lines": sum(NOT_NULL_FLAG in ln for ln in text.split("\n")),
        "live_occ": live.count(NOT_NULL_FLAG),
        "live_lines": sum(NOT_NULL_FLAG in ln for ln in live.split("\n")),
    }
