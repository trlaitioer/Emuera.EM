# 存档读取:畸形文本序列检出加固

Status: needs-triage

## 任务

net9 起 `BinaryReader.ReadString()` 对畸形字节序列不再抛异常,改为静默返回 U+FFFD。存档读取器 `EraBinaryDataReader`(`Runtime/Utils/EraBinaryDataReader.cs`,派生类 `EraBinaryDataReader1808.ReadString` 直接透传,`BinaryReader` 以 `Encoding.Unicode` 构造)因此对损坏存档中文本段(变量名、字符串变量、DICT/DataTable 键值)的检出变弱:异常路径退化为静默替换字符。现有防线是 ReadInt 数量前缀校验(越界抛 `FileEE`),主体仍有效,本票定位为加固而非缺陷修复。

完成标准:构建通过 + 损坏存档夹具读取报 `FileEE`、正常存档读写回归。

## 方案

`BinaryReader` 构造改用带 `DecoderFallback.ExceptionFallback` 的 UTF-16 编码(`Encoding.GetEncoding(Encoding.Unicode.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)`),恢复"损坏即异常"语义,上层统一转 `FileEE`。

## 曾考虑的替代方案

1. 仅校验文本中是否含 U+FFFD:检测粒度粗且可能误伤合法字符,不采用

## 影响

损坏存档的文本段损坏由静默替换字符恢复为抛 `FileEE`;正常存档读写行为不变。
