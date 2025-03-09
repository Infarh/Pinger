using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Pinger;

/// <summary>Информация об активе</summary>
[DebuggerDisplay("{Name}: {Size}b")]
public class RepositoryAssetInfo
{
    /// <summary>URL актива</summary>
    [JsonPropertyName("url")]
    public Uri Url { get; init; } = null!;

    /// <summary>ID актива</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Node ID актива</summary>
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = null!;

    /// <summary>Имя актива</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>Метка актива</summary>
    [JsonPropertyName("label")]
    public object Label { get; init; } = null!;

    /// <summary>Информация о загрузчике</summary>
    [JsonPropertyName("uploader")]
    public UploaderInfo Uploader { get; init; } = null!;

    /// <summary>Тип содержимого</summary>
    [JsonPropertyName("content_type")]
    public string ContentType { get; init; } = null!;

    /// <summary>Состояние актива</summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = null!;

    /// <summary>Размер актива</summary>
    [JsonPropertyName("size")]
    public int Size { get; init; }

    /// <summary>Количество загрузок</summary>
    [JsonPropertyName("download_count")]
    public int DownloadCount { get; init; }

    /// <summary>Дата создания</summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>Дата обновления</summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    /// <summary>URL для загрузки через браузер</summary>
    [JsonPropertyName("browser_download_url")]
    public Uri BrowserDownloadUrl { get; init; } = null!;
}