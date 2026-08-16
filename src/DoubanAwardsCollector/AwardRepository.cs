using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DoubanAwardsCollector.Models;
using Microsoft.Data.Sqlite;

namespace DoubanAwardsCollector;

internal sealed class AwardRepository
{
    private readonly string _connectionString;

    public AwardRepository(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);

        const string schema = """
CREATE TABLE IF NOT EXISTS award_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    provider TEXT NOT NULL,
    slug TEXT NOT NULL,
    name TEXT NOT NULL,
    source_title TEXT NOT NULL,
    first_seen_at TEXT NOT NULL,
    last_seen_at TEXT NOT NULL,
    UNIQUE(provider, slug)
);

CREATE TABLE IF NOT EXISTS award_editions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id INTEGER NOT NULL,
    edition_key TEXT NOT NULL,
    year INTEGER NULL,
    title TEXT NOT NULL,
    source_url TEXT NOT NULL,
    imported_at TEXT NOT NULL,
    parser_version TEXT NOT NULL,
    schema_version INTEGER NOT NULL,
    content_hash TEXT NOT NULL,
    FOREIGN KEY(event_id) REFERENCES award_events(id) ON DELETE CASCADE,
    UNIQUE(event_id, edition_key)
);

CREATE TABLE IF NOT EXISTS award_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    edition_id INTEGER NOT NULL,
    group_name TEXT NOT NULL,
    name TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    FOREIGN KEY(edition_id) REFERENCES award_editions(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS award_entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL,
    result TEXT NOT NULL,
    image_url TEXT NULL,
    image_alt TEXT NULL,
    raw_text TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    FOREIGN KEY(category_id) REFERENCES award_categories(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS subjects (
    douban_subject_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    url TEXT NOT NULL,
    image_url TEXT NULL,
    first_seen_at TEXT NOT NULL,
    last_seen_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS people (
    douban_person_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    url TEXT NOT NULL,
    image_url TEXT NULL,
    first_seen_at TEXT NOT NULL,
    last_seen_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS award_entry_subjects (
    entry_id INTEGER NOT NULL,
    douban_subject_id TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    PRIMARY KEY(entry_id, douban_subject_id),
    FOREIGN KEY(entry_id) REFERENCES award_entries(id) ON DELETE CASCADE,
    FOREIGN KEY(douban_subject_id) REFERENCES subjects(douban_subject_id)
);

CREATE TABLE IF NOT EXISTS award_entry_people (
    entry_id INTEGER NOT NULL,
    douban_person_id TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    PRIMARY KEY(entry_id, douban_person_id),
    FOREIGN KEY(entry_id) REFERENCES award_entries(id) ON DELETE CASCADE,
    FOREIGN KEY(douban_person_id) REFERENCES people(douban_person_id)
);

CREATE TABLE IF NOT EXISTS related_editions (
    edition_id INTEGER NOT NULL,
    related_edition_key TEXT NOT NULL,
    year INTEGER NULL,
    label TEXT NOT NULL,
    url TEXT NOT NULL,
    PRIMARY KEY(edition_id, related_edition_key),
    FOREIGN KEY(edition_id) REFERENCES award_editions(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS import_runs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    requested_url TEXT NOT NULL,
    final_url TEXT NOT NULL,
    event_slug TEXT NOT NULL,
    edition_key TEXT NOT NULL,
    status TEXT NOT NULL,
    message TEXT NOT NULL,
    started_at TEXT NOT NULL,
    completed_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS user_subject_states (
    douban_subject_id TEXT PRIMARY KEY,
    status TEXT NOT NULL CHECK(status IN ('wish','do','collect','unmarked')),
    my_rating INTEGER NULL CHECK(my_rating IS NULL OR (my_rating BETWEEN 1 AND 5)),
    marked_date TEXT NULL,
    comment TEXT NULL,
    synced_at TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_award_editions_event ON award_editions(event_id, edition_key);
CREATE INDEX IF NOT EXISTS idx_award_entries_category_result ON award_entries(category_id, result);
CREATE INDEX IF NOT EXISTS idx_award_entry_subjects_subject ON award_entry_subjects(douban_subject_id);
CREATE INDEX IF NOT EXISTS idx_award_entry_people_person ON award_entry_people(douban_person_id);
""";

