using System.Text.Json;
using DoubanAwardsCollector.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DoubanAwardsCollector;

internal sealed class AwardCollectorService
{
    private readonly WebView2 _webView;
    private readonly string _parserScript;

    public AwardCollectorService(WebView2 webView)
    {
        _webView = webView;
        _parserScript = File.ReadAllText(AppPaths.ParserScriptPath);
    }

    public async Task<AwardEditionData> CollectAsync(
        AwardUrl target,
        CancellationToken cancellationToken)
    {
        if (_webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化。");
        }

        AppLogger.Write($"NAVIGATE {target.NormalizedUri}");
        await NavigateAsync(target.NormalizedUri, cancellationToken);
        await Task.Delay(250, cancellationToken);

        var rawResult = await _webView.CoreWebView2.ExecuteScriptAsync(_parserScript);
        var envelope = JsonSerializer.Deserialize<ParserEnvelope>(rawResult, JsonDefaults.Read);

        if (envelope is null)
        {
            throw new InvalidDataException("Parser 未返回可识别 JSON。");
        }

        if (!envelope.Ok || envelope.Document is null)
        {
            throw new InvalidDataException(
                string.IsNullOrWhiteSpace(envelope.Error)
                    ? "Parser 返回失败。"
                    : envelope.Error);
        }

        envelope.Document.Source.RequestedUrl = target.NormalizedUri.ToString();
        envelope.Document.Source.FinalUrl = _webView.Source?.ToString() ?? string.Empty;

        if (!string.Equals(
                envelope.Document.Event.Slug,
                target.Slug,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Parser slug 不一致：期望 {target.Slug}，实际 {envelope.Document.Event.Slug}。");
        }

        if (!string.Equals(
                envelope.Document.Edition.Key,
                target.EditionKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Parser edition 不一致：期望 {target.EditionKey}，实际 {envelope.Document.Edition.Key}。");
        }

        if (envelope.Document.Categories.Count == 0 ||
            envelope.Document.Categories.All(category => category.Entries.Count == 0))
        {
            throw new InvalidDataException("没有解析到完整奖项条目，不写入数据库。");
        }

        return envelope.Document;
    }

    private async Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            _webView.NavigationCompleted -= Handler;
            tcs.TrySetResult(args);
        }

        _webView.NavigationCompleted += Handler;

        using var registration = cancellationToken.Register(() =>
        {
            _webView.NavigationCompleted -= Handler;
            tcs.TrySetCanceled(cancellationToken);
        });

        _webView.CoreWebView2!.Navigate(uri.ToString());
        var completed = await tcs.Task;

        if (!completed.IsSuccess)
        {
            throw new InvalidOperationException(
                $"WebView2 导航失败：{completed.WebErrorStatus}");
        }
    }
}
