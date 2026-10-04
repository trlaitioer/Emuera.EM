"""内建函数（式中関数）契约表生成器。

产物：`docs/builtin-functions.md`（每个注册名的实参形状/返回/合法实数/空槽语义/说明）+ 控制台的缺口报告。

口径：
  · 注册名 = `BuiltinMethod/` 目录下各类体上的 `[BuiltinMethod("NAME")]` 工厂（live 计数 266，脚本自校验）；
    每个注册名的构造实参取自同一行的工厂体（阶段 A/C 之前这里读的是 `Creator.cs` 的字面量表）。
  · 每个注册名经**构造器实参**解析到该名字真正使用的规则表：`BuiltinMethod/` 的构造器体被当作
    一小段可执行代码走一遍（if/else-if/else、switch/case 落空贯穿、`a ? b : c` 三元），条件里的
    `type == Operation.Add` 与注册点名传入的 `XxxMethod.Operation.Add` 按最后一段比较。这样
    「一个类多个构造器/多段规则」的类不必手工登记映射（形状全部来自代码）。
  · 规则段的逐槽标志按 `FunctionMethod.cs` 的 `ArgType` 枚举**从源码解析出的位值**解码（不手抄常量），
    槽位记号见产物「凡例」。
  · 「合法实参数」按 `FunctionMethod.CheckArgumentTypeEx` 的判据机械推出：
    非变长段必须 `min <= Count <= ArgTypes.Count`（`min` = `OmitStart`，未写则 `ArgTypes.Count`）；
    变长段只要求 `Count >= min`；`MatchVariadicGroup` 另加 `(Count - 变长组起点) % 变长组长 == 0`。
  · 「空槽语义」来自 `tracker/arg-contract/builtin-notes.json`（人工/子 Agent 编写的说明，参考
    upstream 在线文档并按代码核对）。缺说明的名字在产物里写 `（待补）`，脚本在控制台报告缺口。

用法：python3.14 tracker/arg-contract/builtin-contract.py            # 写 docs/builtin-functions.md
      python3.14 tracker/arg-contract/builtin-contract.py --dump    # 只打印解析结果（调试）
      python3.14 tracker/arg-contract/builtin-contract.py --merge   # 合并 .temp/notes/*.json 进 notes
"""

import glob
import json
import os
import re
import sys

from _scan import class_spans, load_sources, parse_blocks, strip_keep_lines

# 阶段 A/C（backlog/issues/18）把 183 个活子类搬出 Creator.Method.cs、把注册名搬成类体上的 attribute 工厂，
# 故注册名与规则表/函数体一律由 `_scan.load_sources()` 扫描（顺序 = 拆分前的区域顺序，
# 见 `_scan.BUILTIN_METHOD_SRC`）；`Creator.cs` 只剩建表逻辑，不再有字面量可读。
FUNCTION_SRC = "Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs"
NOTES_SRC = "tracker/arg-contract/builtin-notes.json"
FRAGMENT_DIRS = (".temp/notes", ".temp/notes2")   # 第一轮（按 upstream 页面分桶）/ 第二轮（索引命不中的补漏）
DST = "docs/builtin-functions.md"

EXPECTED_NAMES = 266         # [BuiltinMethod] 工厂数 == 注册名数（改动即在此同步）
EXPECTED_CLASSES = 183       # 被注册的 FunctionMethod 子类数
EXPECTED_BLOCKS = 114        # BuiltinMethod/ 内 `new ArgTypeList` 总数
# 拒绝侧不变量（arg-contract/issues/03 的裁定值）。这两个数字的权威副本就是这里：
# 票据/文档不再各自维护一份，改为引用本脚本自校验打印的那一行。
EXPECTED_OMITTABLE = 96      # 可省略 (类,槽) 全集（不论是否被 Required 读取）
EXPECTED_REJECT_EMPTY = 54   # 其中「全部可省略段都带 ArgType.NotNull」的 (类,槽) 数

# 没有规则表、由类自己 override `CheckArgumentType` 的注册名：逐槽形状读不出来，只能人工裁定。
# 依据 `MesSkipMethod`：`argumentTypeArray = null`，自写的检查只做「实参多于 0 个报 TooManyFuncArgs」。
# 将来又多出/少掉这类名字，自校验会报，届时补这里。
NO_TABLE_RULED = {
    "MESSKIP": {"shape": "（无实参）", "count": "恰好 0"},
    "MOUSESKIP": {"shape": "（无实参）", "count": "恰好 0"},
}

# upstream 在线文档 Reference 的分类顺序（产物按此排序；不在表内的分类排在后面，按名字排序）
CATEGORY_ORDER = [
    "PRINT 系",
    "显示操作・字体操作・显示规格参照",
    "字符串操作・参照",
    "算术",
    "角色操作・参照",
    "变量操作・变量参照・CSV 参照",
    "存档数据操作",
    "日期・时刻获取",
    "输入・等待",
    "循环・分支语法",
    "随机数控制",
    "调试辅助・系统流程控制",
    "函数系（CALL 等）",
    "RETURN 系",
    "DEBUG 系",
    "工具提示系",
    "HTML 系",
    "AWAIT 相关",
    "图像处理相关",
    "声音系",
    "XML 系",
    "MAP（关联数组）系",
    "DataTable（数据库）系",
    "其他",
]

problems = []


