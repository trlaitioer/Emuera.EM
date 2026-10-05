# 存档读取:畸形文本序列检出加固

id: dotnet-modernization-02
Status: implemented

## 任务

net9 起 `BinaryReader.ReadString()` 对畸形字节序列不再抛异常,改为静默返回 U+FFFD。存档读取器 `EraBinaryDataReader`(`Runtime/Utils/EraBinaryDataReader.cs`,派生类 `EraBinaryDataReader1808.ReadString` 直接透传,`BinaryReader` 以 `Encoding.Unicode` 构造)因此对损坏存档中文本段(变量名、字符串变量、DICT/DataTable 键值)的检出变弱:异常路径退化为静默替换字符。现有防线是 ReadInt 数量前缀校验(越界抛 `FileEE`),主体仍有效,本票定位为加固而非缺陷修复。

完成标准:构建通过 + 损坏存档夹具读取报 `FileEE`、正常存档读写回归。

## 方案

`BinaryReader` 构造改用带 `DecoderFallback.ExceptionFallback` 的 UTF-16 编码(`Encoding.GetEncoding(Encoding.Unicode.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)`),恢复"损坏即异常"语义,上层统一转 `FileEE`。

2026-10-05 落地:编码取自既有的 `EncodingHandler.GetEncoding(Encoding.Unicode.CodePage)`(同形 helper),3 个构造点(`CreateReader` 的主 reader 与压缩/非压缩两条重造路径)统一改用内嵌的 `SaveTextReader`;它把**全部文本解码入口**的 `DecoderFallbackException` 转成 `FileEE(trerror.CorruptedSaveData.Text + 解码器信息)`,不让裸的解码异常以「内部错误」形式冒到解释器循环:`ReadString`(存档文本段)与 `PeekChar`/`ReadChar`(字符级 EOF 检查,`EraBinaryDataReader.EOF` 走的正是 `PeekChar`;该路径的调用方 `LoadFrom` 没有 catch,不包就会上浮成内部错误)。派生类 `EraBinaryDataReader1808.ReadString` 无需改动(编码随 reader 走)。

用例:`Emuera.Tests/Runtime/Utils/EraBinaryDataReaderTests.cs` 5 条——畸形序列(未配对高代理后随普通字符)经 `ReadString` 报 `FileEE`、同一处畸形经 `EOF()`/`PeekChar` 也报 `FileEE`、反空跑对照(同一字节在非严格 UTF-16 下只产出替换字符)、正常文本含增补平面代理对读回原值、**写→读往返**(生产 `EraBinaryDataWriter.WriteHeader` + `WriteString` 落盘后由读取器读回同一字符串)。注意 `CreateReader` 自身仍把头部级损坏吞成 `null`(调用方回退到文本存档路径),本票只覆盖文本段解码;`Config.ZipSaveData` 的压缩档分支不在往返用例范围内;流末尾的孤立高代理不算畸形(解码器挂起等待低位代理,`PeekChar` 按「无字符」返回),故未列入用例。

## 曾考虑的替代方案

1. 仅校验文本中是否含 U+FFFD:检测粒度粗且可能误伤合法字符,不采用

## 影响

损坏存档的文本段损坏由静默替换字符恢复为抛 `FileEE`;正常存档读写行为不变。`LOADDATA` 路径本就先经 `VariableEvaluator.CheckData` 预检,故那里的可见结果是原来的 `CodeEE(LoadCorruptedData)`,只是不再有「静默替换后静默通过」这一档。
