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

switch (await ProcessArgsAsync(args))
{
    case null:
        return 0;

    case { } return_code when return_code != 0:
        return return_code;
}

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
var last_cursor_pos = 0;

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
            var cursor_pos = Console.CursorLeft;
            if(last_cursor_pos > cursor_pos)
                for(var d = last_cursor_pos - cursor_pos; d >= 0; d--)
                    Console.Write(' ');

            last_cursor_pos = cursor_pos;

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

async Task<int?> ProcessArgsAsync(string[] args)
{
    if (args.Length == 0) return 0;

    for (var j = 0; j < args.Length; j++)
    {
        var parameter = args[j].TrimStart('/', '-').Trim(' ').ToLower();

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
                return 0;

            case "vv":
                Console.WriteLine(Update.CurrentVersion);
                return 0;

            case "v":
            case "version":
                // Вывод версии программы, определенной при сборке
                Console.WriteLine($"Version: {Update.CurrentVersion}");
                return 0;

            case "ttl":
                if (j + 1 < args.Length && int.TryParse(args[j + 1], out var ttl))
                {
                    options.Ttl = ttl;
                    j++;
                }
                break;

            case "p":
            case "pause":
                if (j + 1 < args.Length && int.TryParse(args[j + 1], out var p))
                {
                    pause = p;
                    j++;
                }
                break;

            case "t":
            case "timeout":
                if (j + 1 < args.Length && int.TryParse(args[j + 1], out var t))
                {
                    timeout = t;
                    j++;
                }
                break;

            case "l":
            case "length":
                if (j + 1 < args.Length && int.TryParse(args[j + 1], out var l))
                {
                    length = l;
                    j++;
                }
                break;

            case "c":
            case "count":
                if (j + 1 < args.Length && long.TryParse(args[j + 1], out var c))
                {
                    count = c;
                    j++;
                }
                break;

            case "avgt":
            case "averaget":
                if (j + 1 < args.Length && int.TryParse(args[j + 1], out var at))
                {
                    avg_w_t = at;
                    j++;
                }
                break;

            case "e":
            case "ignoreerror":
                ignore_error = true;
                break;

            case "h":
            case "host":
                if (j + 1 < args.Length)
                {
                    host = args[j + 1];
                    j++;
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
                            Console.WriteLine($"unknown parameter {parameter} ({args[j]})");
                        }
                    }
                    catch (SocketException)
                    {
                        // ignored
                    }

                break;
        }
    }

    return 0;
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
