# Nullable:残留断言站点清单

本文件是 issues/21 的机械产物(2026-09-25):issues/13/14 的减法残差类「其他成员/局部 `!`」在 `Emuera/` 内的全部站点,作为逐点判定与验收的锚点。

## 口径

- 范围:`Emuera/`(不含 `Emuera.Tests`,该工程按 issues/14 决策豁免;不含生成文件)
- 预处理:剔除注释与字符串/字符字面量,匹配后缀 `!\s*(?!=)` 形态
- 收紧:匹配式取 `[\w\)\]]!(?!=)`(无空白),排除 **12 处**前缀逻辑非误计(如 `case true when !v.IsString`、`return !(...)`、`operator !=` 实现体)。票面此前的 1088 处(全库)含这 12 处误计,全库真实后缀断言为 **1076 处**(Emuera 969 / Emuera.Tests 107)
- 分类与 issues/13 一致:全库后缀断言扣除 `null!`、`Argument` 可选字段、其他 `list[i]!`、`(x as T)!`、`JumpTo` 族五类,再扣除 `ArgumentListExtensions` 内承担全部参数槽位断言的 2 处,得残差类;本文件即该残差类在 `Emuera/` 内的部分
- 站点 ID 形如 `<文件>#<类型>.<成员>#<序号>`,序号在同一 `文件#成员` 内按源码顺序从 1 计,不含行号
- 交叉验证(2026-09-25):同一收紧式在 `Emuera/` 上用三种实现独立计数一致——IDE(Rider)文本搜索 979、PowerShell/.NET 正则 979、本脚本剥注释与字符串后 969(差的 10 处位于注释或字符串字面量内)。全库剥后为 **1076** = 969 + `Emuera.Tests` 107
- 锚点:清单对应 `1ed33b5c`(2026-09-21)到落地提交 `03292664`(2026-09-25)之间的代码,该区间内 344 条源码片段逐一命中;首个删除发生在 `e9eaaf32`(2026-09-25),此后共删 12 条站点,清单未逐条回填,见文末「已失效站点」;验收一律以该区间为基准

## 构成

| 子桶 | 处数 |
| --- | --- |
| 裸标识符/局部断言(`bare/local`) | 167 |
| `X.Member!` 成员访问 | 80 |
| `F(...)!` 方法调用结果 | 64 |
| `X![i]` 断言后索引(`dict![key]`) | 30 |
| 其他表达式(`other-expr`,括号表达式后断言) | 2 |
| `default!` | 1 |
| **合计** | **344** |

标签与 [issues/21](issues/21-remaining-member-assertions.md) 的构成表一致;下方「判定分组索引」的「子桶」列是粗分类,把上表前三行的三种形态统称 `bare/member`。

## 判定进度

| 结论 | 处数 |
| --- | --- |
| A | 282 |
| B | 9 |
| C | 16 |
| D | 37 |
| 待确认 | 0 |
| 未判定 | 0 |
| **合计** | **344** |

逐站点的结论见各表「结论」列;分组依据与去向见 [issues/21](issues/21-remaining-member-assertions.md) 的判定表。
`A` 成立(保留 + 理由注释) / `B` 不成立但当前不可达(潜在缺陷) / `C` 可达缺陷(另立票) / `D` 断言为假但被调方容忍(契约诚实化)。

### 裸标识符/局部断言(`bare/local`,167 处)