# ── 通用扫描原语 ──────────────────────────────────────────────────────────────
def match_paren(text, i0):
    """text[i0] == '(' 时返回配对 ')' 的下标。"""
    depth = 0
    for i in range(i0, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return i
    raise ValueError("括号不配对")


def match_brace(text, i0):
    depth = 0
    for i in range(i0, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return i
    raise ValueError("花括号不配对")


_OPEN = {"(": ")", "[": "]", "{": "}"}
_CLOSE = {v: k for k, v in _OPEN.items()}


def find_top(text, ch, start=0, end=None):
    """在括号/方括号/花括号深度 0 处找字符 ch（找不到返回 -1）。"""
    depth = 0
    i = start
    end = len(text) if end is None else end
    while i < end:
        c = text[i]
        if c in _OPEN:
            depth += 1
        elif c in _CLOSE:
            depth -= 1
        elif c == ch and depth == 0:
            return i
        i += 1
    return -1


def split_top(text, sep=","):
    """按深度 0 的分隔符切分（用于实参表、槽位表达式列表）。"""
    out, start = [], 0
    while True:
        i = find_top(text, sep, start)
        if i < 0:
            out.append(text[start:])
            break
        out.append(text[start:i])
        start = i + 1
    return [x.strip() for x in out if x.strip()]


# ── ArgType 枚举（从源码解析位值，避免手抄）────────────────────────────────────
def parse_argtype(enum_text):
    m = re.search(r"protected enum ArgType\s*\{(.*?)\n\t\}", enum_text, re.S)
    if not m:
        raise SystemExit("找不到 ArgType 枚举定义")
    entries = []
    for mm in re.finditer(r"(?m)^\s*(\w+)\s*=\s*([^,\n]+?)\s*,?\s*$", m.group(1)):
        entries.append((mm.group(1), mm.group(2).strip()))
    vals = {}
    for _ in range(4):  # 复合成员可能引用后面才解析出的成员，多过几遍
        for name, expr in entries:
            v = eval_bits(expr, vals)
            if v is not None:
                vals[name] = v
    missing = [n for n, _ in entries if n not in vals]
    if missing:
        problems.append(f"ArgType 成员未解析出位值：{missing}")
    return vals


def eval_bits(expr, vals):
    total = 0
    for tok in expr.split("|"):
        tok = tok.strip()
        if tok in vals:
            total |= vals[tok]
        elif re.fullmatch(r"\d+", tok):
            total |= int(tok)
        elif re.fullmatch(r"1\s*<<\s*\d+", tok):
            total |= 1 << int(re.fullmatch(r"1\s*<<\s*(\d+)", tok).group(1))
        elif tok.startswith("(") and tok.endswith(")"):
            inner = eval_bits(tok[1:-1], vals)
            if inner is None:
                return None
            total |= inner
        else:
            return None
    return total


# ── 注册名：`[BuiltinMethod("NAME")]` 工厂（阶段 C 之后名字只写在这里）──────────────────
#     [BuiltinMethod("MIN")]
#     private static MaxMethod CreateMIN()
#     {
#         return new MaxMethod(false);
#     }
# 只钉住「attribute 与工厂签名相邻」这一条形态约定：缩进、工厂可见性（private/internal 皆可，见
# BuiltinMethodAttribute.cs）与工厂体写法（表达式体 `=> new(...)` 或块体 `{ return new(...); }`）都不进匹配，
# 构造实参用括号配对从 `new(` 取出。任何漏匹配由 main() 的 attribute 处数交叉校验报出来。
_FACTORY_RE = re.compile(
    r'(?m)^\t*\[BuiltinMethod\("([^"]+)"\)\]\n'
    r'\t*(?:private|internal|public) static (\w+) \w+\(\)')
# 工厂签名与 `new(` 之间只允许：空白、`=> `、或块体的 `{` + 可选 `return`
_FACTORY_GAP_RE = re.compile(r'^\s*(?:=>[ \t]*|\{[ \t]*\n[ \t]*(?:return[ \t]+)?)?$')
# 构造调用：`new Xxx(...)`（`new` 与类型名、类型名与 `(` 之间都可能有空白）
_FACTORY_NEW_RE = re.compile(r'\bnew\s+[\w.]+(?:<[^<>()]*>)?[ \t]*\(')


def parse_registrations(text):
    """按工厂方法解析注册名 → (名字, 实现类, 构造实参)：与注册层扫描的是同一处声明。"""
    out = []
    for m in _FACTORY_RE.finditer(text):
        start = m.end()
        call = _FACTORY_NEW_RE.search(text, start)
        if not call:
            continue
        # `new(` 必须紧接工厂签名（中间只允许 `=>` 或块体开头），否则它属于工厂后面的构造器/方法体
        if not _FACTORY_GAP_RE.match(text[start:call.start()]):
            continue
        paren = call.end() - 1
        c = match_paren(text, paren)
        if not re.match(r'[ \t]*;', text[c + 1:]):
            continue
        out.append({
            "name": m.group(1),
            "cls": m.group(2),
            "args": split_top(text[paren + 1:c]),
            "line": text.count("\n", 0, m.start()) + 1,
        })
    return out


# ── 构造器的小型解释器 ────────────────────────────────────────────────────────
def enum_tail(value):
    """`DataTableCellGetMethod.Operation.Gets` -> `Gets`；`true`/`false` 原样。"""
    return value.split(".")[-1].strip() if "." in value else value.strip()


def eval_value(rhs, env):
    rhs = rhs.strip()
    if rhs in env:
        return env[rhs]
    return rhs


def cond_holds(cond, env):
    """够用即可的条件求值：|| / && / ! / == / != / 裸标识符（含 true/false）。"""
    cond = cond.strip()
    i = find_top(cond, "|")  # 这里只在 depth 0 找 '|'；'||' 与位或都够用
    parts = [p for p in re.split(r"\|\|", cond)]
    if len(parts) > 1:
        return any(cond_holds(p, env) for p in parts)
    parts = re.split(r"&&", cond)
    if len(parts) > 1:
        return all(cond_holds(p, env) for p in parts)
    cond = cond.strip()
    if cond.startswith("!"):
        return not cond_holds(cond[1:], env)
    for op in ("==", "!="):
        k = find_top(cond, op[0])
        if k > 0 and cond[k:k + 2] == op:
            lhs = enum_tail(eval_value(cond[:k], env))
            rhs = enum_tail(eval_value(cond[k + 2:], env))
            return (lhs == rhs) if op == "==" else (lhs != rhs)
    if cond in ("true", "false"):
        return cond == "true"
    if cond in env:
        return str(env[cond]).strip() == "true"
    return False


def parse_stmt(text, i):
    """从 i 起吃一个语句或块；返回 (内容, 下一位置, 是不是块)。"""
    i = skip_ws(text, i)
    if i < len(text) and text[i] == "{":
        j = match_brace(text, i)
        return text[i + 1:j], j + 1, True
    end = text.find(";", i)
    if end < 0:
        end = len(text)
    return text[i:end], end + 1, False


def skip_ws(text, i):
    while i < len(text) and text[i].isspace():
        i += 1
    return i


def run_body(body, env, out):
    """把构造器体当代码走一遍，收集关心的赋值（out: prop -> rhs 文本）。"""
    i = 0
    while True:
        i = skip_ws(body, i)
        if i >= len(body):
            return
        c = body[i]
        if body.startswith("if", i) and re.match(r"if\s*\(", body[i:]):
            o = body.index("(", i)
            cond = body[o + 1:match_paren(body, o)]
            then_text, j, _ = parse_stmt(body, match_paren(body, o) + 1)
            taken = False
            if cond_holds(cond, env):
                run_body(then_text, env, out)
                taken = True
            while True:
                k = skip_ws(body, j)
                if not body.startswith("else", k):
                    break
                k = skip_ws(body, k + 4)
                if body.startswith("if", k):
                    o2 = body.index("(", k)
                    cond2 = body[o2 + 1:match_paren(body, o2)]
                    t2, j, _ = parse_stmt(body, match_paren(body, o2) + 1)
                    if not taken and cond_holds(cond2, env):
                        run_body(t2, env, out)
                        taken = True
                else:
                    t2, j, _ = parse_stmt(body, k)
                    if not taken:
                        run_body(t2, env, out)
                        taken = True
            i = j
        elif body.startswith("switch", i) and re.match(r"switch\s*\(", body[i:]):
            o = body.index("(", i)
            subject = body[o + 1:match_paren(body, o)]
            brace = skip_ws(body, match_paren(body, o) + 1)
            end = match_brace(body, brace)
            inner = body[brace + 1:end]
            subject_value = enum_tail(eval_value(subject, env))
            # 按 case/default 标签分组（case A: case B: 共享同一段代码）
            labels = list(re.finditer(r"(?m)^\s*(?:case\s+([^:]+)|(default))\s*:", inner))
            groups = []
            for idx, m in enumerate(labels):
                stop = labels[idx + 1].start() if idx + 1 < len(labels) else len(inner)
                groups.append((m.group(1), inner[m.end():stop]))
            # case 标签是分组边界，`case A: case B: 代码` 的两个标签各成一组（A 组代码为空），
            # 故落空贯穿要单独跟一遍：某组运行过且没有 break，下一组就继承它的匹配结果。
            prev_matched, prev_fell, any_case = False, False, False
            for label, code in groups:
                if label is None:
                    matched = not any_case
                else:
                    matched = enum_tail(str(eval_value(label, env))) == subject_value
                    any_case = any_case or matched
                effective = matched or (prev_matched and prev_fell)
                if effective:
                    run_body(code, env, out)
                    if "break" in code:
                        break
                prev_matched, prev_fell = effective, "break" not in code
            i = end + 1
        elif body.startswith("for", i) or body.startswith("while", i) or body.startswith("foreach", i):
            i = skip_ws(body, i)
            o = body.index("(", i)
            stmt, j, _ = parse_stmt(body, match_paren(body, o) + 1)
            i = j
        elif body.startswith("return", i) or body.startswith("throw", i):
            end = body.find(";", i)
            i = len(body) if end < 0 else end + 1
        else:
            end = find_top(body, ";", i)
            if end < 0:
                return
            stmt = body[i:end]
            m = re.match(r"\s*(\w+)\s*=\s*(.+)$", stmt, re.S)
            if m:
                prop, rhs = m.group(1), " ".join(m.group(2).split())
                if prop in ("argumentTypeArray", "argumentTypeArrayEx", "ReturnType"):
                    out[prop] = rhs
                else:
                    env[prop] = eval_value(rhs, env)
            i = end + 1


# ── 类与构造器 ────────────────────────────────────────────────────────────────
def class_ctors(code, cls):
    spans = dict((n, (p, e)) for n, p, e in class_spans(code))
    if cls not in spans:
        return None
    pos, end = spans[cls]
    body = code[pos:end]
    ctors = []
    # 类成员缩进随族的布局（阶段 A 之后 BuiltinMethod/ 是 1 个 Tab，此前是 2 个）
    for m in re.finditer(r"^\t+(?:public|private|internal)\s+%s\s*\(" % re.escape(cls), body, re.M):
        o = body.index("(", m.end() - 1)
        c = match_paren(body, o)
        params = split_top(body[o + 1:c])
        # 构造器初始化器（`: this(...)` / `: base(...)`）：C# 会先跑被委派的那个构造器，而
        # `ReturnType` 之类常只写在无参重载里（`XmlGetMethod(bool byname) : this()`），故记下来先跑。
        delegate, j = None, skip_ws(body, c + 1)
        while j < len(body) and body[j] == ":":
            o2 = body.find("(", j)
            if o2 < 0:
                break
            if body[j + 1:o2].strip().endswith("this"):
                argtext = body[o2 + 1:match_paren(body, o2)].strip()
                delegate = split_top(argtext) if argtext else []
            j = skip_ws(body, match_paren(body, o2) + 1)
        brace = j
        if brace >= len(body) or body[brace] != "{":
            continue  # 声明与实现分离（本文件不存在这种写法）
        close = match_brace(body, brace)
        ctors.append({"params": params, "body": body[brace + 1:close], "delegate": delegate})
    return ctors


def bind_ctor(ctors, args):
    """按实参个数挑构造器，返回 env（形参名 -> 实参文本）。"""
    for ctor in ctors:
        names = []
        required = 0
        for p in ctor["params"]:
            parts = p.split("=")
            names.append(parts[0].strip().split()[-1])
            if len(parts) == 1:
                required += 1
        if required <= len(args) <= len(names):
            env = {}
            for idx, nm in enumerate(names):
                if idx < len(args):
                    env[nm] = args[idx]
            return ctor, env
    return None, {}


# ── 规则段解码 ────────────────────────────────────────────────────────────────
def decode_rule_table(rhs, kind, flags):
    """规则表 -> [block]。kind: 'Ex' 为 ArgTypeList[]，'Arr' 为 Type[]。"""
    rhs = rhs.strip()
    if rhs in ("null", "null!"):
        return None
    if kind == "Arr":
        types = re.findall(r"typeof\((\w+)\)", rhs)
        decoded = []
        for t in types:
            if t in ("long", "int", "Int64"):
                decoded.append(flags["Int"])
            elif t == "string":
                decoded.append(flags["String"])
            else:
                problems.append(f"{rhs}：Type[] 里的类型 {t} 未登记")
                decoded.append(0)
        return [{"om": -1, "argc": len(decoded), "entries": types, "flags": decoded,
                 "rejects_empty": [True] * len(decoded), "match_variadic_group": False}]
    blocks = parse_blocks(rhs, 0, len(rhs))
    if not blocks:
        return None
    bodies = []
    for m in re.finditer(r"new ArgTypeList\s*\{", rhs):
        bodies.append(rhs[m.end():match_brace(rhs, m.end() - 1)])
    for b, body in zip(blocks, bodies):
        b["match_variadic_group"] = "MatchVariadicGroup = true" in body
        decoded = []
        for e in b["entries"]:
            v = 0
            ok = True
            for tok in split_top(e, "|"):
                tok = tok.strip()
                if tok.startswith("ArgType."):
                    tok = tok[len("ArgType."):]
                if tok in flags:
                    v |= flags[tok]
                else:
                    problems.append(f"槽位表达式无法解码：{e}")
                    ok = False
            decoded.append(v)
        b["flags"] = decoded
        b["rejects_empty"] = [
            b["om"] < 0 or i < b["om"] or (v & flags["NotNull"]) != 0
            or (i == 0 and b["om"] == 0 and any(x & flags["SameAsFirst"] for x in decoded))
            for i, v in enumerate(decoded)
        ]
    return blocks


def slot_text(value, flags, omittable, rejects_empty, variadic):
    if value & flags["SameAsFirst"]:
        t = "随槽0"          # 类型跟随 0 号槽（ArgType.SameAsFirst），不与「可省略」的 `?` 混用
    elif value & flags["Any"]:
        t = "any"
    elif value & flags["String"]:
        t = "str"
    elif value & flags["Int"]:
        t = "int"
    else:
        t = "?"
    dims = ("[,,]" if value & flags["Array3D"] else "[,]" if value & flags["Array2D"]
            else "[]" if value & flags["Array1D"] else "[…]" if value & flags["Array"] else "")
    if value & flags["Ref"]:
        # `ref*`：该槽还接受 const 变量（ArgType.AllowConstRef），字面量与表达式照样被拒
        t = ("ref* " if value & flags["AllowConstRef"] else "ref ") + t
    t += dims
    # 角色变量用的是独立位（1<<10），不能用复合成员 CharacterData（它含 Ref 位，会把所有引用位都算进来）
    if value & (1 << 10):
        t += " (角色变量)"
    if variadic:
        t += "…"
    if omittable:
        t += "?"
    if not rejects_empty:
        t += "-"
    return t


def count_text(block, flags):
    entries = block["flags"]
    argc = len(entries)
    last_variadics = 0
    for e in reversed(entries):
        if e & flags["Variadic"]:
            last_variadics += 1
        else:
            break
    minimum = block["om"] if block["om"] >= 0 else argc
    if last_variadics:
        txt = f">= {minimum}" if block["om"] >= 0 else f">= {argc}"
        if block.get("match_variadic_group") and last_variadics:
            start = argc - last_variadics
            txt += f"，且超出 {start} 的部分按每 {last_variadics} 个一组（(n-{start})%{last_variadics}=0）"
        return txt
    if block["om"] >= 0:
        # OmitStart 落在末位之后（等于槽数）时一个槽都省不掉，与 om == -1 同样是「恰好 argc 个」
        return f"{minimum}–{argc}" if minimum != argc else f"恰好 {argc}"
    return f"恰好 {argc}"


def blocks_of_class(code, cls, name, args, flags):
    ctors = class_ctors(code, cls)
    if not ctors:
        problems.append(f"{cls}：找不到构造器")
        return None, None
    ctor, env = bind_ctor(ctors, args)
    if ctor is None:
        problems.append(f"{cls}：注册名 {name} 的实参 {args} 匹配不到构造器")
        return None, None
    out = {}
    # C# 语义：先跑初始化器委派的那个构造器（作为基线），再跑本构造器（体内赋值覆盖基线）。
    # 只跟一层：本文件里最深就是 `Xxx(bool) : this()`。委派实参按同一套 arity 规则挑重载，
    # 挑回自身时跳过（否则等于跑两遍自己）。
    if ctor.get("delegate") is not None:
        base, base_env = bind_ctor(ctors, ctor["delegate"])
        if base is not None and base is not ctor:
            run_body(base["body"], base_env, out)
    run_body(ctor["body"], env, out)
    ret = eval_ternary(out.get("ReturnType", ""), env)
    ret = {"typeof(long)": "int", "typeof(string)": "str"}.get(ret, ret or None)
    if "argumentTypeArrayEx" in out:
        rhs = out["argumentTypeArrayEx"]
        kind = "Ex"
    elif "argumentTypeArray" in out:
        rhs = out["argumentTypeArray"]
        kind = "Arr"
    else:
        return ret, None
    # 三元表达式：cond ? a : b
    rhs = eval_ternary(rhs, env)
    return ret, decode_rule_table(rhs, kind, flags)


def eval_ternary(rhs, env):
    """`cond ? a : b` -> 取到的那一支（没有三元就原样返回）。"""
    if not rhs:
        return rhs
    q = find_top(rhs, "?")
    if q <= 0:
        return rhs
    cond = rhs[:q].strip()
    rest = rhs[q + 1:]
    colon = find_top(rest, ":")
    left, right = rest[:colon].strip(), rest[colon + 1:].strip()
    return left if cond_holds(cond, env) else right


# ── 说明文件（notes）─────────────────────────────────────────────────────────
def load_notes():
    if not os.path.exists(NOTES_SRC):
        return {}
    with open(NOTES_SRC, encoding="utf-8") as f:
        return json.load(f)


# upstream 索引页的分类（日文原文，含简中版导航的中文译名）-> 本表用的中文分类名。
# 子 Agent 交回的说明里可能写日文原名或旧译名，统一在这里折叠，避免同一个分类被拆成两节。
CATEGORY_ALIAS = {
    "表示操作・フォント操作・表示仕様参照": "显示操作・字体操作・显示规格参照",
    "文字列操作・参照": "字符串操作・参照",
    "算術": "算术",
    "キャラ操作・参照": "角色操作・参照",
    "変数操作・変数参照・CSV 参照": "变量操作・变量参照・CSV 参照",
    "セーブデータ操作": "存档数据操作",
    "日付・時刻取得": "日期・时刻获取",
    "入力・ウェイト": "输入・等待",
    "ループ・分岐構文": "循环・分支语法",
    "乱数制御": "随机数控制",
    "デバッグ補助・システムフロー制御": "调试辅助・系统流程控制",
    "関数系（CALL 等）": "函数系（CALL 等）",
    "ツールチップ系": "工具提示系",
    "AWAIT 関連": "AWAIT 相关",
    "画像処理関連": "图像处理相关",
    "サウンド系": "声音系",
    "MAP（連想配列）系": "MAP（关联数组）系",
    "DataTable（データベース）系": "DataTable（数据库）系",
    "その他": "其他",
}


def norm_category(cat):
    return CATEGORY_ALIAS.get(cat, cat)


def note_of(notes, name):
    note = notes.get("functions", {}).get(name)
    if note and note.get("category"):
        note = dict(note)
        note["category"] = norm_category(note["category"])
    return note


def merge_fragments():
    notes = load_notes()
    funcs = notes.setdefault("functions", {})
    # 注册名以 `[BuiltinMethod]` 工厂为准：交回的片段可能写 upstream 的拼写（例如把 CBGREMOVEBMAP 写成 CBGREMOVEMAPB），
    # 这类名字直接拒收并报出来，免得产物里出现查不到的名字。
    live = {r["name"] for r in parse_registrations(load_sources())}
    # 片段是文本的唯一来源，notes 是它的派生物：同名条目以片段为准改写，
    # 否则「改正片段后重跑 --merge」不会生效（旧版本这里对同名条目直接 continue）。
    # 同一名字出现在多个片段时按路径序「先到先得」，后来者不影响产物，文本不同则报冲突——
    # 顺序决定内容太隐晦，正是这一点让一个片段的错误分类盖掉正确分类。
    added, changed, rejected, conflicts = 0, [], [], []
    seen = {}
    paths = [p for d in FRAGMENT_DIRS for p in sorted(glob.glob(os.path.join(d, "*.json")))]
    for path in paths:
        with open(path, encoding="utf-8") as f:
            frag = json.load(f)
        for item in frag.get("functions", []):
            nm = item["name"]
            if nm not in live:
                rejected.append(f"{nm}（来自 {os.path.basename(path)}）")
                continue
            entry = {
                "category": norm_category(frag.get("category", "")),
                "upstream": item.get("upstream", ""),
                "desc": item.get("desc", ""),
                "empty_slot": item.get("empty_slot", ""),
                "notes": item.get("notes", ""),
                "uncertain": bool(item.get("uncertain", False)),
            }
            if nm in seen:
                if funcs.get(nm) != entry:
                    conflicts.append(f"{nm}（{seen[nm]} 定稿，{os.path.basename(path)} 文本不同）")
                continue
            seen[nm] = os.path.basename(path)
            if nm in funcs:
                if funcs[nm] != entry:
                    funcs[nm] = entry
                    changed.append(f"{nm}（{os.path.basename(path)}）")
                continue
            funcs[nm] = entry
            added += 1
    notes["functions"] = dict(sorted(funcs.items()))
    with open(NOTES_SRC, "w", encoding="utf-8", newline="\n") as f:
        json.dump(notes, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print(f"merged {added} new / {len(changed)} changed notes from {'+'.join(FRAGMENT_DIRS)} -> {NOTES_SRC}（合计 {len(funcs)}）")
    if changed:
        print(f"改写 {len(changed)} 条（片段覆盖旧文本）：{'、'.join(changed)}")
    if rejected:
        print(f"拒收 {len(rejected)} 条（不是 [BuiltinMethod] 工厂里的注册名）：{', '.join(rejected)}")
    if conflicts:
        print(f"!! 冲突 {len(conflicts)} 条（同一名字多份片段且文本不同，已取先到者）：{'、'.join(conflicts)}")


# ── 产物 ─────────────────────────────────────────────────────────────────────
LEGEND = """## 凡例

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
"""


def render(regs, flags, notes, dump=False):
    rows, missing, unresolved = [], [], []
    for reg in regs:
        ret, blocks = blocks_of_class(CODE, reg["cls"], reg["name"], reg["args"], flags)
        note = note_of(notes, reg["name"]) or {}
        entry = {"reg": reg, "ret": ret, "blocks": blocks, "note": note}
        rows.append(entry)
        if note.get("desc"):
            pass
        else:
            missing.append(reg["name"])
        if blocks is None and not reg["cls"]:
            unresolved.append(reg["name"])
    if dump:
        for e in rows:
            print(f"{e['reg']['name']:24s} {e['reg']['cls']:34s} {e['ret']} {e['blocks'] and len(e['blocks'])}")
        return
    return rows, missing


def dump_mode(rows):
    for e in rows:
        blocks = e["blocks"]
        print(f"{e['reg']['name']:26s} {e['reg']['cls']:32s} ret={e['ret'] or '?':4s} blocks={0 if blocks is None else len(blocks)}"
              f" note={'有' if e['note'].get('desc') else '—'}")
        if blocks:
            for b in blocks:
                print(f"      om={b['om']:<3} flags={b['flags']} reject={b['rejects_empty']} mvg={b['match_variadic_group']}")
    for p in problems[:40]:
        print(f"WARN {p}")
    print(f"WARN 合计 {len(problems)}")


def say(text):
    """控制台编码可能是 GBK（打不出「・」这类字符），降级替代而不是让诊断输出把脚本打崩。"""
    enc = sys.stdout.encoding or "utf-8"
    print(text.encode(enc, errors="replace").decode(enc, errors="replace"))


def main():
    global CODE
    raw = load_sources()
    func_src = open(FUNCTION_SRC, encoding="utf-8-sig").read()
    CODE = strip_keep_lines(raw)
    flags = parse_argtype(func_src)

    if "--merge" in sys.argv:
        merge_fragments()
        return

    regs = parse_registrations(raw)
    # 交叉校验：正则只认「attribute + 工厂签名」的相邻两行形态，漏掉的工厂必须在这里报出来，
    # 否则只剩 EXPECTED_NAMES 一个总数兜底（漏一个、多一个也可能凑巧相等）。
    attr_count = raw.count('[BuiltinMethod("')
    if len(regs) != attr_count:
        problems.append(f"扫到 {len(regs)} 个工厂，但文本里有 {attr_count} 处 [BuiltinMethod(（工厂形态变了？）")
    if len(regs) != EXPECTED_NAMES:
        problems.append(f"注册名 {len(regs)} != 期望 {EXPECTED_NAMES}（BuiltinMethod/ 目录改了要同步本脚本）")
    classes = sorted(set(r["cls"] for r in regs))
    if len(classes) != EXPECTED_CLASSES:
        problems.append(f"注册类 {len(classes)} != 期望 {EXPECTED_CLASSES}")
    total_blocks = sum(len(parse_blocks(CODE, p, e)) for _, p, e in class_spans(raw))
    if total_blocks != EXPECTED_BLOCKS:
        problems.append(f"ArgTypeList 段数 {total_blocks} != 期望 {EXPECTED_BLOCKS}")

    notes = load_notes()
    rows = []
    for reg in regs:
        ret, blocks = blocks_of_class(CODE, reg["cls"], reg["name"], reg["args"], flags)
        rows.append({"reg": reg, "ret": ret, "blocks": blocks, "note": note_of(notes, reg["name"]) or {}})

    if "--dump" in sys.argv:
        dump_mode(rows)
    else:
        write_doc(rows, flags, notes)

    # 与 arg-contract/issues/03 的裁定对齐：可省略 (类,槽) 全集、其中拒绝显式空槽的个数
    # （A 表 41 + 片 2-C 消费型变长尾 2 + 片 2-D 的 7 + issues/04 阶段 A 的 4）。改规则表就要同步这两个常量。
    omittable, reject = set(), set()
    for e in rows:
        for b in (e["blocks"] or []):
            for i, fv in enumerate(b["flags"]):
                if b["om"] >= 0 and i >= b["om"]:
                    omittable.add((e["reg"]["cls"], i))
                    if b["rejects_empty"][i]:
                        reject.add((e["reg"]["cls"], i))
    if len(omittable) != EXPECTED_OMITTABLE:
        problems.append(f"可省略 (类,槽) {len(omittable)} != EXPECTED_OMITTABLE {EXPECTED_OMITTABLE}")
    if len(reject) != EXPECTED_REJECT_EMPTY:
        problems.append(f"可省略且拒绝空槽的 (类,槽) {len(reject)} != EXPECTED_REJECT_EMPTY {EXPECTED_REJECT_EMPTY}")

    missing = [e["reg"]["name"] for e in rows if not e["note"].get("desc")]
    stale = [n for n in notes.get("functions", {}) if n not in {e["reg"]["name"] for e in rows}]
    uncertain = [e["reg"]["name"] for e in rows if e["note"].get("uncertain")]

    # 返回类型：构造器解释器要跟 `: this()` 链，跟丢了这里就是空——它不进文档的「返回」列。
    for e in rows:
        if not e["ret"]:
            problems.append(f"{e['reg']['name']}（类 {e['reg']['cls']}）的返回类型没解析出来")

    # 无规则表的名字只能人工裁定：代码里的实际集合必须与 NO_TABLE_RULED 完全一致（两个方向都报）
    no_table = {e["reg"]["name"] for e in rows if e["blocks"] is None}
    for nm in sorted(no_table - set(NO_TABLE_RULED)):
        problems.append(f"{nm} 没有规则表，但不在 NO_TABLE_RULED 里（补一条人工裁定）")
    for nm in sorted(set(NO_TABLE_RULED) - no_table):
        problems.append(f"NO_TABLE_RULED 里的 {nm} 现在有规则表了（删掉这条人工裁定）")

    # 分类：必须落在 CATEGORY_ORDER 里；空分类是上游有、本仓没有式中関数，属正常，打印出来免得被当异常
    cat_of = {e["reg"]["name"]: (e["note"].get("category") or "") for e in rows}
    for nm, cat in sorted(cat_of.items()):
        if cat not in CATEGORY_ORDER:
            problems.append(f"{nm} 的分类「{cat}」不在 CATEGORY_ORDER 里")
    empty_cats = [c for c in CATEGORY_ORDER if c not in set(cat_of.values())]

    # 说明与机器结论的一致性：有可留空槽的名字必须写清留空语义；
    # 无可留空槽的名字不得把「留空」当作可接受（说「拒绝/报错」可以，那正是机器结论）。
    deny = ("拒绝", "报", "CodeEE", "ArgCanNotBeNull", "个数错", "不可", "不能", "无槽", "无；", "都报")
    for e in rows:
        blocks = e["blocks"]
        if not blocks:
            continue
        mech = any(not r for b in blocks for r in b["rejects_empty"])
        txt = (e["note"].get("empty_slot") or "").strip()
        blank = txt in ("", "—", "-", "无")
        if mech and blank:
            problems.append(f"{e['reg']['name']} 有可显式留空的槽，但说明里的空槽语义为空")
        if not mech and "留空" in txt and not any(w in txt for w in deny):
            problems.append(f"{e['reg']['name']} 无可留空槽，说明却像在说留空可用（{txt[:24]}…）")
    # 反空跑：类体里明明有规则表，却没解析到该注册名的规则段 ⇒ 构造器解释器漏了分支
    spans = dict((n, (p, e)) for n, p, e in class_spans(raw))
    for e in rows:
        if e["blocks"] is not None:
            continue
        pos, end = spans.get(e["reg"]["cls"], (0, 0))
        body = CODE[pos:end]
        has_table = re.search(r"new ArgTypeList\s*\{", body) or re.search(
            r"argumentTypeArray\w*\s*=\s*\[", body)
        if has_table:
            problems.append(f"{e['reg']['name']}（类 {e['reg']['cls']}）没解析到规则段，但类体里有规则表")
    say(f"注册名 {len(regs)} / 类 {len(classes)} / 规则段 {total_blocks}")
    say(f"说明缺口 {len(missing)}（{', '.join(missing[:12])}{' …' if len(missing) > 12 else ''}）")
    # 这一行是「可省略/拒绝空槽」两个不变量的权威输出：票据与文档改为引用它，不再各自维护数字
    say(f"可省略 (类,槽) {len(omittable)}（其中拒绝显式空槽 {len(reject)}）")
    if empty_cats:
        say(f"上游有、本仓无式中関数的分类 {len(empty_cats)}：{', '.join(empty_cats)}")
    if stale:
        problems.append(f"notes 里有已不存在的注册名：{stale}")
    if uncertain:
        say(f"标记 uncertain 的名字 {len(uncertain)}：{', '.join(uncertain)}")
    for p in problems:
        say(f"WARN {p}")
    say("自校验" + ("通过" if not problems else f"有 {len(problems)} 项待处理"))


def write_doc(rows, flags, notes):
    by_cat = {}
    for e in rows:
        cat = e["note"].get("category") or ""
        by_cat.setdefault(cat, []).append(e)

    def cat_key(c):
        if c in CATEGORY_ORDER:
            return (0, CATEGORY_ORDER.index(c), "")
        if not c:
            return (2, 0, "")
        return (1, 0, c)

    lines = []
    lines.append("# 内建函数（式中関数）契约表")
    lines.append("")
    lines.append("本表覆盖 `Emuera/Runtime/Script/Statements/Function/BuiltinMethod/` 目录下 `[BuiltinMethod]` 工厂注册的全部 "
                 "**%d 个式中関数注册名**（%d 个 `FunctionMethod` 子类）的实参契约。"
                 % (len(rows), len(set(e["reg"]["cls"] for e in rows))))
    lines.append("形状、合法实参数与返回类型由代码机械推出（生成器 [`tracker/arg-contract/builtin-contract.py`]"
                 "(../tracker/arg-contract/builtin-contract.py)，可复跑）；"
                 "说明列参考 [upstream 在线文档](https://evilmask.gitlab.io/emuera.em.doc/Reference/index.html) "
                 "并逐条按本仓代码核对，**不一致时以代码为准**。")
    lines.append("")
    lines.append(LEGEND)
    lines.append("")
    lines.append("## 索引（按注册名）")
    lines.append("")
    lines.append("| 注册名 | 返回 | 类别 | 首个候选形状 |")
    lines.append("| --- | --- | --- | --- |")
    for e in sorted(rows, key=lambda x: x["reg"]["name"]):
        blocks = e["blocks"] or []
        ruled = NO_TABLE_RULED.get(e["reg"]["name"])
        first = shape_of(blocks[0], flags) if blocks else (ruled["shape"] if ruled else "—")
        lines.append(f"| `{e['reg']['name']}` | {e['ret'] or '—'} | {e['note'].get('category') or '—'} | {first} |")
    lines.append("")
    lines.append("## 分族契约")
    lines.append("")
    for cat in sorted(by_cat, key=cat_key):
        title = cat or "未分类（说明待补）"
        lines.append(f"### {title}")
        lines.append("")
        lines.append("| 注册名 | 返回 | 实参形状 | 合法实参数 | 空槽语义 | 说明 |")
        lines.append("| --- | --- | --- | --- | --- | --- |")
        for e in sorted(by_cat[cat], key=lambda x: x["reg"]["name"]):
            blocks = e["blocks"]
            note = e["note"]
            if not blocks:
                # 无规则表 = 类自己 override 了 CheckArgumentType，形状只能取人工裁定（NO_TABLE_RULED）
                ruled = NO_TABLE_RULED.get(e["reg"]["name"])
                if ruled is None:
                    lines.append(f"| `{e['reg']['name']}` | {e['ret'] or '—'} | —（无规则表，自检） | — | — "
                                 f"| {cell(note.get('desc'))} |")
                else:
                    lines.append(f"| `{e['reg']['name']}` | {e['ret'] or '—'} | {ruled['shape']} | {ruled['count']} "
                                 f"| {cell(note.get('empty_slot'), '—')} | 手工裁定（无规则表，类自行检查）："
                                 f"{cell(note.get('desc'))} |")
                continue
            for idx, b in enumerate(blocks):
                shape = shape_of(b, flags)
                cnt = count_text(b, flags)
                ep = cell(note.get("empty_slot") or empty_of(blocks), "（待补）")
                desc = cell(note.get("desc")) if idx == 0 else "（同上）"
                if idx == 0 and note.get("notes"):
                    # 片段里 desc 与 notes 各自带句末标点，直接拼会出现「。；」这种双标点
                    desc = desc.rstrip("。；") + "；" + cell(note["notes"])
                desc = desc.rstrip("。")
                lines.append(f"| `{e['reg']['name']}`{'（候选 %d）' % (idx + 1) if len(blocks) > 1 else ''} "
                             f"| {e['ret'] or '—'} | {shape} | {cnt} | {ep} | {desc} |")
        lines.append("")

    # 显式留空（槽位记号的 `-`）集中一览：这一维与「数量省略」正交，写脚本时最常踩
    lines.append("## 显式留空（记号 `-`）语义一览")
    lines.append("")
    lines.append("只列**允许显式留空**的注册名（`f(a,,c)` 这种写法能过检查器的那些槽）——这一维与「数量省略」正交，"
                 "写脚本时最容易踩。空槽语义取自本表「空槽语义」列；有可留空槽却没写清语义的会在生成时报警。")
    lines.append("")
    lines.append("| 注册名 | 可留空的槽 | 空槽语义 |")
    lines.append("| --- | --- | --- |")
    for e in sorted(rows, key=lambda x: x["reg"]["name"]):
        blocks = e["blocks"]
        if not blocks:
            continue
        nullable = sorted({i + 1 for b in blocks for i, r in enumerate(b["rejects_empty"]) if not r})
        if not nullable:
            continue
        lines.append(f"| `{e['reg']['name']}` | {'、'.join(str(x) for x in nullable)} | "
                     f"{cell(e['note'].get('empty_slot'), '（待补）')} |")
    lines.append("")
    text = "\n".join(lines).rstrip() + "\n"
    os.makedirs(os.path.dirname(DST), exist_ok=True)
    with open(DST, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(text)
    print(f"wrote {DST}（{text.count(chr(10))} 行）")


def cell(s, dash="（待补）"):
    return (s or dash).replace("|", "\\|").replace("\n", " ")


def shape_of(block, flags):
    entries = block["flags"]
    argc = len(entries)
    if argc == 0:
        return "（无实参）"
    last_variadics = 0
    for e in reversed(entries):
        if e & flags["Variadic"]:
            last_variadics += 1
        else:
            break
    start = argc - last_variadics
    out = []
    for i in range(argc):
        omittable = block["om"] >= 0 and i >= block["om"]
        out.append(slot_text(entries[i], flags, omittable, block["rejects_empty"][i],
                             bool(entries[i] & flags["Variadic"])))
    if last_variadics:
        out.append("（尾部：自第 %d 槽起按末位规则重复）" % (start + 1))
    return ", ".join(out)


def empty_of(blocks):
    nullable = []
    slots = 0
    for b in blocks:
        slots += len(b["rejects_empty"])
        for i, r in enumerate(b["rejects_empty"]):
            if not r:
                nullable.append(i + 1)
    if slots == 0:
        return "—"
    if not nullable:
        return "拒绝（显式空槽 = `CodeEE(ArgCanNotBeNull)`）"
    return "可留空的槽：" + "、".join(str(x) for x in sorted(set(nullable)))


if __name__ == "__main__":
    main()
