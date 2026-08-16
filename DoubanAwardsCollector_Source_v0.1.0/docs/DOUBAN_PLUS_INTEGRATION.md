# Douban Plus Integration

未来 Douban Plus 打开 Awards 页面时，每个影片已经有 `DoubanSubjectId`，因此无需进入详情页即可查询个人状态。

```sql
SELECT douban_subject_id, status, my_rating, marked_date
FROM user_subject_states
WHERE douban_subject_id IN (...);
```

UI 层按 `DoubanSubjectId` 合并：

```text
色，戒
🏆 最佳剧情片
✓ 已看 ★★★★
```

奖项关系必须保持结构化：

```text
Event → Edition → Category → Entry → Subject
```

不要把 `金马,最佳剧情片,获奖` 这种字符串标签作为唯一数据来源。
