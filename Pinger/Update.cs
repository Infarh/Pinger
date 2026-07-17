using System.ComponentModel;
using System.Diagnostics;
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
            MaxResponseContentBufferSize = 1024 * 50,
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Pinger");

        return client;
    }

    public static async Task CheckUpdateAsync(CancellationToken Cancel = default)
    {
        Console.WriteLine("Check updates...");

        using var http = GetClient();

        if (await GetRepositoryInfoAsync(http, Cancel).ConfigureAwait(false) is not { Draft: false, Assets: [ { BrowserDownloadUrl: var download_uri }, .. ] } release_info)
            return;

        var current_version = CurrentVersion;
        VersionInfo server_version = release_info.TagName;

        Console.WriteLine($"Current version is {current_version}");
        Console.WriteLine($" Server version is {server_version}");

        if (server_version <= CurrentVersion)
        {
            Console.WriteLine("No update required.");
            return;
        }

        var program_file = new FileInfo(Environment.ProcessPath!);
        var program_dir = program_file.Directory!.FullName;
        var program_file_name = program_file.Name;
        var program_file_name_without_ext = Path.GetFileNameWithoutExtension(program_file_name);
        var program_ext = program_file.Extension;

        // Сначала бекапим текущий файл
        var backup_file_path = Path.Combine(program_dir, $"{program_file_name_without_ext}[{CurrentVersion}]{program_ext}.bak");
        await using (var program_file_stream = program_file.OpenRead())
        await using (var backup_file_stream = new FileStream(backup_file_path, FileMode.Create, FileAccess.Write, FileShare.None))
            await program_file_stream.CopyToAsync(backup_file_stream, Cancel);

        Console.WriteLine($"Backup created: {backup_file_path}");

        // Скачиваем новую версию
        var downloaded_file_name = $"{program_file_name_without_ext}[{server_version}]{program_ext}";
        var path_to_download = Path.Combine(program_dir, downloaded_file_name);

        await download_uri.DownloadFileAsync(http, path_to_download, Cancel);

        Console.WriteLine($"Downloaded: {path_to_download}");

        // Заменяем текущий файл скачанным
        program_file.Delete();
        File.Move(path_to_download, program_file.FullName);

        Console.WriteLine("Update applied. Restarting...");

        // Запускаем обновлённую версию
        Process.Start(new ProcessStartInfo
        {
            FileName = program_file.FullName,
            Arguments = Environment.GetCommandLineArgs().Length > 1
                ? string.Join(" ", Environment.GetCommandLineArgs()[1..].Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
                : "",
            UseShellExecute = true
        });

        // Завершаем текущий процесс
        Environment.Exit(0);
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