        await ExecuteNonQueryAsync(connection, schema, cancellationToken);
    }

    public async Task<ImportSummary> ReplaceEditionAsync(
        AwardEditionData document,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var startedAt = DateTimeOffset.UtcNow.ToString("O");
            var eventId = await UpsertEventAsync(connection, transaction, document, startedAt, cancellationToken);
            var editionId = await UpsertEditionAsync(connection, transaction, eventId, document, startedAt, cancellationToken);

            await DeleteEditionChildrenAsync(connection, transaction, editionId, cancellationToken);

            var uniqueSubjects = new HashSet<string>(StringComparer.Ordinal);
            var uniquePeople = new HashSet<string>(StringComparer.Ordinal);
            var entryCount = 0;
            var winnerCount = 0;

            foreach (var category in document.Categories.OrderBy(item => item.Order))
            {
                var categoryId = await InsertCategoryAsync(
                    connection, transaction, editionId, category, cancellationToken);

                foreach (var entry in category.Entries.OrderBy(item => item.Order))
                {
                    entryCount++;
                    if (string.Equals(entry.Result, "winner", StringComparison.OrdinalIgnoreCase))
                    {
                        winnerCount++;
                    }

                    var entryId = await InsertEntryAsync(
                        connection, transaction, categoryId, entry, cancellationToken);

                    var subjectOrder = 0;
                    foreach (var subject in entry.Subjects)
                    {
                        if (string.IsNullOrWhiteSpace(subject.DoubanId))
                        {
                            continue;
                        }

                        uniqueSubjects.Add(subject.DoubanId);
                        var subjectImage = entry.Image is not null &&
                            string.Equals(entry.Image.Kind, "subject", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(entry.Image.DoubanId, subject.DoubanId, StringComparison.Ordinal)
                                ? entry.Image.Url
                                : null;

                        await UpsertSubjectAsync(
                            connection, transaction, subject, subjectImage, startedAt, cancellationToken);
                        await LinkSubjectAsync(
                            connection, transaction, entryId, subject.DoubanId, subjectOrder++, cancellationToken);
                    }

                    var personOrder = 0;
                    foreach (var person in entry.People)
                    {
                        if (string.IsNullOrWhiteSpace(person.DoubanId))
                        {
                            continue;
                        }

                        uniquePeople.Add(person.DoubanId);
                        var personImage = entry.Image is not null &&
                            string.Equals(entry.Image.Kind, "person", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(entry.Image.DoubanId, person.DoubanId, StringComparison.Ordinal)
                                ? entry.Image.Url
                                : null;

                        await UpsertPersonAsync(
                            connection, transaction, person, personImage, startedAt, cancellationToken);
                        await LinkPersonAsync(
                            connection, transaction, entryId, person.DoubanId, personOrder++, cancellationToken);
                    }
                }
            }

            foreach (var related in document.RelatedEditions)
            {
                await InsertRelatedEditionAsync(
                    connection, transaction, editionId, related, cancellationToken);
            }

            await InsertImportRunAsync(
                connection,
                transaction,
                document,
                "success",
                $"categories={document.Categories.Count}; entries={entryCount}",
                startedAt,
                DateTimeOffset.UtcNow.ToString("O"),
                cancellationToken);

            transaction.Commit();

            return new ImportSummary(
                document.Categories.Count,
                entryCount,
                uniqueSubjects.Count,
                uniquePeople.Count,
                winnerCount);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<long> UpsertEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AwardEditionData document,
        string now,
        CancellationToken cancellationToken)
    {
        const string upsert = """
INSERT INTO award_events(provider, slug, name, source_title, first_seen_at, last_seen_at)
VALUES('douban', $slug, $name, $sourceTitle, $now, $now)
ON CONFLICT(provider, slug) DO UPDATE SET
    name = excluded.name,
    source_title = excluded.source_title,
    last_seen_at = excluded.last_seen_at;
""";
        await ExecuteNonQueryAsync(
            connection, transaction, upsert, cancellationToken,
            ("$slug", document.Event.Slug),
            ("$name", document.Event.Name),
            ("$sourceTitle", document.Event.SourceTitle),
            ("$now", now));

        return await ExecuteScalarLongAsync(
            connection,
            transaction,
            "SELECT id FROM award_events WHERE provider='douban' AND slug=$slug;",
            cancellationToken,
            ("$slug", document.Event.Slug));
    }

    private static async Task<long> UpsertEditionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long eventId,
        AwardEditionData document,
        string now,
        CancellationToken cancellationToken)
    {
        var contentHash = ComputeContentHash(document);

        const string upsert = """
INSERT INTO award_editions(
    event_id, edition_key, year, title, source_url,
    imported_at, parser_version, schema_version, content_hash)
VALUES(
    $eventId, $editionKey, $year, $title, $sourceUrl,
    $importedAt, $parserVersion, $schemaVersion, $contentHash)
ON CONFLICT(event_id, edition_key) DO UPDATE SET
    year = excluded.year,
    title = excluded.title,
    source_url = excluded.source_url,
    imported_at = excluded.imported_at,
    parser_version = excluded.parser_version,
    schema_version = excluded.schema_version,
    content_hash = excluded.content_hash;
""";

        await ExecuteNonQueryAsync(
            connection, transaction, upsert, cancellationToken,
            ("$eventId", eventId),
            ("$editionKey", document.Edition.Key),
            ("$year", document.Edition.Year),
            ("$title", document.Edition.Title),
            ("$sourceUrl", document.Source.FinalUrl),
            ("$importedAt", now),
            ("$parserVersion", document.ParserVersion),
            ("$schemaVersion", document.SchemaVersion),
            ("$contentHash", contentHash));

        return await ExecuteScalarLongAsync(
            connection,
            transaction,
            "SELECT id FROM award_editions WHERE event_id=$eventId AND edition_key=$editionKey;",
            cancellationToken,
            ("$eventId", eventId),
            ("$editionKey", document.Edition.Key));
    }

    private static async Task DeleteEditionChildrenAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long editionId,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection, transaction,
            "DELETE FROM related_editions WHERE edition_id=$editionId;",
            cancellationToken,
            ("$editionId", editionId));

        await ExecuteNonQueryAsync(
            connection, transaction,
            "DELETE FROM award_categories WHERE edition_id=$editionId;",
            cancellationToken,
            ("$editionId", editionId));
    }

    private static Task<long> InsertCategoryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long editionId,
        AwardCategoryData category,
        CancellationToken cancellationToken)
        => ExecuteScalarLongAsync(
            connection, transaction,
            """
INSERT INTO award_categories(edition_id, group_name, name, sort_order)
VALUES($editionId, $groupName, $name, $sortOrder);
SELECT last_insert_rowid();
""",
            cancellationToken,
            ("$editionId", editionId),
            ("$groupName", category.GroupName),
            ("$name", category.Name),
            ("$sortOrder", category.Order));

    private static Task<long> InsertEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long categoryId,
        AwardEntryData entry,
        CancellationToken cancellationToken)
        => ExecuteScalarLongAsync(
            connection, transaction,
            """
INSERT INTO award_entries(
    category_id, result, image_url, image_alt, raw_text, sort_order)
VALUES(
    $categoryId, $result, $imageUrl, $imageAlt, $rawText, $sortOrder);
SELECT last_insert_rowid();
""",
            cancellationToken,
            ("$categoryId", categoryId),
            ("$result", entry.Result),
            ("$imageUrl", entry.Image?.Url),
            ("$imageAlt", entry.Image?.Alt),
            ("$rawText", entry.RawText),
            ("$sortOrder", entry.Order));

    private static Task UpsertSubjectAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SubjectRefData subject,
        string? imageUrl,
        string now,
        CancellationToken cancellationToken)
        => ExecuteNonQueryAsync(
            connection, transaction,
            """
INSERT INTO subjects(
    douban_subject_id, name, url, image_url, first_seen_at, last_seen_at)
VALUES($id, $name, $url, $imageUrl, $now, $now)
ON CONFLICT(douban_subject_id) DO UPDATE SET
    name = CASE WHEN excluded.name <> '' THEN excluded.name ELSE subjects.name END,
    url = CASE WHEN excluded.url <> '' THEN excluded.url ELSE subjects.url END,
    image_url = COALESCE(NULLIF(excluded.image_url, ''), subjects.image_url),
    last_seen_at = excluded.last_seen_at;
""",
            cancellationToken,
            ("$id", subject.DoubanId),
            ("$name", subject.Name),
            ("$url", subject.Url),
            ("$imageUrl", imageUrl),
            ("$now", now));

    private static Task UpsertPersonAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PersonRefData person,
        string? imageUrl,
        string now,
        CancellationToken cancellationToken)
        => ExecuteNonQueryAsync(
            connection, transaction,
            """
INSERT INTO people(
    douban_person_id, name, url, image_url, first_seen_at, last_seen_at)
VALUES($id, $name, $url, $imageUrl, $now, $now)
ON CONFLICT(douban_person_id) DO UPDATE SET
    name = CASE WHEN excluded.name <> '' THEN excluded.name ELSE people.name END,
    url = CASE WHEN excluded.url <> '' THEN excluded.url ELSE people.url END,
    image_url = COALESCE(NULLIF(excluded.image_url, ''), people.image_url),
    last_seen_at = excluded.last_seen_at;
""",
            cancellationToken,
            ("$id", person.DoubanId),
            ("$name", person.Name),
            ("$url", person.Url),
            ("$imageUrl", imageUrl),
            ("$now", now));

    private static Task LinkSubjectAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long entryId,
        string subjectId,
        int sortOrder,
        CancellationToken cancellationToken)
        => ExecuteNonQueryAsync(
            connection, transaction,
            """
INSERT OR REPLACE INTO award_entry_subjects(entry_id, douban_subject_id, sort_order)
VALUES($entryId, $subjectId, $sortOrder);
""",
            cancellationToken,
            ("$entryId", entryId),
            ("$subjectId", subjectId),
            ("$sortOrder", sortOrder));

    private static Task LinkPersonAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long entryId,
        string personId,
        int sortOrder,
        CancellationToken cancellationToken)
        => ExecuteNonQueryAsync(
            connection, transaction,
            """
INSERT OR REPLACE INTO award_entry_people(entry_id, douban_person_id, sort_order)
VALUES($entryId, $personId, $sortOrder);
""",
            cancellationToken,
            ("$entryId", entryId),
            ("$personId", personId),
            ("$sortOrder", sortOrder));

    private static Task InsertRelatedEditionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long editionId,
        RelatedEditionData related,
        CancellationToken cancellationToken)
        => ExecuteNonQueryAsync(
            connection, transaction,
            """
INSERT OR REPLACE INTO related_editions(
    edition_id, related_edition_key, year, label, url)
VALUES($editionId, $editionKey, $year, $label, $url);
""",
            cancellationToken,
            ("$editionId", editionId),
            ("$editionKey", related.EditionKey),
            ("$year", related.Year),
            ("$label", related.Label),
            ("$url", related.Url));

    private static Task InsertImportRunAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AwardEditionData document,
        string status,
        string message,
        string startedAt,
        string completedAt,
        CancellationToken cancellationToken)
        => ExecuteNonQueryAsync(
            connection, transaction,
            """
INSERT INTO import_runs(
    requested_url, final_url, event_slug, edition_key,
    status, message, started_at, completed_at)
VALUES(
    $requestedUrl, $finalUrl, $eventSlug, $editionKey,
    $status, $message, $startedAt, $completedAt);
""",
            cancellationToken,
            ("$requestedUrl", document.Source.RequestedUrl),
            ("$finalUrl", document.Source.FinalUrl),
            ("$eventSlug", document.Event.Slug),
            ("$editionKey", document.Edition.Key),
            ("$status", status),
            ("$message", message),
            ("$startedAt", startedAt),
            ("$completedAt", completedAt));

    private static string ComputeContentHash(AwardEditionData document)
    {
        var projection = new
        {
            document.Event.Slug,
            document.Edition.Key,
            document.Edition.Year,
            document.Edition.Title,
            document.Categories
        };

        var json = JsonSerializer.Serialize(projection, JsonDefaults.Write);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ExecuteScalarLongAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value);
    }

    private static void AddParameters(
        SqliteCommand command,
        params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
