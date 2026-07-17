using System.Net;
using System.Net.Sockets;

namespace Pinger;

/// <summary>Результат парсинга аргументов командной строки</summary>
internal sealed record CommandLineOptions(
    IReadOnlyList<string> Hosts,
    int Pause,
    int Timeout,
    int Length,
    long Count,
    int AverageWindow,
    bool IgnoreErrors,
    bool Clean,
    int Ttl)
{
    /// <summary>Парсит аргументы командной строки</summary>
    public static async Task<(int? ExitCode, CommandLineOptions? Options)> ParseAsync(string[] Args, CancellationToken Cancel)
    {
        if (Args.Length == 0)
            return (null, new CommandLineOptions(
                Hosts: ["ya.ru"],
                Pause: 250,
                Timeout: 1000,
                Length: 32,
                Count: -1L,
                AverageWindow: 100,
                IgnoreErrors: false,
                Clean: false,
                Ttl: 54));

        var hosts = new List<string>();
        var pause = 250;
        var timeout = 1000;
        var length = 32;
        var count = -1L;
        var avg_w_t = 100;
        var ignore_error = false;
        var clean = false;
        var ttl = 54;

        for (var j = 0; j < Args.Length; j++)
        {
            var parameter = Args[j].TrimStart('/', '-').Trim(' ').ToLower();

            switch (parameter)
            {
                case "?":
                case "help":
                    PrintHelp();
                    return (0, null);

                case "update":
                    await Update.CheckUpdateAsync(Cancel);
                    return (0, null);

                case "vv":
                    Console.WriteLine(Update.CurrentVersion);
                    return (0, null);

                case "v":
                case "version":
                    Console.WriteLine($"Version: {Update.CurrentVersion}");
                    return (0, null);

                case "ttl":
                    if (j + 1 < Args.Length && int.TryParse(Args[j + 1], out var t))
                    {
                        ttl = t;
                        j++;
                    }
                    break;

                case "p":
                case "pause":
                    if (j + 1 < Args.Length && int.TryParse(Args[j + 1], out var p))
                    {
                        pause = p;
                        j++;
                    }
                    break;

                case "t":
                case "timeout":
                    if (j + 1 < Args.Length && int.TryParse(Args[j + 1], out var to))
                    {
                        timeout = to;
                        j++;
                    }
                    break;

                case "l":
                case "length":
                    if (j + 1 < Args.Length && int.TryParse(Args[j + 1], out var l))
                    {
                        length = l;
                        j++;
                    }
                    break;

                case "c":
                case "count":
                    if (j + 1 < Args.Length && long.TryParse(Args[j + 1], out var c))
                    {
                        count = c;
                        j++;
                    }
                    break;

                case "avgt":
                case "averaget":
                    if (j + 1 < Args.Length && int.TryParse(Args[j + 1], out var at))
                    {
                        avg_w_t = at;
                        j++;
                    }
                    break;

                case "e":
                case "ignoreerror":
                    ignore_error = true;
                    break;

                case "cls":
                case "cln":
                case "clean":
                case "clear":
                    clean = true;
                    break;

                default:
                    if (IsValidHost(parameter))
                    {
                        hosts.Add(parameter);
                        break;
                    }
                    Console.Error.WriteLine($"unknown parameter: {Args[j]}");
                    return (1, null);
            }
        }

        if (hosts.Count == 0)
            hosts.Add("ya.ru");

        return (null, new CommandLineOptions(
            Hosts: hosts.AsReadOnly(),
            Pause: pause,
            Timeout: timeout,
            Length: length,
            Count: count,
            AverageWindow: avg_w_t,
            IgnoreErrors: ignore_error,
            Clean: clean,
            Ttl: ttl));
    }

    private static bool IsValidHost(string Parameter)
    {
        if (IPAddress.TryParse(Parameter, out _))
            return true;
        try
        {
            return Dns.GetHostAddresses(Parameter) is { Length: > 0 };
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: pinger [options] [host1 host2 ...]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -? or --help          show this help");
        Console.WriteLine("  --ttl <ttl>           set ttl");
        Console.WriteLine("  -p or --pause <pause> set pause between pings");
        Console.WriteLine("  -t or --timeout <timeout> set timeout");
        Console.WriteLine("  -l or --length <length> set buffer length");
        Console.WriteLine("  -c or --count <count> set count of pings");
        Console.WriteLine("  --avgt or --averaget <averaget> set average time weight");
        Console.WriteLine("  -e or --ignoreerror   ignore ping errors");
        Console.WriteLine("  --cls or --clean      clear console before start");
        Console.WriteLine($"  -v or --version       show program version \"Version: {Update.CurrentVersion}\"");
        Console.WriteLine($"  --vv                  show clean program version \"{Update.CurrentVersion}\"");
        Console.WriteLine("  -u or --update        check update program");
    }
}
