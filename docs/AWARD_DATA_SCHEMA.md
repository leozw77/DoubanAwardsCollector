# AwardEditionData v1

`AwardEditionData v1` 是本项目最重要的兼容边界。

## 顶层字段

```text
schemaVersion
parserVersion
collectedAtUtc
source
event
edition
relatedEditions[]
categories[]
```

## Source

```text
provider      固定 douban
requestedUrl  用户输入规范化后的地址
finalUrl      WebView2 实际最终地址
```

## Event

```text
slug          URL 稳定标识，如 goldenhorse
name          页面推导的显示名
sourceTitle   豆瓣 H1 原文
```

## Edition

```text
key           URL 届次标识，如 44
year          可空
title         页面 H1
```

## RelatedEdition

只记录当前页面已经出现的同一 Event 历届链接，不自动访问：

```text
editionKey
year          可空
label
url
```

## Category

```text
order
groupName
name
entries[]
```

## Entry

```text
order
result        winner | nominee | unknown
subjects[]    允许 0..N
people[]      允许 0..N
image         可空
rawText       必存
```

Subjects / People 使用数组是为了保留人物奖、多人编剧奖、特殊奖等关系，不为第一版 UI 方便而丢数据。

## Personal State

个人状态禁止写进 AwardEditionData：

```text
watched
status
myRating
comment
```

未来通过 `DoubanSubjectId` JOIN。

## Image

`image.kind` 为 `subject | person | unknown`，并带 `doubanId`。这样人物奖的头像不会误写成关联影片的海报。
