# Test Plan

第一轮先验证跨奖项稳定性：

1. 同一奖项多个届次，例如金马 44 / 45 / 46。
2. 一个完全不同的奖项页面。
3. 电影类别：海报 + Subject link。
4. 人物类别：人物头像 + Person link + 关联 Subject link。
5. 多人物类别。
6. 特殊/荣誉奖。
7. 同一届重复导入：SQLite 不翻倍。
8. 中途取消：不产生半份当前届数据。
9. 验证页/加载失败：不得覆盖旧 Edition。
10. JSON `doubanId` 与页面 href 一致。

重点：模型不丢关系、SubjectId 可 JOIN、重复导入稳定、不访问影片详情。
