"""arg-contract 裁定表的**跨脚本**单一来源：4 个硬转型槽（`arg-contract/issues/04` 阶段 A 的收口）。

用途：这 4 条裁定 `arg-slots.py` 的 `RULINGS` 与 `slots-worklist.py` 的 `SUPPLEMENT_RULINGS` 都要写，
此前各写一份，文案已经漂移——处置句一边写「本片标 `ArgType.NotNull`（显式空槽应拒绝；输出数组）」，
另一边写「已由 arg-contract/issues/04 阶段 A 标 `NotNull`（输出数组；cast 的类型化移除归 05）」。
现在只有这一份源数据，两侧各自适配成自己的元组形状：
  · `consumption_rulings()` → `arg-slots.py` 用的 3 元组 (类别, 处置, 数量省略尺子)；
  · `worklist_rulings()` → `slots-worklist.py` 用的 4 元组 (类别, 处置, 数量省略尺子, 是否已带标志)。
两个生成器的**其余**裁定（`arg-slots.py` 的 2 条假阳性、`slots-worklist.py` 的另外 16 条补车道裁定）
与本站点无关，留在原处。

字段：
  · 键 = (类名, 槽号)，与两个生成器一致；
  · `category` = 类别（两侧的分类表都有「硬转型解引用」）；
  · `disposition` = 处置文案，**唯一一句**——两侧产物里必须逐字相同；
  · `ruler` = (结论, 守卫式)：数量省略尺子的结论。两侧成文格式历来不同（`arg-slots` 写
    ``过（`Count == 2` 才读）``，`slots-worklist` 写 `过（Count == 2）`），故这里只存结构，
    由下面的 `_ruler_*` 成文；`DataTableSelectMethod[3]` 的原 `slots-worklist` 文案
    「Count != 4 的否支」与 `arg-slots` 文案「`Count == 4` 才读」说的是同一守卫
    （`bool toResult = arguments.Count != 4;` 的三元真支才读 `arguments[3]`），统一后取 `Count == 4`；
  · `flagged` = 该槽的**全部**可省略段是否都已在**该槽位**带 `ArgType.NotNull`（只有 `worklist_rulings()`
    需要这一列；`arg-slots.py` 的标志由脚本机械判定，不进裁定表）。

代码侧取证：`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs` 的 `EnumNameMethod.GetIntValue`、
`EnumFilesMethod.GetIntValue`、`DataTableSelectMethod.GetIntValue`、`DataTableToXmlMethod.GetStrValue`。
"""

# 4 条共用的处置文案（唯一一句；改这里即同时改两个产物）
DISPOSITION = ("已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`"
               "（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05）")

HARDCAST_RULINGS = {
    ("EnumNameMethod", 1): {
        "category": "硬转型解引用",
        "disposition": DISPOSITION,
        "ruler": ("过", "Count == 2"),
        "flagged": True,
    },
    ("EnumFilesMethod", 3): {
        "category": "硬转型解引用",
        "disposition": DISPOSITION,
        "ruler": ("过", "Count == 4"),
        "flagged": True,
    },
    ("DataTableSelectMethod", 3): {
        "category": "硬转型解引用",
        "disposition": DISPOSITION,
        "ruler": ("过", "Count == 4"),
        "flagged": True,
    },
    ("DataTableToXmlMethod", 1): {
        "category": "硬转型解引用",
        "disposition": DISPOSITION,
        "ruler": ("过", "Count > 1"),
        "flagged": True,
    },
}


def _ruler_consumption(ruler):
    """`arg-slots.py` 的尺子成文格式。"""
    verdict, guard = ruler
    return f"{verdict}（`{guard}` 才读）"


def _ruler_worklist(ruler):
    """`slots-worklist.py` 的尺子成文格式。"""
    verdict, guard = ruler
    return f"{verdict}（{guard}）"


def consumption_rulings():
    """`arg-slots.py` 的 `RULINGS` 片段：{键: (类别, 处置, 数量省略尺子)}。"""
    return {k: (v["category"], v["disposition"], _ruler_consumption(v["ruler"]))
            for k, v in HARDCAST_RULINGS.items()}


def worklist_rulings():
    """`slots-worklist.py` 的 `SUPPLEMENT_RULINGS` 片段：{键: (类别, 处置, 数量省略尺子, 已带标志)}。"""
    return {k: (v["category"], v["disposition"], _ruler_worklist(v["ruler"]), v["flagged"])
            for k, v in HARDCAST_RULINGS.items()}
