using System.Text.Json.Serialization;

namespace Pinger;

/// <summary>Контекст сериализации информации о репозитории</summary>
[JsonSerializable(typeof(ReleaseInfo[]))]
//[JsonSerializable(typeof(ReleaseInfo))]
//[JsonSerializable(typeof(AuthorInfo))]
//[JsonSerializable(typeof(RepositoryAssetInfo))]
//[JsonSerializable(typeof(UploaderInfo))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, IncludeFields = true)]
internal partial class RepositoryInfoSerializationContext : JsonSerializerContext;