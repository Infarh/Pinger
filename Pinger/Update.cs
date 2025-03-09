using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Pinger;

internal static class Update
{
    private static string? __CurrentVersion;
    public static VersionInfo CurrentVersion => __CurrentVersion ??= typeof(Update).Assembly.GetName().Version!.ToString();

    private const string __BaseUri = "https://api.github.com";
    private const string __RepositoryUri = "/repos/Infarh/Pinger/releases";

    private static HttpClient GetClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new(__BaseUri),
            MaxResponseContentBufferSize = 1024 * 5,
            Timeout = TimeSpan.FromSeconds(3)
        };
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Pinger");

        return client;
    }

    public static async Task CheckUpdateAsync(CancellationToken Cancel = default)
    {
        using var http = GetClient();

        if (await GetRepositoryInfoAsync(http, Cancel).ConfigureAwait(false) is not { Assets: [ { BrowserDownloadUrl: var download_uri }, .. ] } release_info)
            return;

        VersionInfo server_version = release_info.TagName;
        if (server_version <= CurrentVersion)
            return;

        var v = Environment.Version;

        var program_file = new FileInfo(Environment.ProcessPath!);
        var program_dir = program_file.Directory!.FullName;
        var program_file_name = program_file.Name;
        var program_file_name_without_ext = Path.GetFileNameWithoutExtension(program_file_name);

        var program_file_name_new = $"{program_file_name_without_ext}[{server_version}]{program_file.Extension}";
        var path_to_download = Path.Combine(program_dir, program_file_name_new);

        var downloaded_file = await download_uri.DownloadFileAsync(http, path_to_download, Cancel);
        var backup_file = new FileInfo(Path.Combine(program_dir, $"{program_file_name_without_ext}[{CurrentVersion}]{program_file.Extension}.bak"));

        await using(var program_file_stream = program_file.OpenRead())
        await using(var backup_file_stream = backup_file.OpenWrite())
            await program_file_stream.CopyToAsync(backup_file_stream, Cancel);
    }

    private static async Task<FileInfo> DownloadFileAsync(
        this Uri FileUri,
        HttpClient http,
        string FilePath,
        CancellationToken Cancel = default)
    {
         // Получаем ответ от сервера
        using var response = await http.GetAsync(FileUri, HttpCompletionOption.ResponseHeadersRead, Cancel);
        response.EnsureSuccessStatusCode();

        // Получаем поток содержимого ответа
        await using var content_stream = await response.Content.ReadAsStreamAsync(Cancel);

        // Открываем поток для записи файла
        await using var file_stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None);

        // Копируем содержимое ответа в файл
        await content_stream.CopyToAsync(file_stream, Cancel);

        return new(FilePath);
    }

    private static ReleaseInfo? __RepositoryInfo;

    private static async Task<ReleaseInfo?> GetRepositoryInfoAsync(HttpClient http, CancellationToken Cancel)
    {
        try
        {
            if (__RepositoryInfo is not null) return __RepositoryInfo;

           using var response = await http.GetAsync(__RepositoryUri, Cancel);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = response.Content;
            var type_info = RepositoryInfoSerializationContext.Default.ReleaseInfoArray;

            return await content.ReadFromJsonAsync(type_info, Cancel) is [{ } info, ..]
                ? __RepositoryInfo = info
                : null;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}