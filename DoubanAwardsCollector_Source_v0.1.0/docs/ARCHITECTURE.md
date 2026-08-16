# Architecture

## 核心约束

采集器不是“爬电影详情的爬虫”，而是一个 **Awards 页面 DOM → 稳定数据模型** 的转换器。

```text
用户输入 Awards URL
        ↓
AwardUrlNormalizer
        ↓
WebView2 正常导航到 /nominees?k=a
        ↓
Assets/award-parser.js
        ↓
AwardEditionData v1
        ├── JSON Snapshot
        └── SQLite AwardRepository
```

## Parser 与存储必须解耦

禁止：

```text
DOM → 直接拼 SQL
```

必须：

```text
DOM
 ↓
AwardEditionData v1
 ↓
 ├─ SQLite
 ├─ JSON
 ├─ 调试
 └─ 未来 Douban Plus UI / Repository
```

## 数据职责

### Awards 数据

负责“电影与奖项的关系”：

- AwardEvent
- AwardEdition
- AwardCategory
- AwardEntry
- Subject / Person 引用
- Winner / Nominee

### User 数据

负责“我与电影的关系”：

- wish
- do
- collect
- myRating
- markedDate
- comment

两者唯一需要共享的关键标识是 `DoubanSubjectId`。

## 重新导入

每一届视为一个完整快照。只有 Parser 完整成功后才写库。

写入事务：

1. Upsert Event
2. Upsert Edition
3. 删除该 Edition 旧 Categories/Entries/relations
4. 写入新 Categories/Entries
5. Upsert Subjects/People
6. Commit

任何一步失败则 Rollback，旧版数据继续保留。
