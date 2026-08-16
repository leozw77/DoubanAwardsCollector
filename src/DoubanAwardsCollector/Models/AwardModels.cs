namespace DoubanAwardsCollector.Models;

public sealed class ParserEnvelope
{
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;
    public AwardEditionData? Document { get; set; }
}

public sealed class AwardEditionData
{
    public int SchemaVersion { get; set; } = 1;
    public string ParserVersion { get; set; } = "1.0.0";
    public DateTimeOffset CollectedAtUtc { get; set; }
    public SourceInfo Source { get; set; } = new();
    public AwardEventData Event { get; set; } = new();
    public AwardEditionInfo Edition { get; set; } = new();
    public List<RelatedEditionData> RelatedEditions { get; set; } = [];
    public List<AwardCategoryData> Categories { get; set; } = [];
}

public sealed class SourceInfo
{
    public string Provider { get; set; } = "douban";
    public string RequestedUrl { get; set; } = string.Empty;
    public string FinalUrl { get; set; } = string.Empty;
}

public sealed class AwardEventData
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
}

public sealed class AwardEditionInfo
{
    public string Key { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Title { get; set; } = string.Empty;
}

public sealed class RelatedEditionData
{
    public string EditionKey { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class AwardCategoryData
{
    public int Order { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<AwardEntryData> Entries { get; set; } = [];
}

public sealed class AwardEntryData
{
    public int Order { get; set; }
    public string Result { get; set; } = "unknown";
    public List<SubjectRefData> Subjects { get; set; } = [];
    public List<PersonRefData> People { get; set; } = [];
    public ImageRefData? Image { get; set; }
    public string RawText { get; set; } = string.Empty;
}

public sealed class SubjectRefData
{
    public string Provider { get; set; } = "douban";
    public string DoubanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class PersonRefData
{
    public string Provider { get; set; } = "douban";
    public string DoubanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class ImageRefData
{
    public string Url { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public string Kind { get; set; } = "unknown";
    public string DoubanId { get; set; } = string.Empty;
}

public sealed record ImportSummary(
    int CategoryCount,
    int EntryCount,
    int UniqueSubjectCount,
    int UniquePersonCount,
    int WinnerCount);