| 站点 ID | 接收者 | 根声明类型 | 结论 | 源码 |
| --- | --- | --- | --- | --- |
| `Emuera/Runtime/Config/ConfigItem.cs#ConfigItem<T>.ValueToString#1` | `val` | T (成员) | A | `return val!.ToString()!;` |
| `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.KeywordToInteger#1` | `dic` | Dictionary<string, int>? (局部) | A | `if (dic!.TryGetValue(key, out int ret))` |
| `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.toCharacterTemplate#1` | `intArray` | Dictionary<int, long>? (局部) | A | `if (intArray!.ContainsKey(index))` |
| `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.loadDataTo#1` | `output` | EmueraConsole (成员) | B | `output!.PrintError(trerror.UnexpectedError.Text);` |
| `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.loadAliases#1` | `output` | EmueraConsole (成员) | B | `output!.PrintError(trerror.UnexpectedError.Text);` |
| `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetVariableToken#1` | `line` | LogicalLine? (局部) | A | `ParserMediator.Warn(treer.CannotRecommendCallLocalVar.Text, line!, 1, false, false);` |
| `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetVariableToken#2` | `line` | LogicalLine? (局部) | C | `LocalVariableToken retLocal = value.GetNewVariableTokenIfAbsent(subKey, line!.ParentLabelLine!);` |
| `Emuera/Runtime/Script/Data/ParserMediator.cs#ParserMediator.FlushWarningList#1` | `console` | EmueraConsole? (成员) | A | `console!.PrintWarning(warning.WarningMes, warning.WarningPos, warning.WarningLevel);` |
| `Emuera/Runtime/Script/Data/ParserMediator.cs#ParserMediator.SingleLine#1` | `console` | EmueraConsole? (成员) | A | `console!.PrintSingleLine(str);` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#4` | `pos` | ScriptPosition? (局部) | A | `string.Format(trerror.LabelIsAlreadyDefined.Text, gotoLabel.LabelName, pos!.Value.Filename,` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.addLine#1` | `lastLine` | LogicalLine? (成员) | A | `lastLine!.NextLine = nextLine;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#2` | `symbol` | SymbolWord? (局部) | A | `if (symbol!.Type == ',')` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#3` | `term` | AExpression? (局部) | A | `if (!(term!.Restructure(exm) is VariableTerm vTerm) \|\| vTerm.Identifier.IsConst)` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#4` | `term` | AExpression? (局部) | A | `def = term!.Restructure(exm) as SingleTerm;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#5` | `def` | SingleTerm? (局部) | D | `defs[i] = def!;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.nestCheck#1` | `func` | InstructionLine? (局部) | A | `ParserMediator.Warn(trerror.DefaultError.Text, func!, 2, true, false);` |
| `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#2` | `parentLine` | FunctionLabelLine? (参数) | C | `parentLine!.AddPrivateVariable(varData);` |
| `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#3` | `parentLine` | FunctionLabelLine? (参数) | C | `parentLine!.AddPrivateVariable(varData);` |
| `Emuera/Runtime/Script/Parser/SubWord.cs#YenAtSubWord.SetIsMacro#1` | `Words` | — | A | `Words!.SetIsMacro();` |
| `Emuera/Runtime/Script/Parser/SubWord.cs#YenAtSubWord.SetIsMacro#2` | `Right` | StrFormWord? (成员) | B | `Right!.SetIsMacro(); //Right is null only on the missing-# warning path, which never reaches macro expansion` |
| `Emuera/Runtime/Script/Parser/WordCollection.cs#WordCollection.Next#1` | `Pointer` | LinkedListNode<Word>? (成员) | A | `return Pointer!.Next!.Value;` |
| `Emuera/Runtime/Script/Parser/WordCollection.cs#WordCollection.Remove#1` | `Pointer` | LinkedListNode<Word>? (成员) | A | `LinkedListNode<Word>? next = Pointer!.Next;` |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallEventFunction#1` | `line` | FunctionLabelLine? (局部) | A | `throw new CodeEE(string.Format(trerror.CalleventToNonEventFunc.Text, label, line!.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallLabel#1` | `CurrentLabel` | FunctionLabelLine? (成员) | A | `return parent.LabelDictionary.GetLabelDollar(label, CurrentLabel!);` |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.IsOnly#1` | `CurrentLabel` | FunctionLabelLine? (成员) | A | `public bool IsOnly => CurrentLabel!.IsOnly;` |
| `Emuera/Runtime/Script/Process.ScriptProc.cs#Process.runScriptProc#1` | `line` | LogicalLine? (局部) | A | `if (line!.IsError)` |
| `Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#1` | `term` | AExpression? (局部) | A | `exm.Console.PrintPlain(term!.GetStrValue(exm));` |
| `Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#2` | `term` | AExpression? (局部) | A | `str = term!.GetStrValue(exm);` |
| `Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#3` | `term` | AExpression? (局部) | A | `str = term!.GetStrValue(exm);` |
| `Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#4` | `term` | AExpression? (局部) | A | `string target = term!.GetStrValue(exm);` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.ShiftNextLine#1` | `currentLine` | LogicalLine? (成员) | A | `currentLine = currentLine!.NextLine;` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#7` | `exm` | ExpressionMediator (成员) | D | `srcArgs.SetTransporter(exm!);` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#8` | `exm` | ExpressionMediator (成员) | D | `call.TopLabel.Arg[i].SetValue(srcArgs.TransporterInt[i], exm!);` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#9` | `exm` | ExpressionMediator (成员) | D | `call.TopLabel.Arg[i].SetValue(srcArgs.TransporterStr[i], exm!);` |
| `Emuera/Runtime/Script/Process.cs#Process.handleException#1` | `current` | LogicalLine? (参数) | C | `console.PrintError(string.Format(trerror.ErrorInFunc.Text, current!.ParentLabelLine!.LabelName,` |
| `Emuera/Runtime/Script/Statements/Argument.cs#CallformCache.Store#1` | `strKey` | string? (参数) | A | `strDic[strKey!] = entry;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_IMG_ArgumentBuilder.CreateArgument#1` | `arg` | AExpression? (局部) | C | `if (arg!.IsString)` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#1` | `arg` | AExpression? (局部) | C | `arg = arg!.Restructure(exm);` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#2` | `arg` | AExpression? (局部) | C | `param.Add(new MixedIntegerExprTerm { num = arg!, isPx = wc.Current.Type != '\0' && wc.Current.Type != ',' });` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALL_ArgumentBuilder.CreateArgument#2` | `subNames` | List<AExpression?>? (局部) | A | `for (int i = 0; i < subNames!.Count; i++)` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALLSHARP_ArgumentBuilder.CreateArgument#2` | `subNames` | List<AExpression?>? (局部) | A | `for (int i = 0; i < subNames!.Count; i++)` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_SPLIT_ArgumentBuilder.CreateArgument#1` | `term` | VariableTerm? (局部) | A | `return new SpSplitArgument(terms.Required(0), terms.Required(1), x.Identifier, term!);` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#1` | `name` | AExpression? (局部) | A | `return new RefArgument(refm, name!);` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#2` | `refVar` | ReferenceToken? (局部) | D | `return new RefArgument(refVar!, name!);` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#3` | `name` | AExpression? (局部) | D | `return new RefArgument(refVar!, name!);` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#4` | `refVar` | ReferenceToken? (局部) | A | `return new RefArgument(refVar!, srcVar);` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.Reduce#1` | `LeftTerm` | AExpression? (成员) | A | `LeftTerm = LeftTerm!.Restructure(exm);` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.Reduce#2` | `RightTerm` | AExpression? (成员) | A | `RightTerm = RightTerm!.Restructure(exm);` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#1` | `LeftTerm` | AExpression? (成员) | A | `return LeftTerm!.ToString();` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#2` | `LeftTerm` | AExpression? (成员) | A | `return "Is " + Operator + " " + LeftTerm!;` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#3` | `LeftTerm` | AExpression? (成员) | A | `return LeftTerm! + " To " + RightTerm!;` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#4` | `RightTerm` | AExpression? (成员) | A | `return LeftTerm! + " To " + RightTerm!;` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#1` | `LeftTerm` | AExpression? (成员) | A | `return LeftTerm!.GetIntValue(exm) <= Is && Is <= RightTerm!.GetIntValue(exm);` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#2` | `RightTerm` | AExpression? (成员) | A | `return LeftTerm!.GetIntValue(exm) <= Is && Is <= RightTerm!.GetIntValue(exm);` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#3` | `LeftTerm` | AExpression? (成员) | A | `if (LeftTerm!.IsInteger)` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#4` | `LeftTerm` | AExpression? (成员) | A | `return LeftTerm!.GetIntValue(exm) == Is;` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#5` | `LeftTerm` | AExpression? (成员) | A | `return string.Compare(LeftTerm!.GetStrValue(exm), Is, Config.Config.SCExpression) <= 0` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#6` | `RightTerm` | AExpression? (成员) | A | `&& string.Compare(Is, RightTerm!.GetStrValue(exm), Config.Config.SCExpression) <= 0;` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#7` | `LeftTerm` | AExpression? (成员) | A | `if (LeftTerm!.IsString)` |
| `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#8` | `LeftTerm` | AExpression? (成员) | A | `return LeftTerm!.GetStrValue(exm) == Is;` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.Reset#1` | `minTimer` | System.Timers.Timer? (成员) | A | `minTimer!.Dispose();` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.SetTimerInterval#1` | `minTimer` | System.Timers.Timer? (成员) | A | `minTimer!.Interval = interval;` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.ScrollUp#1` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `if (lineBuffer!.Count < ScrollPos)` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.MinTimeCheck#1` | `minTimer` | System.Timers.Timer? (成员) | A | `minTimer!.Start();` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.AddLine#1` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `lineBuffer!.Enqueue(processed);` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.DelLine#1` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `if (count >= lineBuffer!.Count)` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.ClearScreen#1` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `lineBuffer!.Clear();` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.ClearScreen#2` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `lineBuffer!.Enqueue("");` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.SendToCB#1` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `length = Math.Min(NewLineCount, lineBuffer!.Count);` |
| `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.SendToCB#2` | `lineBuffer` | CircularBuffer<string>? (成员) | A | `length = Math.Min(MaxCB, lineBuffer!.Count - ScrollPos);` |
| `Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.reduceIdentifier#1` | `mToken` | AExpression? (局部) | A | `return mToken!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#EnumNameMethod.GetIntValue#1` | `array` | string[]? (局部) | B | `foreach (string item in array!)` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ArrayMultiSortExMethod.GetConvertedTerm#1` | `term` | AExpression? (局部) | A | `string? err = CheckVariableTerm(term!, name);` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#2` | `attr` | XmlAttribute? (局部) | A | `case 1: attr!.OwnerElement!.Attributes.InsertBefore(newAttr, attr); break;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#4` | `attr` | XmlAttribute? (局部) | A | `case 2: attr!.OwnerElement!.Attributes.InsertAfter(newAttr, attr); break;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ReplaceMethod.GetStrValue#1` | `reg` | Regex? (局部) | A | `return reg!.Replace(baseString, (Match match) =>` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ReplaceMethod.GetStrValue#2` | `reg` | Regex? (局部) | A | `return reg!.Replace(baseString, arguments.Required(2).GetStrValue(exm));` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#GetConfigMethod.GetSingleTerm#1` | `term` | SingleTerm? (局部) | A | `return term!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#CBGSETButtonSpriteMethod.GetIntValue#1` | `imgN` | ASprite? (局部) | D | `if (!exm.Console.CBG_SetButtonImage((int)b64, imgN!, imgB!, p.X, p.Y, (int)z64, tooltip))` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#CBGSETButtonSpriteMethod.GetIntValue#2` | `imgB` | ASprite? (局部) | D | `if (!exm.Console.CBG_SetButtonImage((int)b64, imgN!, imgB!, p.X, p.Y, (int)z64, tooltip))` |
| `Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs#FunctionMethod.CheckArgumentTypeEx#1` | `argumentTypeArrayEx` | ArgTypeList[]? (成员) | A | `ArgTypeList[] rules = argumentTypeArrayEx!;` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#PRINT_Instruction.DoInstruction#1` | `termV` | AExpression? (局部) | A | `if (termV!.IsInteger)` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#CALLSHARP_Instruction.DoInstruction#1` | `term` | — | C | `PluginMethodParameter[] pluginArgs = [.. arg.RowArgs.Select((term) => PluginMethodParameterBuilder.ConvertTerm(term!, exm))];` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#SELECTCASE_Instruction.DoInstruction#1` | `sValue` | string? (局部) | A | `string Is = sValue!;` |
| `Emuera/Runtime/Script/Statements/LogicalLine.cs#InstructionLine.BuiltArgument#1` | `Argument` | Argument? (成员) | A | `public Argument BuiltArgument => Argument!;` |
| `Emuera/Runtime/Script/Statements/LogicalLine.cs#FunctionLabelLine.CompareTo#1` | `other` | FunctionLabelLine? (成员) | B | `if (FileIndex != other!.FileIndex)` |
| `Emuera/Runtime/Script/Statements/LogicalLine.cs#FunctionLabelLine.CompareTo#2` | `Position` | — | A | `if (Position!.Value.LineNo != other.Position!.Value.LineNo)` |
| `Emuera/Runtime/Script/Statements/LogicalLine.cs#GotoLabelLine.GetHashCode#1` | `ParentLabelLine` | — | A | `return labelname.GetHashCode() ^ ParentLabelLine!.GetHashCode();` |
| `Emuera/Runtime/Script/Statements/Variable/VariableData.cs#VariableData.CreateUserDefCharaVariable#1` | `ret` | UserDefinedCharaVariableToken? (局部) | A | `UserDefinedCharaVarList.Add(ret!);` |
| `Emuera/Runtime/Script/Statements/Variable/VariableData.cs#VariableData.CreateUserDefCharaVariable#2` | `ret` | UserDefinedCharaVariableToken? (局部) | A | `return ret!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableParser.cs#VariableParser.ReduceVariable#1` | `op1` | AExpression? (局部) | A | `terms = [op1!, op2];` |
| `Emuera/Runtime/Script/Statements/Variable/VariableStrArgTerm.cs#VariableStrArgTerm.GetIntValue#1` | `!dic` | Dictionary<string, int>? (成员) | A | `if (!dic!.TryGetValue(key, out int i))` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#ReferenceToken.IsArrayRangeValid#1` | `array` | Array? (成员) | A | `if (index1 < 0 \|\| index1 > array!.GetLength(Dimension - 1))` |
| `Emuera/Runtime/Utils/EraDataStream.cs#EraDataReader.ReadInt64Array3DExtended#1` | `str` | string? (局部) | C | `if (str!.Length == 0)` |
| `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#1` | `innerEllipse` | GraphicsPath? (局部) | A | `graph.FillPath(brush, innerEllipse!);` |
| `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#2` | `innerEllipse` | GraphicsPath? (局部) | A | `cornerEllipse.AddPath(innerEllipse!, false);` |
| `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.ExecuteLine#1` | `(InstructionLine)logicalLine` | — | C | `InstructionLine func = (InstructionLine)logicalLine!;` |
| `Emuera/Runtime/Utils/WebPWrapper.cs#WebP.Decode#1` | `bmp` | Bitmap? (局部) | A | `bmp!.UnlockBits(bmpData);` |
| `Emuera/Runtime/Utils/WebPWrapper.cs#WebP.Decode#2` | `bmp` | Bitmap? (局部) | A | `bmp!.UnlockBits(bmpData);` |
| `Emuera/Runtime/Utils/WebPWrapper.cs#WebP.GetThumbnailFast#1` | `bmp` | Bitmap? (局部) | A | `bmp!.UnlockBits(bmpData);` |
| `Emuera/Runtime/Utils/WebPWrapper.cs#WebP.GetThumbnailQuality#1` | `bmp` | Bitmap? (局部) | A | `bmp!.UnlockBits(bmpData);` |
| `Emuera/UI/FontFactory.cs#FontFactory.CreateFont#1` | `ff` | FontFamily? (局部) | A | `FontFamily? ff = GlobalStatic.Pfc.Families.FirstOrDefault(ff => ff!.Name == requestFontName, null);` |
| `Emuera/UI/Framework/Forms/ConfigDialog.cs#ConfigDialog.SetupLang#1` | `fisrt` | object? (局部) | A | `comboBox7.Items.Add(fisrt!);` |
| `Emuera/UI/Framework/Forms/DebugDialog.cs#DebugDialog.getValueString#1` | `term` | AExpression? (局部) | C | `SingleTerm value = term!.GetValue(GlobalStatic.EMediator);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#1` | `console` | EmueraConsole? (成员) | A | `dialog.Setup(console!);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#2` | `console` | EmueraConsole? (成员) | A | `console!.ReloadPartialErb(filepath);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#3` | `console` | EmueraConsole? (成员) | A | `if (console!.notToTitle)` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#5` | `console` | EmueraConsole? (成员) | A | `if (Config.CBUseClipboard && console!.CBProc.ScrollUp(1))` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#6` | `console` | EmueraConsole? (成员) | A | `if (Config.CBUseClipboard && console!.CBProc.ScrollDown(1))` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.Init#1` | `console` | EmueraConsole? (成员) | A | `await console!.Initialize();` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseClickCBCheck#1` | `console` | EmueraConsole? (成员) | A | `console!.CBProc.Check(ClipboardProcessor.CBTriggers.LeftClick);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseClickCBCheck#2` | `console` | EmueraConsole? (成员) | A | `console!.CBProc.Check(ClipboardProcessor.CBTriggers.MiddleClick);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseDoubleClickCBCheck#1` | `console` | EmueraConsole? (成员) | A | `console!.CBProc.Check(ClipboardProcessor.CBTriggers.DoubleLeftClick);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseLeave#1` | `console` | EmueraConsole? (成员) | A | `console!.LeaveMouse();` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ToolStripMenuItem_Click#1` | `console` | EmueraConsole? (成员) | A | `dialog.Setup(console!);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ToolStripMenuItem_Click#2` | `console` | EmueraConsole? (成员) | A | `console!.OpenDebugDialog();` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ToolStripMenuItem_Click#3` | `console` | EmueraConsole? (成员) | A | `if (console!.DebugDialog != null && console.DebugDialog.Created)` |
| `Emuera/UI/Framework/Forms/RikaiDialog.cs#IndexEntry.CompareTo#1` | `ie` | IndexEntry? (成员) | A | `return Compare(mWord, ie!.mWord);` |
| `Emuera/UI/Framework/Forms/RikaiDialog.cs#RikaiDialog.bwDoWork#1` | `worker` | BackgroundWorker? (局部) | A | `worker!.ReportProgress(100);` |
| `Emuera/UI/Framework/Forms/RikaiDialog.cs#RikaiDialog.bwDoWork#2` | `worker` | BackgroundWorker? (局部) | A | `worker!.ReportProgress(percentage);` |
| `Emuera/UI/Game/ConsoleButtonString.cs#ConsoleButtonString.ConsoleButtonString#1` | `console` | EmueraConsole? (参数) | D | `parent = console!;` |
| `Emuera/UI/Game/ConsoleImagePart.cs#ConsoleImagePart.computeGeometry#1` | `cImage` | ASprite? (成员) | A | `Width = cImage!.DestBaseSize.Width * height / cImage.DestBaseSize.Height;` |
| `Emuera/UI/Game/ConsoleImagePart.cs#ConsoleImagePart.SetWidth#1` | `Text` | — | A | `Width = sm.GetDisplayLength(Text!, Config.DefaultFont);` |
| `Emuera/UI/Game/ConsoleImagePart.cs#ConsoleImagePart.DrawTo#1` | `AltText` | — | A | `GraphicsTextRenderer.Draw(graph, AltText!, Config.DefaultFont, Config.ForeColor, PointX, XsubPixel, pointY);` |
| `Emuera/UI/Game/ConsoleShapePart.cs#ConsoleErrorShapePart.DrawTo#1` | `Text` | — | A | `GraphicsTextRenderer.Draw(graph, Text!, Config.DefaultFont, Config.ForeColor, PointX, XsubPixel, pointY);` |
| `Emuera/UI/Game/ConsoleShapePart.cs#ConsoleErrorShapePart.SetWidth#1` | `Text` | — | A | `Width = sm.GetDisplayLength(Text!, Config.DefaultFont);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DivideAt#1` | `Text` | — | A | `if (index <= 0 \|\| index > Text!.Length \|\| Error)` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#1` | `Text` | — | A | `Width = sm.GetDisplayLength(Text!, Font!);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#2` | `Font` | Font? (成员) | A | `Width = sm.GetDisplayLength(Text!, Font!);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#3` | `Text` | — | A | `int len = Text!.Length;` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#4` | `Font` | Font? (成员) | A | `Ends[i] = sm.GetDisplayLength(temp, Font!);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawTo#1` | `Text` | — | A | `GraphicsTextRenderer.Draw(graph, Text!, Font!, color, PointX, XsubPixel, pointY);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawTo#2` | `Font` | Font? (成员) | A | `GraphicsTextRenderer.Draw(graph, Text!, Font!, color, PointX, XsubPixel, pointY);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawToBitmap#1` | `Text` | — | A | `GraphicsTextRenderer.Draw(graph, Text!, Font!, color, xOffset, subPixel, 0);` |
| `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawToBitmap#2` | `Font` | Font? (成员) | A | `GraphicsTextRenderer.Draw(graph, Text!, Font!, color, xOffset, subPixel, 0);` |
| `Emuera/UI/Game/EmueraConsole.Print.cs#EmueraConsole.CreateTypeCString#1` | `str` | string (参数) | A | `return str!;` |
| `Emuera/UI/Game/EmueraConsole.Print.cs#EmueraConsole.CreateTypeCString#2` | `str` | string (参数) | A | `return str!;` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OpenErrorFile#1` | `pos` | ScriptPosition? (成员) | A | `ScriptPosition position = pos!.Value;` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#1` | `escapedParts` | Dictionary<int, List<AConsoleDisplayNode>> (成员) | A | `if (depth != 0 \|\| escapedParts!.ContainsKey(depth))` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#3` | `pointing` | ConsoleButtonString? (局部) | A | `if (pointing!.IsButton)` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlAnalzeState.GetSS#1` | `fontname` | string? (局部) | D | `return new StringStyle(c, colorChanged, b, FontStyle, fontname!);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.DisplayLine2Html#2` | `css` | ConsoleStyledString? (局部) | A | `b.Append(getStringStyleStartingTag(css!.StringStyle));` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#1` | `str` | string? (参数) | A | `CharStream st = parent != null ? parent.Stream : new CharStream(str!);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#2` | `str` | string? (参数) | A | `bool hasComment = parent?.HasComment ?? str!.Contains("<!--", StringComparison.Ordinal);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#3` | `str` | string? (参数) | A | `bool hasReturn = parent?.HasReturn ?? str!.Contains('\n', StringComparison.Ordinal);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#1` | `height` | MixedNum? (局部) | D | `ParseMixedNum(ref height!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#2` | `width` | MixedNum? (局部) | D | `ParseMixedNum(ref width!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#3` | `ypos` | MixedNum? (局部) | D | `ParseMixedNum(ref ypos!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#4` | `height` | MixedNum? (局部) | D | `ParseMixedNum(ref height!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#5` | `width` | MixedNum? (局部) | D | `ParseMixedNum(ref width!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#6` | `y` | MixedNum? (局部) | D | `ParseMixedNum(ref y!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#7` | `x` | MixedNum? (局部) | D | `ParseMixedNum(ref x!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#8` | `width` | MixedNum? (局部) | D | `case 0: ParseMixedNum(ref width!, tag, "width", tokens[i]); break;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#9` | `height` | MixedNum? (局部) | D | `case 1: ParseMixedNum(ref height!, tag, "height", tokens[i]); break;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#10` | `x` | MixedNum? (局部) | D | `case 0: ParseMixedNum(ref x!, tag, "xpos", tokens[i]); break;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#11` | `y` | MixedNum? (局部) | D | `case 1: ParseMixedNum(ref y!, tag, "ypos", tokens[i]); break;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#12` | `width` | MixedNum? (局部) | D | `case 2: ParseMixedNum(ref width!, tag, "width", tokens[i]); break;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#13` | `height` | MixedNum? (局部) | D | `case 3: ParseMixedNum(ref height!, tag, "height", tokens[i]); break;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#14` | `box` | StyledBoxModel? (局部) | D | `ParseParam4IntNum(ref CreateIfNull(ref box!).color!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#16` | `box` | StyledBoxModel? (局部) | D | `else if (!TryParseStyledBoxModel(ref box!, tag, word.Code, attrValue))` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#17` | `x` | MixedNum? (局部) | D | `state.CurrentDivTag = new HtmlDivTag(x!, y!, width, height, depth, color, box, isRelative);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#18` | `y` | MixedNum? (局部) | D | `state.CurrentDivTag = new HtmlDivTag(x!, y!, width, height, depth, color, box, isRelative);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#19` | `value` | string? (局部) | D | `buttonTag.ButtonValueStr = value!;` |
| `Emuera/UI/Game/Image/CroppedImage.cs#ASpriteSingle.GraphicsDraw#1` | `Bitmap` | Bitmap? (成员) | A | `g.DrawImage(Bitmap!, new Rectangle(offset, DestBaseSize), SrcRectangle, GraphicsUnit.Pixel);` |
| `Emuera/UI/Game/Image/CroppedImage.cs#ASpriteSingle.GraphicsDraw#2` | `Bitmap` | Bitmap? (成员) | A | `g.DrawImage(Bitmap!, destRect, SrcRectangle, GraphicsUnit.Pixel);` |
| `Emuera/UI/Game/Image/CroppedImage.cs#ASpriteSingle.GraphicsDraw#3` | `Bitmap` | Bitmap? (成员) | A | `g.DrawImage(Bitmap!, destRect, SrcRectangle.X, SrcRectangle.Y, SrcRectangle.Width, SrcRectangle.Height, GraphicsUnit.Pixel, attr);` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#2` | `css` | ConsoleStyledString? (局部) | A | `int widthLimit = divWidth - css!.PointX;` |

