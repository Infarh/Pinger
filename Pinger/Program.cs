using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;

//if (args.Length == 0)
//{
//    Console.Error.WriteLine("Please provide a host address.");
//    return 1;
//}

var pause = 250;
var timeout = 1000;
var length = 32;
var count = -1L;
var avg_w_t = 100;
var ignore_error = false;
var clean = false;

var buffer = new byte[length];
var options = new PingOptions { Ttl = 54 };

string? host = "ya.ru";
if (!await ProcessArgsAsync(args))
    return -2;

if (host is null)
{
    host = args[^1];

    if (!IPAddress.TryParse(host, out _) && await Dns.GetHostAddressesAsync(host) is [])
    {
        Console.Error.WriteLine("Please provide a host address.");
        return 1;
    }
}

var ping = new Ping();

var i = 0;
var n = 0;
var last_ttl = 0;
var last_time = 0L;
var avg_time = 0d;
var last_ip = IPAddress.None;
var lost_count = 0;

var ip_str = "";
var host_str = "";

var cancellation = new CancellationTokenSource();
var cancel = cancellation.Token;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

if (clean)
    Console.Clear();

try
{
    while (!cancel.IsCancellationRequested && count-- != 0)
    {
        i++;
        await Task.Delay(pause, cancel);

        Console.CursorLeft = 0;
        try
        {
            var reply = ping.Send(host, timeout, buffer, options);

            switch (reply.Status)
            {
                case IPStatus.Success:
                    if (!reply.Address.Equals(last_ip))
                    {
                        last_ip = reply.Address;
                        ip_str = last_ip.ToString();
                        host_str = host == ip_str ? $"ip:{ip_str}" : $"host:{host}({ip_str})";
                    }

                    last_ttl = reply.Options!.Ttl;
                    last_time = reply.RoundtripTime;
                    n++;
                    if (n == 1)
                        avg_time = last_time;
                    else
                        avg_time += (last_time - avg_time) / (avg_w_t <= 0 ? n : avg_w_t);
                    break;

                case IPStatus.TimedOut:
                    lost_count++;
                    break;
                //default: break;
            }

            var lost_p = (double)lost_count / i * 100;
            
            Console.Write($"[{i,6}]{host_str} t:{last_time,3}(avg:{avg_time,6:f1})ms ttl:{last_ttl} lost:{lost_count}({lost_p,5:f1}%)");

            Console.Title = $"{host}[{i,4}] t:{avg_time,5:0.0ms} lost:{lost_p,5:f1}%";

        }
        catch (PingException ex)
        {
            Console.Write($"Ping failed: {ex.Message}");

            if (!ignore_error)
                return 2;
        }
    }
}
catch(TaskCanceledException)
{
    // ignored
}

Console.WriteLine();
Console.WriteLine($"Ping {host} complete.");

return cancel.IsCancellationRequested ? -1 : 0;

async Task<bool> ProcessArgsAsync(string[] args)
{
    if (args.Length == 0) return true;

    for (var i = 0; i < args.Length; i++)
    {
        var parameter = args[i].TrimStart('/', '-').Trim(' ').ToLower();

        switch (parameter)
        {
            case "?":
            case "help":
                Console.WriteLine("Usage: pinger [options] [host]");
                Console.WriteLine("Options:");
                Console.WriteLine("  /? or /help - show this help");
                Console.WriteLine("  /ttl or /ttl <ttl> - set ttl");
                Console.WriteLine("  /p or /pause <pause> - set pause between pings");
                Console.WriteLine("  /t or /timeout <timeout> - set timeout");
                Console.WriteLine("  /l or /length <length> - set buffer length");
                Console.WriteLine("  /c or /count <count> - set count of pings");
                Console.WriteLine("  /avgt or /averaget <averaget> - set average time weight");
                Console.WriteLine("  /e or /ignoreerror - ignore ping errors");
                Console.WriteLine("  /h or /host <host> - set host");
                Console.WriteLine("  /cls or /cln or /clean or /clear - clear console before start");
                Console.WriteLine("  /v or /version - show program version");
                return false;

            case "v":
            case "version":
                // Вывод версии программы, определенной при сборке
                var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
                Console.WriteLine($"Version: {version?.Version}");
                return false;

            case "ttl":
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var ttl))
                {
                    options.Ttl = ttl;
                    i++;
                }
                break;

            case "p":
            case "pause":
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
                {
                    pause = p;
                    i++;
                }
                break;

            case "t":
            case "timeout":
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var t))
                {
                    timeout = t;
                    i++;
                }
                break;

            case "l":
            case "length":
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var l))
                {
                    length = l;
                    i++;
                }
                break;

            case "c":
            case "count":
                if (i + 1 < args.Length && long.TryParse(args[i + 1], out var c))
                {
                    count = c;
                    i++;
                }
                break;

            case "avgt":
            case "averaget":
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var at))
                {
                    avg_w_t = at;
                    i++;
                }
                break;

            case "e":
            case "ignoreerror":
                ignore_error = true;
                break;

            case "h":
            case "host":
                if (i + 1 < args.Length)
                {
                    host = args[i + 1];
                    i++;
                }
                break;

            case "cls":
            case "cln":
            case "clean":
            case "clear":
                clean = true;
                break;

            default:

                if (IPAddress.TryParse(parameter, out _))
                    host = parameter;
                else
                    try
                    {
                        if (await Dns.GetHostAddressesAsync(parameter) is { Length: > 0 })
                            host = parameter;
                        else
                        {
                            Console.WriteLine($"unknown parameter {parameter} ({args[i]})");
                        }
                    }
                    catch (SocketException)
                    {
                        // ignored
                    }

                break;
        }
    }

    return true;
}

internal readonly struct OutputColor : IDisposable
{
    private readonly ConsoleColor _BackgroundColor = Console.BackgroundColor;
    private readonly ConsoleColor _ForegroundColor = Console.ForegroundColor;

    public OutputColor(ConsoleColor Foreground, ConsoleColor? Background = null)
    {
        //_BackgroundColor = Console.BackgroundColor;
        //_ForegroundColor = Console.ForegroundColor;

        Console.ForegroundColor = Foreground;
        if (Background is { } background)
            Console.BackgroundColor = background;
    }

    public void Dispose()
    {
        Console.ForegroundColor = _ForegroundColor;
        Console.BackgroundColor = _BackgroundColor;
    }
}
