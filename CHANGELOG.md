# Changelog

## 0.1.0 - Initial collector prototype

- 建立 `AwardEditionData v1` 稳定中间模型。
- 建立 JSON Schema。
- 建立 SQLite 奖项关系数据库。
- 预留 `user_subject_states`，用于未来 Douban Plus 本地个人状态 JOIN。
- 新增 WinForms + WebView2 多链接顺序导入。
- 输入任意 `/awards/{slug}/{edition}/...`，统一进入 `/nominees?k=a`。
- Parser 只读取当前 Awards DOM，不访问影片/人物详情页。
- 一个条目允许关联多个影片和多个人物，避免人物奖、编剧奖等信息丢失。
- 保存 `rawText`，解析不完整时仍保留豆瓣原始信息。
- 同一届重新导入采用事务整体替换，避免重复数据。
- 自动保存每届 JSON 快照。
- GitHub Actions Windows Runner 构建并上传 self-contained ZIP。