### `X.Member!` 成员访问(80 处)

| 站点 ID | 接收者 | 根声明类型 | 结论 | 源码 |
| --- | --- | --- | --- | --- |
| `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetFunctionMethod#1` | `func.Position` | FunctionLabelLine? (局部) | A | `throw new CodeEE(string.Format(treer.UsedNonMethodFunc.Text, func.Position!.Value.Filename, func.Position!.Value.LineNo));` |
| `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetFunctionMethod#2` | `func.Position` | FunctionLabelLine? (局部) | A | `throw new CodeEE(string.Format(treer.UsedNonMethodFunc.Text, func.Position!.Value.Filename, func.Position!.Value.LineNo));` |
| `Emuera/Runtime/Script/Data/LabelDictionary.cs#LabelDictionary.RemoveLabelWithPath#1` | `line.Position` | FunctionLabelLine (局部) | A | `static bool IsMatch(string fname, FunctionLabelLine line) => string.Equals(line.Position!.Value.Filename, fname, Config.Config.SCIgnoreCase);` |
| `Emuera/Runtime/Script/Data/StrForm.cs#StrForm.FromWordToken#1` | `SWT.Words` | SubWord (局部) | A | `wc = SWT.Words!;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#1` | `seniorLabel.Position` | FunctionLabelLine? (局部) | A | `string.Format(trerror.FuncIsAlreadyDefined.Text, label.LabelName, seniorLabel.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#2` | `seniorLabel.Position` | FunctionLabelLine? (局部) | A | `seniorLabel.Position!.Value.LineNo.ToString()), position, 1);` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.ParseScriptCore#1` | `label.Position` | FunctionLabelLine (局部) | A | `string filename = label.Position!.Value.Filename;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.printFunctionNotFoundWarning#1` | `line.Position` | InstructionLine (参数) | A | `string filename = line.Position!.Value.Filename;` |
| `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#1` | `wc.Pointer` | WordCollection (成员) | A | `wc.Pointer = wc.Pointer!.Next;` |
| `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#2` | `wc.Pointer` | WordCollection (成员) | B | `wc.Collection.Remove(wc.Pointer!.Previous!);` |
| `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#3` | `wc.Pointer!.Previous` | WordCollection (成员) | A | `wc.Collection.Remove(wc.Pointer!.Previous!);` |
| `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#4` | `macroWC.Pointer` | WordCollection (局部) | A | `macroWC.Pointer = macroWC.Pointer!.Next;` |
| `Emuera/Runtime/Script/Parser/WordCollection.cs#WordCollection.Next#2` | `Pointer!.Next` | LinkedListNode<Word>? (成员) | A | `return Pointer!.Next!.Value;` |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallEventFunction#2` | `line!.Position` | FunctionLabelLine? (局部) | A | `throw new CodeEE(string.Format(trerror.CalleventToNonEventFunc.Text, label, line!.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallEventFunction#3` | `called.CurrentLabel` | CalledFunction (局部) | A | `called.TopLabel = called.CurrentLabel!;` |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallFunction#1` | `labelline.Position` | FunctionLabelLine? (局部) | A | `throw new CodeEE(string.Format(trerror.CallToUserFunc.Text, labelline.LabelName, labelline.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.ClearFunctionList#1` | `called.CurrentLabel` | CalledFunction (局部) | A | `if (called.CurrentLabel!.hasPrivDynamicVar)` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.Begin#1` | `called.CurrentLabel` | CalledFunction (局部) | A | `if (called.CurrentLabel!.hasPrivDynamicVar)` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#1` | `called.CurrentLabel` | CalledFunction (局部) | A | `if (called.CurrentLabel!.hasPrivDynamicVar)` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#2` | `called.CurrentLabel` | CalledFunction (局部) | A | `FunctionLabelLine label = called.CurrentLabel!;` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#3` | `currentLine.Position` | LogicalLine? (成员) | A | `long line = currentLine.Position!.Value.LineNo;` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#4` | `label.Position` | FunctionLabelLine (局部) | A | `console.DebugAddTraceLog(string.Format(trsl.DebugTraceCall.Text, label.LabelName, label.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#1` | `call.CurrentLabel` | CalledFunction (参数) | A | `FunctionLabelLine label = call.CurrentLabel!;` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#2` | `exm.Process.getCurrentLine` | ExpressionMediator (成员) | A | `long line = exm.Process.getCurrentLine!.Position!.Value.LineNo;` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#3` | `exm.Process.getCurrentLine!.Position` | ExpressionMediator (成员) | A | `long line = exm.Process.getCurrentLine!.Position!.Value.LineNo;` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#4` | `label.Position` | FunctionLabelLine (局部) | A | `console.DebugAddTraceLog(string.Format(trsl.DebugTraceJump2.Text, label.LabelName, label.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#5` | `label.Position` | FunctionLabelLine (局部) | A | `console.DebugAddTraceLog(string.Format(trsl.DebugTraceCall2.Text, label.LabelName, label.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#6` | `label.Position` | FunctionLabelLine (局部) | A | `console.DebugAddTraceLog(string.Format(trsl.DebugTraceJump.Text, label.LabelName, label.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Process.SystemProc.cs#Process.endAutoSaveCallSaveInfo#1` | `vEvaluator.SAVEDATA_TEXT` | — | A | `if (!vEvaluator.SaveTo(saveTarget, vEvaluator.SAVEDATA_TEXT!))` |
| `Emuera/Runtime/Script/Process.SystemProc.cs#Process.endCallSaveInfo#1` | `vEvaluator.SAVEDATA_TEXT` | — | A | `if (!vEvaluator.SaveTo(saveTarget, vEvaluator.SAVEDATA_TEXT!))` |
| `Emuera/Runtime/Script/Process.cs#Process.Initialize#1` | `Program.AnalysisFiles` | — | A | `noError = await loader.LoadErbList(Program.AnalysisFiles!, labelDic);` |
| `Emuera/Runtime/Script/Process.cs#Process.checkInfiniteLoop#1` | `currentLine.Position` | LogicalLine? (局部) | A | `currentLine.Position!.Value.Filename, currentLine.Position.Value.LineNo, state.lineCount, elapsedTime);` |
| `Emuera/Runtime/Script/Process.cs#Process.handleExceptionInSystemProc#1` | `exc.StackTrace` | Exception (参数) | A | `string[] stack = exc.StackTrace!.Split('\n');` |
| `Emuera/Runtime/Script/Process.cs#Process.handleException#2` | `current.ParentLabelLine.Position` | LogicalLine? (参数) | A | `current.ParentLabelLine.Position!.Value.Filename, current.ParentLabelLine.Position.Value.LineNo.ToString()));` |
| `Emuera/Runtime/Script/Process.cs#Process.handleException#3` | `exc.StackTrace` | Exception (参数) | A | `string[] stack = exc.StackTrace!.Split('\n');` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#METHOD_ArgumentBuilder.CreateArgument#1` | `line.Function.Method` | InstructionLine (参数) | A | `string? errmes = line.Function.Method!.CheckArgumentType(line.Function.Name, args);` |
| `Emuera/Runtime/Script/Statements/ArgumentParser.cs#ArgumentParser.SetArgumentTo#1` | `line.Function.Instruction` | InstructionLine (成员) | A | `arg = line.Function.Instruction!.CreateArgument(line, GlobalStatic.EMediator);` |
| `Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.local#1` | `terms.Last` | LinkedList<AExpression?> (成员) | A | `if (term.GetOperandType() != terms.Last!.Value!.GetOperandType())` |
| `Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.local#2` | `terms.Last` | LinkedList<AExpression?> (成员) | A | `if (terms.Last!.Value!.IsInteger)` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#1` | `node.Attributes` | XmlNode (参数) | A | `case 0: node.Attributes!.Append(newAttr); break;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#3` | `attr!.OwnerElement` | XmlAttribute? (局部) | A | `case 1: attr!.OwnerElement!.Attributes.InsertBefore(newAttr, attr); break;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#5` | `attr!.OwnerElement` | XmlAttribute? (局部) | A | `case 2: attr!.OwnerElement!.Attributes.InsertAfter(newAttr, attr); break;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.GetIntValue#2` | `childNode.DocumentElement` | XmlDocument (局部) | A | `XmlElement newNode = childNode.DocumentElement!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.GetIntValue#3` | `child.Attributes` | XmlNode (局部) | A | `child.Attributes!.Append(attr);` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlRemoveNodeMethod.Remove#1` | `attr.OwnerElement` | XmlAttribute (局部) | A | `attr.OwnerElement!.Attributes.Remove(attr);` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlReplaceMethod.GetIntValue#2` | `newXml.DocumentElement` | XmlDocument (局部) | A | `XmlElement newNode = newXml.DocumentElement!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlReplaceMethod.GetIntValue#3` | `child.Attributes` | XmlNode (局部) | A | `child.Attributes!.Append(attr);` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#UnicodeMethod.GetStrValue#1` | `GlobalStatic.Process.getCurrentLine.Position` | — | A | `GlobalStatic.Process.getCurrentLine.Position!.Value.Filename,` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#CALLSHARP_Instruction.DoInstruction#2` | `pluginArgs[i].strValue` | PluginMethodParameter[] (局部) | D | `varTerm.SetValue(pluginArgs[i].strValue!, exm);` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#SIF_Instruction.SetJumpTo#1` | `func.Position` | InstructionLine (参数) | A | `if (func.JumpTo != null && func.Position!.Value.LineNo + 1 != func.NextLine.Position!.Value.LineNo)` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#SIF_Instruction.SetJumpTo#2` | `func.NextLine.Position` | InstructionLine (参数) | A | `if (func.JumpTo != null && func.Position!.Value.LineNo + 1 != func.NextLine.Position!.Value.LineNo)` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#BREAK_Instruction.DoInstruction#1` | `jumpTo.LoopCounter` | InstructionLine (局部) | C | `jumpTo.LoopCounter!.ChangeValue(jumpTo.LoopStep, exm);` |
| `Emuera/Runtime/Script/Statements/LogicalLine.cs#FunctionLabelLine.CompareTo#3` | `other.Position` | FunctionLabelLine? (成员) | A | `if (Position!.Value.LineNo != other.Position!.Value.LineNo)` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.GetLength#1` | `this.sizes` | int[] (局部) | A | `int[] sizes = this.sizes!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.GetLength#2` | `this.sizes` | int[] (局部) | A | `int[] sizes = this.sizes!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.CheckElement#1` | `this.sizes` | int[] (局部) | A | `int[] sizes = this.sizes!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.IsArrayRangeValid#1` | `this.sizes` | int[] (局部) | A | `int[] sizes = this.sizes!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#UserDefinedCharaInt2DVariableToken.SetValueAll#1` | `this.sizes` | int[] (局部) | A | `int[] sizes = this.sizes!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#UserDefinedCharaStr2DVariableToken.SetValueAll#1` | `this.sizes` | int[] (局部) | A | `int[] sizes = this.sizes!;` |
| `Emuera/Runtime/Utils/AssemblyData.cs#AssemblyData.AssemblyData#1` | `Environment.ProcessPath` | — | A | `ExePath = Environment.ProcessPath!;` |
| `Emuera/Runtime/Utils/AssemblyData.cs#AssemblyData.AssemblyData#2` | `typeof(AssemblyData).Assembly.GetName().Version` | — | A | `emueraVer = typeof(AssemblyData).Assembly.GetName().Version!;` |
| `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.ExecuteLine#2` | `func.Function.Instruction` | InstructionLine (局部) | C | `func.Function.Instruction!.DoInstruction(expressionMediator, func, processState);` |
| `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetStackTrace#1` | `processState.CurrentLine` | ProcessState (成员) | B | `stackTraceCollecter.Add(processState.CurrentLine!.Position!.Value.Filename + ":" + processState.CurrentLine.Position.Value.LineNo +` |
| `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetStackTrace#2` | `processState.CurrentLine!.Position` | ProcessState (成员) | A | `stackTraceCollecter.Add(processState.CurrentLine!.Position!.Value.Filename + ":" + processState.CurrentLine.Position.Value.LineNo +` |
| `Emuera/UI/Framework/Forms/ConfigDialog.cs#ConfigDialog.SaveConfig#1` | `(string)comboBox2.SelectedItem` | — | A | `config.GetRequiredConfigItem(ConfigCode.FontName).SetValue((string)comboBox2.SelectedItem!);` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseDown#1` | `console.SelectingButton` | EmueraConsole? (成员) | A | `GlobalStatic.Process.InputInteger(3, console.SelectingButton!.GetMappedColor(e.X, e.Y));` |
| `Emuera/UI/Game/ConsoleButtonString.cs#ConsoleButtonString.DivideAt#1` | `strArray[cssIndex].Text` | AConsoleDisplayNode[] (成员) | A | `int length = strArray[cssIndex].Text!.Length;` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OnPaint#1` | `p.Parent` | AConsoleDisplayNode (局部) | A | `int baseLineNo = p.Parent!.ParentLine.LineNo;` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OnPaint#2` | `System.Threading.SynchronizationContext.Current` | — | A | `System.Threading.SynchronizationContext context = System.Threading.SynchronizationContext.Current!;` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OnPaint#3` | `Cursor.Current` | — | A | `Point p = new(mousePos.X + 2, mousePos.Y + Cursor.Current!.Size.Height);` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#4` | `css.Text` | ConsoleStyledString (局部) | A | `rikaichan.laststr = css.Text!;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.DisplayLine2Html#1` | `buttons[buttonCounter].Title` | ConsoleButtonString[] (局部) | A | `titleValue = Escape(buttons[buttonCounter].Title!);` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.DisplayLine2Html#3` | `css.Text` | ConsoleStyledString? (局部) | A | `b.Append(Escape(css.Text!));` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#15` | `CreateIfNull(refbox!).color` | — | D | `ParseParam4IntNum(ref CreateIfNull(ref box!).color!, tag, word.Code, attrValue);` |
| `Emuera/UI/Game/Image/AppContents.cs#AppContents.LoadContents#1` | `PurePath.From(path).Directory` | — | A | `PurePath directory = PurePath.From(path).Directory!;` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.BufferStrLength#1` | `css.Text` | AConsoleDisplayNode (局部) | A | `length += css.Text!.Length;` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.createButtons#1` | `css.Text` | AConsoleDisplayNode (局部) | A | `if (cssStartCharIndex + css.Text!.Length >= buttonEndCharIndex)` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#1` | `css.Text` | AConsoleDisplayNode (局部) | A | `strLength += css.Text!.Length;` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#3` | `css.Text` | ConsoleStyledString? (局部) | A | `string str = css.Text!;` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#4` | `css.Font` | ConsoleStyledString? (局部) | B | `Font font = css.Font!;` |

