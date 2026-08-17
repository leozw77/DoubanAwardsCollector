using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DoubanAwardsCollector;

public sealed class MainForm : Form
{
    private readonly TextBox _urlInput = new();
    private readonly Button _importButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openDataButton = new();
    private readonly Label _statusLabel = new();
    private readonly ListBox _logList = new();
    private readonly WebView2 _webView = new();

    private AwardCollectorService? _collector;
    private AwardRepository? _repository;
    private CancellationTokenSource? _importCancellation;

    public MainForm()
    {
        Text = "Douban Awards Collector 0.1.1";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1320;
        Height = 860;
        MinimumSize = new Size(980, 680);

        BuildUi();

        Shown += async (_, _) => await InitializeAsync();
        FormClosing += (_, _) => _importCancellation?.Cancel();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(18, 18, 20)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 7,
            Padding = new Padding(14),
            BackColor = Color.FromArgb(24, 24, 26)
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Text = "豆瓣奖项采集器",
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        _urlInput.Dock = DockStyle.Fill;
        _urlInput.Multiline = true;
        _urlInput.ScrollBars = ScrollBars.Vertical;
        _urlInput.BackColor = Color.FromArgb(34, 34, 36);
        _urlInput.ForeColor = Color.White;
        _urlInput.BorderStyle = BorderStyle.FixedSingle;
        _urlInput.Font = new Font("Consolas", 10);
        _urlInput.Text =
            "https://movie.douban.com/awards/goldenhorse/44/" +
            Environment.NewLine;
        _urlInput.PlaceholderText = "一行一个豆瓣 Awards 链接";

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 10, 0, 4)
        };

        ConfigureButton(_importButton, "开始导入");
        ConfigureButton(_cancelButton, "取消");
        ConfigureButton(_openDataButton, "打开数据目录");

        _cancelButton.Enabled = false;

        _importButton.Click += async (_, _) => await ImportAsync();
        _cancelButton.Click += (_, _) => _importCancellation?.Cancel();
        _openDataButton.Click += (_, _) => OpenDataFolder();

        buttonRow.Controls.AddRange([_importButton, _cancelButton, _openDataButton]);

        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(345, 0),
            Text =
                "支持 /awards/{slug}/{edition}/ 任意子页面。" +
                "程序统一进入 nominees?k=a，只读当前 DOM，不访问影片详情。",
            ForeColor = Color.FromArgb(170, 170, 175),
            Font = new Font("Microsoft YaHei UI", 9),
            Margin = new Padding(0, 6, 0, 10)
        };

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "正在初始化 WebView2…";
        _statusLabel.ForeColor = Color.FromArgb(76, 217, 122);
        _statusLabel.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
        _statusLabel.Margin = new Padding(0, 0, 0, 8);

        _logList.Dock = DockStyle.Fill;
        _logList.BackColor = Color.FromArgb(14, 14, 16);
        _logList.ForeColor = Color.FromArgb(215, 215, 220);
        _logList.BorderStyle = BorderStyle.FixedSingle;
        _logList.Font = new Font("Consolas", 9);

        var footer = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(345, 0),
            Text = "本地：%LOCALAPPDATA%\\DoubanAwardsCollector",
            ForeColor = Color.FromArgb(120, 120, 125),
            Font = new Font("Microsoft YaHei UI", 8),
            Margin = new Padding(0, 8, 0, 0)
        };

        left.Controls.Add(title, 0, 0);
        left.Controls.Add(_urlInput, 0, 1);
        left.Controls.Add(buttonRow, 0, 2);
        left.Controls.Add(hint, 0, 3);
        left.Controls.Add(_statusLabel, 0, 4);
        left.Controls.Add(_logList, 0, 5);
        left.Controls.Add(footer, 0, 6);

        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.FromArgb(12, 10, 9);

        root.Controls.Add(left, 0, 0);
        root.Controls.Add(_webView, 1, 0);
        Controls.Add(root);
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.AutoSize = true;
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 74);
        button.BackColor = Color.FromArgb(44, 44, 46);
        button.ForeColor = Color.White;
        button.Padding = new Padding(8, 3, 8, 3);
        button.Margin = new Padding(0, 0, 8, 6);
    }

    private async Task InitializeAsync()
    {
        try
        {
            AppPaths.EnsureCreated();

            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: AppPaths.WebView2UserDataFolder);

            await _webView.EnsureCoreWebView2Async(environment);

            _repository = new AwardRepository(AppPaths.DatabasePath);
            await _repository.InitializeAsync();

            _collector = new AwardCollectorService(_webView);

            _statusLabel.Text = "就绪";
            Log("INIT OK");
            Log($"DB {AppPaths.DatabasePath}");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "初始化失败";
            Log($"INIT ERROR {ex.Message}");
            AppLogger.Write($"INIT ERROR {ex}");
            MessageBox.Show(
                this,
                ex.ToString(),
                "初始化失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task ImportAsync()
    {
        if (_collector is null || _repository is null)
        {
            MessageBox.Show(this, "WebView2 / SQLite 尚未初始化。");
            return;
        }

        var lines = _urlInput.Lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (lines.Length == 0)
        {
            MessageBox.Show(this, "请至少输入一个 Awards 链接。");
            return;
        }

        var targets = new List<AwardUrl>();
        foreach (var line in lines)
        {
            if (!AwardUrl.TryNormalize(line, out var target, out var error) || target is null)
            {
                Log($"SKIP {line} :: {error}");
                continue;
            }

            targets.Add(target);
        }

        if (targets.Count == 0)
        {
            MessageBox.Show(this, "没有可导入的有效链接。");
            return;
        }

        _importCancellation?.Dispose();
        _importCancellation = new CancellationTokenSource();
        var token = _importCancellation.Token;

        SetBusy(true);

        var success = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < targets.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var target = targets[index];

                _statusLabel.Text =
                    $"[{index + 1}/{targets.Count}] {target.Slug} / {target.EditionKey}";
                Log($"OPEN {target.NormalizedUri}");

                try
                {
                    var document = await _collector.CollectAsync(target, token);
                    var jsonPath = await AwardJsonStore.SaveAsync(document, token);
                    var summary = await _repository.ReplaceEditionAsync(document, token);

                    success++;
                    Log(
                        $"OK {document.Event.Name} {document.Edition.Key} :: " +
                        $"{summary.CategoryCount}类 / {summary.EntryCount}条 / " +
                        $"{summary.UniqueSubjectCount}影片 / {summary.UniquePersonCount}人物 / " +
                        $"{summary.WinnerCount}获奖");
                    Log($"JSON {jsonPath}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"ERROR {target.Slug}/{target.EditionKey} :: {ex.Message}");
                    AppLogger.Write(
                        $"IMPORT ERROR {target.NormalizedUri}{Environment.NewLine}{ex}");
                }

                if (index + 1 < targets.Count)
                {
                    await Task.Delay(1000, token);
                }
            }

            _statusLabel.Text = $"完成：成功 {success}，失败 {failed}";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = $"已取消：成功 {success}，失败 {failed}";
            Log("CANCELLED");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _importButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _urlInput.ReadOnly = busy;
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        _logList.Items.Add(line);
        _logList.TopIndex = Math.Max(0, _logList.Items.Count - 1);
        AppLogger.Write(message);
    }

    private static void OpenDataFolder()
    {
        AppPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{AppPaths.Root}\"",
            UseShellExecute = true
        });
    }
}
