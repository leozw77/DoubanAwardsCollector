# 上传到 GitHub

目标仓库：

```text
leozw77/DoubanAwardsCollector
```

把本 ZIP **解压后根目录里的全部文件** 上传到仓库根目录，不要把外层 `DoubanAwardsCollector_Source_v0.1.0` 文件夹再套一层。

上传后应直接看到：

```text
.github/
docs/
schemas/
src/
DoubanAwardsCollector.sln
README.md
build.cmd
```

提交到 `main` 后，GitHub Actions 的 `build-windows` 会自动运行。

成功后在：

```text
Actions → build-windows → 对应运行 → Artifacts
```

可以看到：

```text
DoubanAwardsCollector-win-x64
```

其中包含 `DoubanAwardsCollector-win-x64.zip`。