### 其他表达式(`other-expr`,2 处)

| 站点 ID | 接收者 | 根声明类型 | 结论 | 源码 |
| --- | --- | --- | --- | --- |
| `Emuera/Runtime/Config/JSON/JSONConfig.cs#JSONConfig.Load#1` | `(json)` | string (局部) | C | `Data = JsonSerializer.Deserialize<JSONConfigData>(json)!;` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.SetMaximizedBounds#1` | `(Screen.FromControl(this)??Screen.PrimaryScreen)` | — | A | `Screen screen = (Screen.FromControl(this) ?? Screen.PrimaryScreen)!;` |

### `F(...)!` 方法调用结果(64 处)

| 站点 ID | 接收者 | 根声明类型 | 结论 | 源码 |
| --- | --- | --- | --- | --- |
| `Emuera/Runtime/Config/Config.cs#Config.DefaultFont#1` | `FontFactory.GetFont(,FontStyle.Regular)` | — | C | `public static Font DefaultFont => FontFactory.GetFont("", FontStyle.Regular)!;` |
| `Emuera/Runtime/Config/ConfigData.cs#ConfigData.GetRequiredItem#1` | `GetItem(code)` | — | A | `return GetItem(code)!;` |
| `Emuera/Runtime/Config/ConfigData.cs#ConfigData.GetRequiredConfigItem#1` | `GetConfigItem(code)` | — | A | `return GetConfigItem(code)!;` |
| `Emuera/Runtime/Config/ConfigData.cs#ConfigData.code#1` | `(ConfigItem<T>)GetConfigItem(code)` | — | A | `return (ConfigItem<T>)GetConfigItem(code)!;` |
| `Emuera/Runtime/Config/ConfigData.cs#ConfigData.GetRequiredDebugItem#1` | `GetDebugItem(code)` | — | A | `return GetDebugItem(code)!;` |
| `Emuera/Runtime/Config/ConfigData.cs#ConfigData.code#2` | `(ConfigItem<T>)GetDebugItem(code)` | — | A | `return (ConfigItem<T>)GetDebugItem(code)!;` |
| `Emuera/Runtime/Config/ConfigItem.cs#ConfigItem<T>.ValueToString#2` | `val!.ToString()` | T (成员) | A | `return val!.ToString()!;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#3` | `labelDic.GetLabelDollar(gotoLabel.LabelName,lastLabelLine)` | LabelDictionary (成员) | A | `ScriptPosition? pos = labelDic.GetLabelDollar(gotoLabel.LabelName, lastLabelLine)!.Position;` |
| `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#1` | `label.PopRowArgs()` | FunctionLabelLine (成员) | A | `WordCollection wc = label.PopRowArgs()!;` |
| `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.AnalyseYenAt#1` | `GlobalStatic.Process.GetScaningLine()` | — | A | `ParserMediator.Warn(string.Format(trerror.NotFoundCorresponding.Text, "\\@", "#"), GlobalStatic.Process.GetScaningLine()!, 1, false,` |
| `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#1` | `line.PopArgumentPrimitive()` | InstructionLine (局部) | A | `ReadOnlySpan<char> statementsSpan = line.PopArgumentPrimitive()!.SubstringROS();` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#ArgumentBuilder.popWords#1` | `line.PopArgumentPrimitive()` | InstructionLine (成员) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#ArgumentBuilder.popTerms#1` | `line.PopArgumentPrimitive()` | InstructionLine (成员) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_HTML_PRINT_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_DT_COLUMN_OPTIONS_ArgumentBuilder.CreateArgument#1` | `wc.Current.ToString()` | WordCollection (局部) | A | `string keyword = wc.Current.ToString()!.ToLower();` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINTV_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_TIMES_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#FORM_STR_ANY_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#VOID_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#STR_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#FORM_STR_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_VAR_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALL_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALLSHARP_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_SET_ArgumentBuilder.CreateArgument#1` | `line.PopAssignmentDestStr()` | InstructionLine (参数) | A | `WordCollection destWc = line.PopAssignmentDestStr()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_SET_ArgumentBuilder.CreateArgument#2` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_INPUTS_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#STR_DOUBLE_ArgumentBuilder.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/ExpressionMediator.cs#ExpressionMediator.ConvertStringType#1` | `Strings.StrConv(str,VbStrConv.Katakana,0x0411)` | — | D | `return Strings.StrConv(str, VbStrConv.Katakana, 0x0411)!;` |
| `Emuera/Runtime/Script/Statements/ExpressionMediator.cs#ExpressionMediator.ConvertStringType#2` | `Strings.StrConv(str,VbStrConv.Hiragana\|VbStrConv.Wide,0x0411)` | — | D | `return Strings.StrConv(str, VbStrConv.Hiragana \| VbStrConv.Wide, 0x0411)!;` |
| `Emuera/Runtime/Script/Statements/ExpressionMediator.cs#ExpressionMediator.ConvertStringType#3` | `Strings.StrConv(str,VbStrConv.Hiragana,0x0411)` | — | D | `return Strings.StrConv(str, VbStrConv.Hiragana, 0x0411)!;` |
| `Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.reduceTerm#1` | `GlobalStatic.Process.GetScaningLine()` | — | A | `ParserMediator.Warn(trerror.ComparisonOpContinuous.Text, GlobalStatic.Process.GetScaningLine()!, 0, false, false);` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlGetMethod.GetIntValue#1` | `doc.SelectNodes(path)` | XmlDocument? (局部) | A | `nodes = doc.SelectNodes(path)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlSetMethod.GetIntValue#1` | `doc.SelectNodes(path)` | XmlDocument (局部) | A | `nodes = doc.SelectNodes(path)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.GetIntValue#1` | `doc.SelectNodes(path)` | XmlDocument (局部) | A | `nodes = doc.SelectNodes(path)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlRemoveNodeMethod.GetIntValue#1` | `doc.SelectNodes(path)` | XmlDocument (局部) | A | `nodes = doc.SelectNodes(path)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlReplaceMethod.GetIntValue#1` | `doc.SelectNodes(path)` | XmlDocument? (局部) | A | `nodes = doc.SelectNodes(path)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#DataTableCellGetMethod.GetStrValue#1` | `v.ToString()` | object (局部) | A | `return v.ToString()!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MapFromXmlMethod.GetIntValue#1` | `doc.SelectNodes()` | XmlDocument (局部) | A | `nodes = doc.SelectNodes("/map/p")!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MapFromXmlMethod.GetIntValue#2` | `node.SelectNodes()` | XmlNode (局部) | A | `XmlNodeList key = node.SelectNodes("./k")!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MapFromXmlMethod.GetIntValue#3` | `node.SelectNodes()` | XmlNode (局部) | A | `XmlNodeList val = node.SelectNodes("./v")!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MesSkipMethod.CheckArgumentType#1` | `GlobalStatic.Process.GetScaningLine()` | — | A | `ParserMediator.Warn(string.Format(trerror.FuncDeprecated.Text, name, "MESSKIP"), GlobalStatic.Process.GetScaningLine()!, 1, false,` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#StrChangeStyleMethod.GetStrValue#1` | `Microsoft.VisualBasic.Strings.StrConv(str,Microsoft.VisualBasic.VbStrConv.Narrow,Config.Language)` | — | D | `return Microsoft.VisualBasic.Strings.StrConv(str, Microsoft.VisualBasic.VbStrConv.Narrow, Config.Language)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#StrChangeStyleMethod.GetStrValue#2` | `Microsoft.VisualBasic.Strings.StrConv(str,Microsoft.VisualBasic.VbStrConv.Wide,Config.Language)` | — | D | `return Microsoft.VisualBasic.Strings.StrConv(str, Microsoft.VisualBasic.VbStrConv.Wide, Config.Language)!;` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ExistFunctionMethod.GetIntValue#1` | `GlobalStatic.LabelDictionary.GetNonEventLabel(funcname)` | — | A | `FunctionLabelLine func = GlobalStatic.LabelDictionary.GetNonEventLabel(funcname)!;` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#VARI_Instruction.DoInstruction#1` | `func.ParentLabelLine!.GetPrivateVariable(varName)` | InstructionLine (参数) | A | `UserDefinedVariableToken privateVar = func.ParentLabelLine!.GetPrivateVariable(varName)!;` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#VARS_Instruction.DoInstruction#1` | `func.ParentLabelLine!.GetPrivateVariable(varName)` | InstructionLine (参数) | A | `UserDefinedVariableToken privateVar = func.ParentLabelLine!.GetPrivateVariable(varName)!;` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#CUSTOMDRAWLINE_Instruction.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/Instraction.Child.cs#CALLSHARP_Instruction.CreateArgument#1` | `line.PopArgumentPrimitive()` | InstructionLine (参数) | A | `CharStream st = line.PopArgumentPrimitive()!;` |
| `Emuera/Runtime/Script/Statements/LogicalLine.cs#LogicalLine.ToString#1` | `base.ToString()` | — | A | `return base.ToString()!;` |
| `Emuera/Runtime/Script/Statements/Variable/VariableIdentifier.cs#VariableIdentifier.VariableIdentifier#1` | `Enum.GetName(VariableCode.__FILE__)` | — | A | `nameDic.Add(Enum.GetName(VariableCode.__FILE__)!, VariableCode.__FILE__);` |
| `Emuera/Runtime/Script/Statements/Variable/VariableIdentifier.cs#VariableIdentifier.VariableIdentifier#2` | `Enum.GetName(VariableCode.__LINE__)` | — | A | `nameDic.Add(Enum.GetName(VariableCode.__LINE__)!, VariableCode.__LINE__);` |
| `Emuera/Runtime/Script/Statements/Variable/VariableIdentifier.cs#VariableIdentifier.VariableIdentifier#3` | `Enum.GetName(VariableCode.__FUNCTION__)` | — | A | `nameDic.Add(Enum.GetName(VariableCode.__FUNCTION__)!, VariableCode.__FUNCTION__);` |
| `Emuera/Runtime/Utils/EraStreamReader.cs#EraStreamReader.OpenOnCache#1` | `Preload.GetFileLines(path)` | — | C | `_fileLines = Preload.GetFileLines(path)!;` |
| `Emuera/Runtime/Utils/EvilMask/Lang.cs#Lang.loadLangXML#1` | `xml.SelectNodes()` | XmlDocument (成员) | A | `XmlNodeList nodes = xml.SelectNodes("/lang/tr")!;` |
| `Emuera/Runtime/Utils/PathUtils/PurePath.cs#PurePath.ChangeExtension#1` | `Path.ChangeExtension(value,extension)` | — | A | `return new PurePath(Normalize(Path.ChangeExtension(value, extension)!));` |
| `Emuera/UI/Framework/Forms/DebugDialog.cs#DebugDialog.getValueString#2` | `value.ToString()` | SingleTerm (局部) | A | `return value.ToString()!;` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#4` | `Clipboard.GetDataObject()` | — | A | `if (Clipboard.GetDataObject()!.GetDataPresent(DataFormats.Text))` |
| `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow._Click#1` | `Clipboard.GetDataObject()` | — | A | `if (Clipboard.GetDataObject()!.GetDataPresent(DataFormats.Text))` |
| `Emuera/UI/Game/EmueraConsole.Print.cs#EmueraConsole.deleteLine#1` | `BufferToSingleLine(true,false)` | — | B | `ConsoleDisplayLine dummyline = BufferToSingleLine(true, false)!;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.Html2DisplayLine#1` | `html2DisplayLine(str,sm,console,null,null)` | — | A | `return html2DisplayLine(str, sm, console, null, null)!;` |
| `Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#4` | `html2DisplayLine(null,sm,console,newHtmlParentInfo{HasComment=hasComment,HasReturn=hasReturn,State=state,Stream=st},null)` | — | A | `new HtmlParentInfo { HasComment = hasComment, HasReturn = hasReturn, State = state, Stream = st }, null)!;` |
| `Emuera/UI/Game/Image/ConstImage.cs#ConstImage.Load#1` | `ImgUtils.LoadImage(Filepath)` | — | D | `RealBitmap = ImgUtils.LoadImage(Filepath)!;` |
| `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.ButtonsToDisplayLines#1` | `buttonList[i].DivideAt(divIndex,stringMeasure)` | List<ConsoleButtonString> (参数) | A | `ConsoleButtonString newButton = buttonList[i].DivideAt(divIndex, stringMeasure)!;` |

