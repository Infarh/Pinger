using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Pinger;

/// <summary>Информация о релизе</summary>
[DebuggerDisplay("rep: {Name} at {CreatedAt}")]
public class ReleaseInfo
{
    /// <summary>URL релиза</summary>
    [JsonPropertyName("url")]
    public Uri Url { get; init; } = null!;

    /// <summary>URL активов</summary>
    [JsonPropertyName("assets_url")]
    public Uri AssetsUrl { get; init; } = null!;

    /// <summary>URL загрузки</summary>
    [JsonPropertyName("upload_url")]
    public Uri UploadUrl { get; init; } = null!;

    /// <summary>HTML URL релиза</summary>
    [JsonPropertyName("html_url")]
    public Uri HtmlUrl { get; init; } = null!;

    /// <summary>ID релиза</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Информация об авторе</summary>
    [JsonPropertyName("author")]
    public AuthorInfo Author { get; init; } = null!;

    /// <summary>Node ID релиза</summary>
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = null!;

    /// <summary>Тег релиза</summary>
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = null!;

    /// <summary>Целевой коммит</summary>
    [JsonPropertyName("target_commitish")]
    public string TargetCommitis { get; init; } = null!;

    /// <summary>Имя релиза</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>Черновик</summary>
    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    /// <summary>Предрелиз</summary>
    [JsonPropertyName("prerelease")]
    public bool PreRelease { get; init; }

    /// <summary>Дата создания</summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>Дата публикации</summary>
    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; init; }

    /// <summary>Активы релиза</summary>
    [JsonPropertyName("assets")]
    public IReadOnlyList<RepositoryAssetInfo> Assets { get; init; } = null!;

    /// <summary>URL tarball</summary>
    [JsonPropertyName("tarball_url")]
    public Uri TarballUrl { get; init; } = null!;

    /// <summary>URL zipball</summary>
    [JsonPropertyName("zipball_url")]
    public Uri ZipballUrl { get; init; } = null!;

    /// <summary>Описание релиза</summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = null!;

    /// <summary>Количество упоминаний</summary>
    [JsonPropertyName("mentions_count")]
    public int MentionsCount { get; init; }
}