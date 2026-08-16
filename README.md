# DoubanAwardsCollector

独立的豆瓣奖项数据采集器，用于给 Douban Plus / 观影助手建立可长期复用的本地奖项索引。

## v0.1.0 目标

输入一个或多个豆瓣奖项链接：

```text
https://movie.douban.com/awards/goldenhorse/44/
```

程序会：

1. 规范化为该届的完整图文名单 `.../nominees?k=a`
2. 使用 WebView2 正常打开豆瓣页面
3. **只读取当前页面 DOM**
4. 提取奖项、届次、类别、获奖/提名、影片、人物、图片、原始文本
5. 输出稳定的 `AwardEditionData v1` JSON
6. 用事务写入本地 SQLite
7. 不访问影片详情页，不依赖 TMDb / IMDb / PtGen

## 为什么把奖项事实与“我看没看”分开

奖项库只保存客观事实：

```text
影片 1828115
→ 第44届金马
→ 最佳剧情片
→ winner
```

未来 Douban Plus 的个人本地库保存：

```text
1828115
→ collect
→ 4星
→ 观看日期
```

两者通过 `DoubanSubjectId` JOIN。这样打开电影节页面时，不进入电影详情页也能直接显示“已看 / 想看 / 未标记 / 我的评分”。

数据库已预留 `user_subject_states` 表，但 v0.1.0 不写个人状态。

## 技术栈

- .NET 8
- Windows Forms
- WebView2
- Microsoft.Data.Sqlite
- JSON Schema v1
- GitHub Actions `windows-latest`

## 本地数据位置

```text
%LOCALAPPDATA%\DoubanAwardsCollector\
├─ data\awards.db
├─ json\
├─ logs\
└─ webview2\
```

## 构建

```bat
build.cmd
```

或：

```powershell
dotnet restore .\src\DoubanAwardsCollector\DoubanAwardsCollector.csproj
dotnet build   .\src\DoubanAwardsCollector\DoubanAwardsCollector.csproj -c Release
```

GitHub Actions 会额外发布 `win-x64` self-contained ZIP。