### `X![i]` 断言后索引(30 处)

| 站点 ID | 接收者 | 根声明类型 | 结论 | 源码 |
| --- | --- | --- | --- | --- |
| `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.ShiftNext#1` | `eventLabelList` | List<FunctionLabelLine>[]? (成员) | A | `if (eventLabelList![group].Count > counter)` |
| `Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs#FunctionMethod.CheckArgumentTypeEx#2` | `vs` | _ArgType[]? (局部) | A | `_ArgType rule = variadic && i >= list.ArgTypes.Count ? vs![(i - list.ArgTypes.Count) % vs.Length] : list.ArgTypes[i];` |
| `Emuera/Runtime/Utils/EvilMask/Lang.cs#Lang.loadLangXML#2` | `nodes[i]!.Attributes` | XmlNodeList (局部) | A | `XmlAttribute? attr = nodes[i]!.Attributes!["id"];` |
| `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#3` | `radius` | int[] (参数) | A | `flipY ? rect.Y + (rect.Height / 2) : rect.Y + radius![corner],` |
| `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#4` | `radius` | int[] (参数) | A | `(rect.Height / 2) - radius![corner]));` |
| `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#5` | `radius` | int[] (参数) | A | `flipX ? rect.X + (rect.Width / 2) : rect.X + radius![corner],` |
| `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#6` | `radius` | int[] (参数) | A | `(rect.Width / 2) - (flipX ? radius![corner] : 0),` |
| `Emuera/Runtime/Utils/EvilMask/Utils.cs#Utils.MixedNum4ToInt4#1` | `nums` | MixedNum[]? (成员) | A | `nums![i] = MixedNum.ToPixel(mnums[i]);` |
| `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.SetCharVar#1` | `dict` | Dictionary<string, int>? (局部) | A | `variable.SetValue(value, [charId, dict![key]]);` |
| `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetCharVar#1` | `dict` | Dictionary<string, int>? (局部) | A | `return variable.GetIntValue(expressionMediator, [charId, dict![key]]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalInt1dWrapper.Get#1` | `dict` | Dictionary<string, int>? (局部) | A | `return Get(dict![key]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalInt1dWrapper.Set#1` | `dict` | Dictionary<string, int>? (局部) | A | `Set(dict![key], value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalString1dWrapper.Get#1` | `dict` | Dictionary<string, int>? (局部) | A | `return Get(dict![key]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalString1dWrapper.Set#1` | `dict` | Dictionary<string, int>? (局部) | A | `Set(dict![key], value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalConstString1dWrapper.Get#1` | `dict` | Dictionary<string, int>? (局部) | A | `return Get(dict![key]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalConstInt1dWrapper.Get#1` | `dict` | Dictionary<string, int>? (局部) | A | `return Get(dict![key]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt1dWrapper.Get#1` | `dict` | Dictionary<string, int>? (局部) | A | `return Get(dict![key]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt1dWrapper.Set#1` | `dict` | Dictionary<string, int>? (局部) | A | `Set(dict![key], value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#1` | `dictA` | Dictionary<string, int>? (局部) | A | `return Get(dictA![keyA], dictB![keyB]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#2` | `dictB` | Dictionary<string, int>? (局部) | A | `return Get(dictA![keyA], dictB![keyB]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#3` | `dictB` | Dictionary<string, int>? (局部) | A | `return Get(keyA, dictB![keyB]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#4` | `dictA` | Dictionary<string, int>? (局部) | A | `return Get(dictA![keyA], keyB);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#1` | `dictA` | Dictionary<string, int>? (局部) | A | `Set(dictA![keyA], dictB![keyB], value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#2` | `dictB` | Dictionary<string, int>? (局部) | A | `Set(dictA![keyA], dictB![keyB], value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#3` | `dictB` | Dictionary<string, int>? (局部) | A | `Set(keyA, dictB![keyB], value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#4` | `dictA` | Dictionary<string, int>? (局部) | A | `Set(dictA![keyA], keyB, value);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharString1dWrapper.Get#1` | `dict` | Dictionary<string, int>? (局部) | A | `return Get(dict![key]);` |
| `Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharString1dWrapper.Set#1` | `dict` | Dictionary<string, int>? (局部) | A | `Set(dict![key], value);` |
| `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#2` | `escapedParts` | Dictionary<int, List<AConsoleDisplayNode>> (成员) | A | `foreach (AConsoleDisplayNode part in escapedParts![depth])` |
| `Emuera/UI/Game/Rikaichan.cs#Rikaichan.OnPaint#1` | `edict` | byte[] (成员) | A | `while (edict![offset + temp_len] != '\n')` |

