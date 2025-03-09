using System.Text.Json.Serialization;

namespace Pinger;

/// <summary>Информация об авторе</summary>
public class AuthorInfo
{
    /// <summary>Логин автора</summary>
    [JsonPropertyName("login")]
    public string Login { get; init; } = null!;

    /// <summary>ID автора</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Node ID автора</summary>
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = null!;

    /// <summary>URL аватара</summary>
    [JsonPropertyName("avatar_url")]
    public Uri AvatarUrl { get; init; } = null!;

    /// <summary>ID gravatar</summary>
    [JsonPropertyName("gravatar_id")]
    public string GravatarId { get; init; } = null!;

    /// <summary>URL автора</summary>
    [JsonPropertyName("url")]
    public Uri Url { get; init; } = null!;

    /// <summary>HTML URL автора</summary>
    [JsonPropertyName("html_url")]
    public Uri HtmlUrl { get; init; } = null!;

    /// <summary>URL подписчиков</summary>
    [JsonPropertyName("followers_url")]
    public Uri FollowersUrl { get; init; } = null!;

    /// <summary>URL подписок</summary>
    [JsonPropertyName("following_url")]
    public Uri FollowingUrl { get; init; } = null!;

    /// <summary>URL gists</summary>
    [JsonPropertyName("gists_url")]
    public Uri GistsUrl { get; init; } = null!;

    /// <summary>URL starred</summary>
    [JsonPropertyName("starred_url")]
    public Uri StarredUrl { get; init; } = null!;

    /// <summary>URL подписок</summary>
    [JsonPropertyName("subscriptions_url")]
    public Uri SubscriptionsUrl { get; init; } = null!;

    /// <summary>URL организаций</summary>
    [JsonPropertyName("organizations_url")]
    public Uri OrganizationsUrl { get; init; } = null!;

    /// <summary>URL репозиториев</summary>
    [JsonPropertyName("repos_url")]
    public Uri ReposUrl { get; init; } = null!;

    /// <summary>URL событий</summary>
    [JsonPropertyName("events_url")]
    public Uri EventsUrl { get; init; } = null!;

    /// <summary>URL полученных событий</summary>
    [JsonPropertyName("received_events_url")]
    public Uri ReceivedEventsUrl { get; init; } = null!;

    /// <summary>Тип пользователя</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    /// <summary>Тип просмотра пользователя</summary>
    [JsonPropertyName("user_view_type")]
    public string UserViewType { get; init; } = null!;

    /// <summary>Администратор сайта</summary>
    [JsonPropertyName("site_admin")]
    public bool SiteAdmin { get; init; }
}