# SQLite Database

数据库默认：

```text
%LOCALAPPDATA%\DoubanAwardsCollector\data\awards.db
```

主要表：

```text
award_events
award_editions
award_categories
award_entries
subjects
people
award_entry_subjects
award_entry_people
related_editions
import_runs
user_subject_states
```

## user_subject_states

该表是未来兼容位，v0.1.0 不写：

```text
douban_subject_id PRIMARY KEY
status            wish | do | collect | unmarked
my_rating         1..5 nullable
marked_date       nullable
comment           nullable
synced_at         nullable
```

“本地没有状态”数据层理解为 `unmarked`；UI 是否显示“未看”由产品层决定。