### `default!`(1 处)

| 站点 ID | 接收者 | 根声明类型 | 结论 | 源码 |
| --- | --- | --- | --- | --- |
| `Emuera/Runtime/Script/Statements/CircularBuffer.cs#CircularBuffer<T>.Dequeue#1` | `default` | — | D | `_buffer[_tail] = default!; //default(T) is null for reference type arguments, which only releases the slot's reference` |

## 判定分组索引(按接收者根标识符的声明类型)

本索引是 issues/21 第 2 步「按接收者类型分组判定」的工作单位:同组判据相同。组名形如「<接收者根标识符的声明类型> <成员名>」;声明类型未能解析时记 `?` 或 `(未解析)`,组名退化为可还原的成员名。

| 组(声明类型) | 处数 | 子桶 | 站点 |
| --- | --- | --- | --- |
| Dictionary<string, int>? (局部) | 21 | bare/member / X![i] | `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.KeywordToInteger#1`<br>`Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.SetCharVar#1`<br>`Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetCharVar#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalInt1dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalInt1dWrapper.Set#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalString1dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalString1dWrapper.Set#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalConstString1dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#GlobalConstInt1dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt1dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt1dWrapper.Set#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#2`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#3`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Get#4`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#2`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#3`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharInt2dWrapper.Set#4`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharString1dWrapper.Get#1`<br>`Emuera/Runtime/Utils/PluginSystem/VariableDataWrappers.cs#CharString1dWrapper.Set#1` |
| F(...)! line.PopArgumentPrimitive() | 18 | F(...)! | `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#ArgumentBuilder.popWords#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#ArgumentBuilder.popTerms#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_HTML_PRINT_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINTV_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_TIMES_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#FORM_STR_ANY_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#VOID_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#STR_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#FORM_STR_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_VAR_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALL_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALLSHARP_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_SET_ArgumentBuilder.CreateArgument#2`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_INPUTS_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#STR_DOUBLE_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/Instraction.Child.cs#CUSTOMDRAWLINE_Instruction.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/Instraction.Child.cs#CALLSHARP_Instruction.CreateArgument#1` |
| AExpression? (局部) | 16 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#3`<br>`Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#4`<br>`Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#1`<br>`Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#2`<br>`Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#3`<br>`Emuera/Runtime/Script/Process.ScriptProc.cs#Process.doNormalFunction#4`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_IMG_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#2`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#1`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#3`<br>`Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.reduceIdentifier#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ArrayMultiSortExMethod.GetConvertedTerm#1`<br>`Emuera/Runtime/Script/Statements/Instraction.Child.cs#PRINT_Instruction.DoInstruction#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableParser.cs#VariableParser.ReduceVariable#1`<br>`Emuera/UI/Framework/Forms/DebugDialog.cs#DebugDialog.getValueString#1` |
| EmueraConsole? (成员) | 15 | bare/member | `Emuera/Runtime/Script/Data/ParserMediator.cs#ParserMediator.FlushWarningList#1`<br>`Emuera/Runtime/Script/Data/ParserMediator.cs#ParserMediator.SingleLine#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#2`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#3`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#5`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#6`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.Init#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseClickCBCheck#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseClickCBCheck#2`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseDoubleClickCBCheck#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseLeave#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ToolStripMenuItem_Click#1`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ToolStripMenuItem_Click#2`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ToolStripMenuItem_Click#3` |
| MixedNum? (局部) | 15 | bare/member | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#1`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#2`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#3`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#4`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#5`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#6`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#7`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#8`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#9`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#10`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#11`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#12`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#13`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#17`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#18` |
| AExpression? (成员) | 14 | bare/member | `Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.Reduce#1`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.Reduce#2`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#1`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#2`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#3`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.ToString#4`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#1`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#2`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#3`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#4`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#5`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#6`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#7`<br>`Emuera/Runtime/Script/Statements/CaseExpression.cs#CaseExpression.GetBool#8` |
| (未解析) Text | 8 | bare/member | `Emuera/UI/Game/ConsoleImagePart.cs#ConsoleImagePart.SetWidth#1`<br>`Emuera/UI/Game/ConsoleShapePart.cs#ConsoleErrorShapePart.DrawTo#1`<br>`Emuera/UI/Game/ConsoleShapePart.cs#ConsoleErrorShapePart.SetWidth#1`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DivideAt#1`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#1`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#3`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawTo#1`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawToBitmap#1` |
| CircularBuffer<string>? (成员) | 7 | bare/member | `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.ScrollUp#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.AddLine#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.DelLine#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.ClearScreen#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.ClearScreen#2`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.SendToCB#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.SendToCB#2` |
| FunctionLabelLine? (局部) .Position | 6 | bare/member | `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetFunctionMethod#1`<br>`Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetFunctionMethod#2`<br>`Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#1`<br>`Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#2`<br>`Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallEventFunction#2`<br>`Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallFunction#1` |
| FunctionLabelLine (局部) .Position | 6 | bare/member | `Emuera/Runtime/Script/Data/LabelDictionary.cs#LabelDictionary.RemoveLabelWithPath#1`<br>`Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.ParseScriptCore#1`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#4`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#4`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#5`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#6` |
| F(...)! doc.SelectNodes() | 6 | F(...)! | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlGetMethod.GetIntValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlSetMethod.GetIntValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.GetIntValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlRemoveNodeMethod.GetIntValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlReplaceMethod.GetIntValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MapFromXmlMethod.GetIntValue#1` |
| int[] (局部) .sizes | 6 | bare/member | `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.GetLength#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.GetLength#2`<br>`Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.CheckElement#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#CharaVariableToken.IsArrayRangeValid#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#UserDefinedCharaInt2DVariableToken.SetValueAll#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#UserDefinedCharaStr2DVariableToken.SetValueAll#1` |
| CalledFunction (局部) .CurrentLabel | 5 | bare/member | `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallEventFunction#3`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.ClearFunctionList#1`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.Begin#1`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#1`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#2` |
| string? (参数) | 4 | bare/member | `Emuera/Runtime/Script/Statements/Argument.cs#CallformCache.Store#1`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#1`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#2`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#3` |
| string? (局部) | 4 | bare/member | `Emuera/Runtime/Script/Statements/Instraction.Child.cs#SELECTCASE_Instruction.DoInstruction#1`<br>`Emuera/Runtime/Utils/EraDataStream.cs#EraDataReader.ReadInt64Array3DExtended#1`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlAnalzeState.GetSS#1`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#19` |
| int[] (参数) | 4 | X![i] | `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#3`<br>`Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#4`<br>`Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#5`<br>`Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#6` |
| Bitmap? (局部) | 4 | bare/member | `Emuera/Runtime/Utils/WebPWrapper.cs#WebP.Decode#1`<br>`Emuera/Runtime/Utils/WebPWrapper.cs#WebP.Decode#2`<br>`Emuera/Runtime/Utils/WebPWrapper.cs#WebP.GetThumbnailFast#1`<br>`Emuera/Runtime/Utils/WebPWrapper.cs#WebP.GetThumbnailQuality#1` |
| Font? (成员) | 4 | bare/member | `Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#2`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.SetWidth#4`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawTo#2`<br>`Emuera/UI/Game/ConsoleStyledString.cs#ConsoleStyledString.DrawToBitmap#2` |
| LogicalLine? (局部) | 3 | bare/member | `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetVariableToken#1`<br>`Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetVariableToken#2`<br>`Emuera/Runtime/Script/Process.ScriptProc.cs#Process.runScriptProc#1` |
| InstructionLine (参数) .Position | 3 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.printFunctionNotFoundWarning#1`<br>`Emuera/Runtime/Script/Statements/Instraction.Child.cs#SIF_Instruction.SetJumpTo#1`<br>`Emuera/Runtime/Script/Statements/Instraction.Child.cs#SIF_Instruction.SetJumpTo#2` |
| F(...)! GlobalStatic.Process.GetScaningLine() | 3 | F(...)! | `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.AnalyseYenAt#1`<br>`Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.reduceTerm#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MesSkipMethod.CheckArgumentType#1` |
| FunctionLabelLine? (成员) | 3 | bare/member | `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallLabel#1`<br>`Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.IsOnly#1`<br>`Emuera/Runtime/Script/Statements/LogicalLine.cs#FunctionLabelLine.CompareTo#1` |
| ExpressionMediator (成员) | 3 | bare/member | `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#7`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#8`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#9` |
| System.Timers.Timer? (成员) | 3 | bare/member | `Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.Reset#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.SetTimerInterval#1`<br>`Emuera/Runtime/Script/Statements/Clipboard.cs#ClipboardProcessor.MinTimeCheck#1` |
| F(...)! Strings.StrConv() | 3 | F(...)! | `Emuera/Runtime/Script/Statements/ExpressionMediator.cs#ExpressionMediator.ConvertStringType#1`<br>`Emuera/Runtime/Script/Statements/ExpressionMediator.cs#ExpressionMediator.ConvertStringType#2`<br>`Emuera/Runtime/Script/Statements/ExpressionMediator.cs#ExpressionMediator.ConvertStringType#3` |
| F(...)! Enum.GetName() | 3 | F(...)! | `Emuera/Runtime/Script/Statements/Variable/VariableIdentifier.cs#VariableIdentifier.VariableIdentifier#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableIdentifier.cs#VariableIdentifier.VariableIdentifier#2`<br>`Emuera/Runtime/Script/Statements/Variable/VariableIdentifier.cs#VariableIdentifier.VariableIdentifier#3` |
| Bitmap? (成员) | 3 | bare/member | `Emuera/UI/Game/Image/CroppedImage.cs#ASpriteSingle.GraphicsDraw#1`<br>`Emuera/UI/Game/Image/CroppedImage.cs#ASpriteSingle.GraphicsDraw#2`<br>`Emuera/UI/Game/Image/CroppedImage.cs#ASpriteSingle.GraphicsDraw#3` |
| AConsoleDisplayNode (局部) .Text | 3 | bare/member | `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.BufferStrLength#1`<br>`Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.createButtons#1`<br>`Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#1` |
| F(...)! GetConfigItem() | 2 | F(...)! | `Emuera/Runtime/Config/ConfigData.cs#ConfigData.GetRequiredConfigItem#1`<br>`Emuera/Runtime/Config/ConfigData.cs#ConfigData.code#1` |
| F(...)! GetDebugItem() | 2 | F(...)! | `Emuera/Runtime/Config/ConfigData.cs#ConfigData.GetRequiredDebugItem#1`<br>`Emuera/Runtime/Config/ConfigData.cs#ConfigData.code#2` |
| EmueraConsole (成员) | 2 | bare/member | `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.loadDataTo#1`<br>`Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.loadAliases#1` |
| LogicalLine? (成员) | 2 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.addLine#1`<br>`Emuera/Runtime/Script/Process.State.cs#ProcessState.ShiftNextLine#1` |
| SingleTerm? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#5`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#GetConfigMethod.GetSingleTerm#1` |
| WordCollection (成员) .Pointer | 2 | bare/member | `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#1`<br>`Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#2` |
| FunctionLabelLine? (参数) | 2 | bare/member | `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#2`<br>`Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#3` |
| LinkedListNode<Word>? (成员) | 2 | bare/member | `Emuera/Runtime/Script/Parser/WordCollection.cs#WordCollection.Next#1`<br>`Emuera/Runtime/Script/Parser/WordCollection.cs#WordCollection.Remove#1` |
| ? .SAVEDATA_TEXT | 2 | bare/member | `Emuera/Runtime/Script/Process.SystemProc.cs#Process.endAutoSaveCallSaveInfo#1`<br>`Emuera/Runtime/Script/Process.SystemProc.cs#Process.endCallSaveInfo#1` |
| Exception (参数) .StackTrace | 2 | bare/member | `Emuera/Runtime/Script/Process.cs#Process.handleExceptionInSystemProc#1`<br>`Emuera/Runtime/Script/Process.cs#Process.handleException#3` |
| List<AExpression?>? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALL_ArgumentBuilder.CreateArgument#2`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_CALLSHARP_ArgumentBuilder.CreateArgument#2` |
| ReferenceToken? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#2`<br>`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_REF_ArgumentBuilder.CreateArgument#4` |
| LinkedList<AExpression?> (成员) .Last | 2 | bare/member | `Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.local#1`<br>`Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.local#2` |
| XmlAttribute? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#2`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#4` |
| XmlAttribute? (局部) .OwnerElement | 2 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#3`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#5` |
| XmlDocument (局部) .DocumentElement | 2 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.GetIntValue#2`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlReplaceMethod.GetIntValue#2` |
| XmlNode (局部) .Attributes | 2 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.GetIntValue#3`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlReplaceMethod.GetIntValue#3` |
| F(...)! node.SelectNodes() | 2 | F(...)! | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MapFromXmlMethod.GetIntValue#2`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#MapFromXmlMethod.GetIntValue#3` |
| F(...)! Microsoft.VisualBasic.Strings.StrConv() | 2 | F(...)! | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#StrChangeStyleMethod.GetStrValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#StrChangeStyleMethod.GetStrValue#2` |
| Regex? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ReplaceMethod.GetStrValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ReplaceMethod.GetStrValue#2` |
| ASprite? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#CBGSETButtonSpriteMethod.GetIntValue#1`<br>`Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#CBGSETButtonSpriteMethod.GetIntValue#2` |
| F(...)! func.ParentLabelLine!.GetPrivateVariable() | 2 | F(...)! | `Emuera/Runtime/Script/Statements/Instraction.Child.cs#VARI_Instruction.DoInstruction#1`<br>`Emuera/Runtime/Script/Statements/Instraction.Child.cs#VARS_Instruction.DoInstruction#1` |
| UserDefinedCharaVariableToken? (局部) | 2 | bare/member | `Emuera/Runtime/Script/Statements/Variable/VariableData.cs#VariableData.CreateUserDefCharaVariable#1`<br>`Emuera/Runtime/Script/Statements/Variable/VariableData.cs#VariableData.CreateUserDefCharaVariable#2` |
| GraphicsPath? (局部) | 2 | bare/member | `Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#1`<br>`Emuera/Runtime/Utils/EvilMask/Shape.cs#BoxBorder.drawRoundedCornerBorderLeftTop#2` |
| F(...)! Clipboard.GetDataObject() | 2 | F(...)! | `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.ProcessCmdKey#4`<br>`Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow._Click#1` |
| BackgroundWorker? (局部) | 2 | bare/member | `Emuera/UI/Framework/Forms/RikaiDialog.cs#RikaiDialog.bwDoWork#1`<br>`Emuera/UI/Framework/Forms/RikaiDialog.cs#RikaiDialog.bwDoWork#2` |
| string (参数) | 2 | bare/member | `Emuera/UI/Game/EmueraConsole.Print.cs#EmueraConsole.CreateTypeCString#1`<br>`Emuera/UI/Game/EmueraConsole.Print.cs#EmueraConsole.CreateTypeCString#2` |
| ? .Current | 2 | bare/member | `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OnPaint#2`<br>`Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OnPaint#3` |
| Dictionary<int, List<AConsoleDisplayNode>> (成员) | 2 | bare/member / X![i] | `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#1`<br>`Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#2` |
| ConsoleStyledString? (局部) | 2 | bare/member | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.DisplayLine2Html#2`<br>`Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#2` |
| ConsoleStyledString? (局部) .Text | 2 | bare/member | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.DisplayLine2Html#3`<br>`Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#3` |
| F(...)! html2DisplayLine() | 2 | F(...)! | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.Html2DisplayLine#1`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.html2DisplayLine#4` |
| StyledBoxModel? (局部) | 2 | bare/member | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#14`<br>`Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#16` |
| F(...)! FontFactory.GetFont() | 1 | F(...)! | `Emuera/Runtime/Config/Config.cs#Config.DefaultFont#1` |
| F(...)! GetItem() | 1 | F(...)! | `Emuera/Runtime/Config/ConfigData.cs#ConfigData.GetRequiredItem#1` |
| T (成员) | 1 | bare/member | `Emuera/Runtime/Config/ConfigItem.cs#ConfigItem<T>.ValueToString#1` |
| F(...)! val!.ToString() | 1 | F(...)! | `Emuera/Runtime/Config/ConfigItem.cs#ConfigItem<T>.ValueToString#2` |
| string (局部) | 1 | bare/member | `Emuera/Runtime/Config/JSON/JSONConfig.cs#JSONConfig.Load#1` |
| Dictionary<int, long>? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Data/ConstantData.cs#ConstantData.toCharacterTemplate#1` |
| SubWord (局部) .Words | 1 | bare/member | `Emuera/Runtime/Script/Data/StrForm.cs#StrForm.FromWordToken#1` |
| F(...)! labelDic.GetLabelDollar() | 1 | F(...)! | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#3` |
| ScriptPosition? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.loadErb#4` |
| F(...)! label.PopRowArgs() | 1 | F(...)! | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#1` |
| SymbolWord? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.parseLabel#2` |
| InstructionLine? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Loader/ErbLoader.cs#ErbLoader.nestCheck#1` |
| WordCollection (成员) .Previous | 1 | bare/member | `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#3` |
| WordCollection (局部) .Pointer | 1 | bare/member | `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs#LexicalAnalyzer.expandFunctionlikeMacro#4` |
| (未解析) Words | 1 | bare/member | `Emuera/Runtime/Script/Parser/SubWord.cs#YenAtSubWord.SetIsMacro#1` |
| StrFormWord? (成员) | 1 | bare/member | `Emuera/Runtime/Script/Parser/SubWord.cs#YenAtSubWord.SetIsMacro#2` |
| LinkedListNode<Word>? (成员) .Next | 1 | bare/member | `Emuera/Runtime/Script/Parser/WordCollection.cs#WordCollection.Next#2` |
| FunctionLabelLine? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.CallEventFunction#1` |
| List<FunctionLabelLine>[]? (成员) | 1 | X![i] | `Emuera/Runtime/Script/Process.CalledFunction.cs#CalledFunction.ShiftNext#1` |
| LogicalLine? (成员) .Position | 1 | bare/member | `Emuera/Runtime/Script/Process.State.cs#ProcessState.Return#3` |
| CalledFunction (参数) .CurrentLabel | 1 | bare/member | `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#1` |
| ExpressionMediator (成员) .getCurrentLine | 1 | bare/member | `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#2` |
| ExpressionMediator (成员) .Position | 1 | bare/member | `Emuera/Runtime/Script/Process.State.cs#ProcessState.IntoFunction#3` |
| ? .AnalysisFiles | 1 | bare/member | `Emuera/Runtime/Script/Process.cs#Process.Initialize#1` |
| LogicalLine? (局部) .Position | 1 | bare/member | `Emuera/Runtime/Script/Process.cs#Process.checkInfiniteLoop#1` |
| LogicalLine? (参数) | 1 | bare/member | `Emuera/Runtime/Script/Process.cs#Process.handleException#1` |
| LogicalLine? (参数) .Position | 1 | bare/member | `Emuera/Runtime/Script/Process.cs#Process.handleException#2` |
| F(...)! wc.Current.ToString() | 1 | F(...)! | `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_DT_COLUMN_OPTIONS_ArgumentBuilder.CreateArgument#1` |
| F(...)! line.PopAssignmentDestStr() | 1 | F(...)! | `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_SET_ArgumentBuilder.CreateArgument#1` |
| InstructionLine (参数) .Method | 1 | bare/member | `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#METHOD_ArgumentBuilder.CreateArgument#1` |
| VariableTerm? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_SPLIT_ArgumentBuilder.CreateArgument#1` |
| InstructionLine (成员) .Instruction | 1 | bare/member | `Emuera/Runtime/Script/Statements/ArgumentParser.cs#ArgumentParser.SetArgumentTo#1` |
| (未解析) default | 1 | default! | `Emuera/Runtime/Script/Statements/CircularBuffer.cs#CircularBuffer<T>.Dequeue#1` |
| string[]? (局部) | 1 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#EnumNameMethod.GetIntValue#1` |
| XmlNode (参数) .Attributes | 1 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlAddNodeMethod.Insert#1` |
| XmlAttribute (局部) .OwnerElement | 1 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#XmlRemoveNodeMethod.Remove#1` |
| F(...)! v.ToString() | 1 | F(...)! | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#DataTableCellGetMethod.GetStrValue#1` |
| ? .Position | 1 | bare/member | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#UnicodeMethod.GetStrValue#1` |
| F(...)! GlobalStatic.LabelDictionary.GetNonEventLabel() | 1 | F(...)! | `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs#ExistFunctionMethod.GetIntValue#1` |
| ArgTypeList[]? (成员) | 1 | bare/member | `Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs#FunctionMethod.CheckArgumentTypeEx#1` |
| _ArgType[]? (局部) | 1 | X![i] | `Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs#FunctionMethod.CheckArgumentTypeEx#2` |
| (未解析) term | 1 | bare/member | `Emuera/Runtime/Script/Statements/Instraction.Child.cs#CALLSHARP_Instruction.DoInstruction#1` |
| PluginMethodParameter[] (局部) .strValue | 1 | bare/member | `Emuera/Runtime/Script/Statements/Instraction.Child.cs#CALLSHARP_Instruction.DoInstruction#2` |
| InstructionLine (局部) .LoopCounter | 1 | bare/member | `Emuera/Runtime/Script/Statements/Instraction.Child.cs#BREAK_Instruction.DoInstruction#1` |
| F(...)! base.ToString() | 1 | F(...)! | `Emuera/Runtime/Script/Statements/LogicalLine.cs#LogicalLine.ToString#1` |
| Argument? (成员) | 1 | bare/member | `Emuera/Runtime/Script/Statements/LogicalLine.cs#InstructionLine.BuiltArgument#1` |
| (未解析) Position | 1 | bare/member | `Emuera/Runtime/Script/Statements/LogicalLine.cs#FunctionLabelLine.CompareTo#2` |
| FunctionLabelLine? (成员) .Position | 1 | bare/member | `Emuera/Runtime/Script/Statements/LogicalLine.cs#FunctionLabelLine.CompareTo#3` |
| (未解析) ParentLabelLine | 1 | bare/member | `Emuera/Runtime/Script/Statements/LogicalLine.cs#GotoLabelLine.GetHashCode#1` |
| Dictionary<string, int>? (成员) | 1 | bare/member | `Emuera/Runtime/Script/Statements/Variable/VariableStrArgTerm.cs#VariableStrArgTerm.GetIntValue#1` |
| Array? (成员) | 1 | bare/member | `Emuera/Runtime/Script/Statements/Variable/VariableToken.cs#ReferenceToken.IsArrayRangeValid#1` |
| ? .ProcessPath | 1 | bare/member | `Emuera/Runtime/Utils/AssemblyData.cs#AssemblyData.AssemblyData#1` |
| ? .Version | 1 | bare/member | `Emuera/Runtime/Utils/AssemblyData.cs#AssemblyData.AssemblyData#2` |
| F(...)! Preload.GetFileLines() | 1 | F(...)! | `Emuera/Runtime/Utils/EraStreamReader.cs#EraStreamReader.OpenOnCache#1` |
| F(...)! xml.SelectNodes() | 1 | F(...)! | `Emuera/Runtime/Utils/EvilMask/Lang.cs#Lang.loadLangXML#1` |
| XmlNodeList (局部) | 1 | X![i] | `Emuera/Runtime/Utils/EvilMask/Lang.cs#Lang.loadLangXML#2` |
| MixedNum[]? (成员) | 1 | X![i] | `Emuera/Runtime/Utils/EvilMask/Utils.cs#Utils.MixedNum4ToInt4#1` |
| F(...)! Path.ChangeExtension() | 1 | F(...)! | `Emuera/Runtime/Utils/PathUtils/PurePath.cs#PurePath.ChangeExtension#1` |
| (未解析) (InstructionLine)logicalLine | 1 | bare/member | `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.ExecuteLine#1` |
| InstructionLine (局部) .Instruction | 1 | bare/member | `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.ExecuteLine#2` |
| ProcessState (成员) .CurrentLine | 1 | bare/member | `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetStackTrace#1` |
| ProcessState (成员) .Position | 1 | bare/member | `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetStackTrace#2` |
| FontFamily? (局部) | 1 | bare/member | `Emuera/UI/FontFactory.cs#FontFactory.CreateFont#1` |
| object? (局部) | 1 | bare/member | `Emuera/UI/Framework/Forms/ConfigDialog.cs#ConfigDialog.SetupLang#1` |
| ? .SelectedItem | 1 | bare/member | `Emuera/UI/Framework/Forms/ConfigDialog.cs#ConfigDialog.SaveConfig#1` |
| F(...)! value.ToString() | 1 | F(...)! | `Emuera/UI/Framework/Forms/DebugDialog.cs#DebugDialog.getValueString#2` |
| (未解析) (Screen.FromControl(this)??Screen.PrimaryScreen) | 1 | bare/member | `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.SetMaximizedBounds#1` |
| EmueraConsole? (成员) .SelectingButton | 1 | bare/member | `Emuera/UI/Framework/Forms/MainWindow.cs#MainWindow.mainPicBox_MouseDown#1` |
| IndexEntry? (成员) | 1 | bare/member | `Emuera/UI/Framework/Forms/RikaiDialog.cs#IndexEntry.CompareTo#1` |
| EmueraConsole? (参数) | 1 | bare/member | `Emuera/UI/Game/ConsoleButtonString.cs#ConsoleButtonString.ConsoleButtonString#1` |
| AConsoleDisplayNode[] (成员) .Text | 1 | bare/member | `Emuera/UI/Game/ConsoleButtonString.cs#ConsoleButtonString.DivideAt#1` |
| ASprite? (成员) | 1 | bare/member | `Emuera/UI/Game/ConsoleImagePart.cs#ConsoleImagePart.computeGeometry#1` |
| (未解析) AltText | 1 | bare/member | `Emuera/UI/Game/ConsoleImagePart.cs#ConsoleImagePart.DrawTo#1` |
| F(...)! BufferToSingleLine() | 1 | F(...)! | `Emuera/UI/Game/EmueraConsole.Print.cs#EmueraConsole.deleteLine#1` |
| ScriptPosition? (成员) | 1 | bare/member | `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OpenErrorFile#1` |
| AConsoleDisplayNode (局部) .Parent | 1 | bare/member | `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.OnPaint#1` |
| ConsoleButtonString? (局部) | 1 | bare/member | `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#3` |
| ConsoleStyledString (局部) .Text | 1 | bare/member | `Emuera/UI/Game/EmueraConsole.cs#EmueraConsole.MoveMouse#4` |
| ConsoleButtonString[] (局部) .Title | 1 | bare/member | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.DisplayLine2Html#1` |
| ? .color | 1 | bare/member | `Emuera/UI/Game/HtmlManager.cs#HtmlManager.tagAnalyze#15` |
| ? .Directory | 1 | bare/member | `Emuera/UI/Game/Image/AppContents.cs#AppContents.LoadContents#1` |
| F(...)! ImgUtils.LoadImage() | 1 | F(...)! | `Emuera/UI/Game/Image/ConstImage.cs#ConstImage.Load#1` |
| F(...)! buttonList[i].DivideAt() | 1 | F(...)! | `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.ButtonsToDisplayLines#1` |
| ConsoleStyledString? (局部) .Font | 1 | bare/member | `Emuera/UI/Game/PrintStringBuffer.cs#PrintStringBuffer.getDivideIndex#4` |
| byte[] (成员) | 1 | X![i] | `Emuera/UI/Game/Rikaichan.cs#Rikaichan.OnPaint#1` |

## 已失效站点(锚点区间之后被实施删除,2026-09-26 复核)

锚点区间(`1ed33b5c`…`03292664`)之后有 **12 条**站点的 `!` 已随实施提交消失,上面各表的行仍保留(它们是锚点上的事实),但核对当前代码时不再命中;余下 **332 条**仍可在 HEAD 上定位(结论分布 C 9 / B 8 / A 278 / D 37)。本节只列 ID,不进入上面的站点表与分组索引:

- `e9eaaf32`(issues/15,3 处 C):`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_IMG_ArgumentBuilder.CreateArgument#1`、`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#1`、`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs#SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#2`
- `9fe67fb6`(issues/19;其中 `IdentifierDictionary.GetVariableToken#2` 原挂 issues/25,7 处:C 4 / B 1 / A 2):`Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetVariableToken#2`(C)、`Emuera/Runtime/Script/Process.cs#Process.handleException#1`(C)、`Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.ExecuteLine#1`(C)、`Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.ExecuteLine#2`(C)、`Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetStackTrace#1`(B)、`Emuera/Runtime/Script/Process.cs#Process.handleException#2`(A)、`Emuera/Runtime/Utils/PluginSystem/PluginManager.cs#PluginManager.GetStackTrace#2`(A)
- `ab93db4f`(2 处 A):`Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.local#1`、`Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs#ExpressionParser.local#2`
