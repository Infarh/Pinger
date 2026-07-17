using System.Net;
using System.Net.NetworkInformation;

var cancellation = new CancellationTokenSource();
var cancel = cancellation.Token;

var (exit_code, options) = await CommandLineOptions.ParseAsync(args, cancel);

if (exit_code is not null)
    return exit_code.Value;

var opts = options!;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

if (opts.Clean)
    Console.Clear();

// Резервируем строки под каждый хост
var base_row = 0;
if (!Console.IsOutputRedirected)
{
    base_row = Console.CursorTop;
    for (var j = 0; j < opts.Hosts.Count; j++)
        Console.WriteLine();
}

// Запускаем параллельный пинг для каждого хоста
var console_lock = new object();
var tasks = opts.Hosts.Select((host, idx) => PingHostAsync(host, base_row + idx, console_lock, opts, cancel));
var results = await Task.WhenAll(tasks);

// Итоговая статистика
if (!Console.IsOutputRedirected)
{
    Console.CursorTop = base_row + opts.Hosts.Count;
    Console.CursorLeft = 0;
}

if (results.Length > 1)
{
    var total_tx = results.Sum(r => r.Total);
    var total_rx = results.Sum(r => r.Received);
    var total_lost = total_tx - total_rx;
    var total_loss_pct = total_tx > 0 ? (double)total_lost / total_tx * 100 : 0d;
    Console.WriteLine();
    Console.WriteLine($"--- all hosts ping statistics ---");
    Console.WriteLine($"{total_tx} packets transmitted, {total_rx} received, {total_loss_pct:f1}% packet loss");
}

return cancel.IsCancellationRequested ? -1 : results.All(r => r.ExitCode == 0) ? 0 : 2;

/// <summary>Пингует хост, обновляя закреплённую строку консоли</summary>
async Task<PingResult> PingHostAsync(string Host, int RowIndex, object ConsoleLock, CommandLineOptions Opts, CancellationToken Cancel)
{
    var buffer = new byte[Opts.Length];
    var ping_options = new PingOptions { Ttl = Opts.Ttl };
    var ping = new Ping();

    var i = 0;
    var n = 0;
    var last_ttl = 0;
    var last_time = 0L;
    var avg_time = 0d;
    var last_ip = IPAddress.None;
    var lost_count = 0;
    var min_time = long.MaxValue;
    var max_time = long.MinValue;

    var ip_str = "";
    var host_str = Host;
    var need_resolve = !IPAddress.TryParse(Host, out var _);

    var time_interval = TimeSpan.Zero;
    var remaining = Opts.Count;

    while (!Cancel.IsCancellationRequested && (remaining == -1 || remaining-- > 0))
    {
        i++;
        var current_pause = Opts.Pause - time_interval.Milliseconds;
        if (current_pause > 0)
        {
            try { await Task.Delay(current_pause, Cancel); }
            catch (OperationCanceledException) { break; }
        }

        var start_time = Environment.TickCount64;

        try
        {
            IPAddress target;
            if (need_resolve || last_ip == IPAddress.None)
            {
                var addresses = await Dns.GetHostAddressesAsync(Host, Cancel);
                if (addresses.Length == 0)
                {
                    WriteAt(RowIndex, ConsoleLock, ConsoleColor.Red, $"[err ]{Host} — DNS resolve failed");
                    break;
                }
                last_ip = addresses[0];
                ip_str = last_ip.ToString();
                host_str = Host == ip_str ? $"ip:{ip_str}" : $"host:{Host}({ip_str})";
            }

            target = last_ip;

            var reply = await ping.SendPingAsync(target, Opts.Timeout, buffer, ping_options);

            switch (reply.Status)
            {
                case IPStatus.Success:
                    if (!reply.Address.Equals(last_ip))
                    {
                        last_ip = reply.Address;
                        ip_str = last_ip.ToString();
                        host_str = Host == ip_str ? $"ip:{ip_str}" : $"host:{Host}({ip_str})";
                    }

                    last_ttl = reply.Options!.Ttl;
                    last_time = reply.RoundtripTime;
                    n++;

                    if (n == 1)
                        avg_time = last_time;
                    else
                        avg_time += (last_time - avg_time) / (Opts.AverageWindow <= 0 ? n : Opts.AverageWindow);

                    if (last_time < min_time) min_time = last_time;
                    if (last_time > max_time) max_time = last_time;
                    break;

                case IPStatus.TimedOut:
                    lost_count++;
                    last_time = 0;
                    last_ttl = 0;
                    break;

                default:
                    lost_count++;
                    last_time = 0;
                    last_ttl = 0;
                    break;
            }

            var lost_p = lost_count > 0 ? (double)lost_count / i * 100 : 0d;
            var trend = last_time > avg_time ? '+' : last_time < avg_time ? '-' : '=';

            var line_color = last_time == 0 ? ConsoleColor.Red
                : last_time > avg_time * 1.5 ? ConsoleColor.Yellow
                : ConsoleColor.Green;

            WriteAt(RowIndex, ConsoleLock, line_color,
                $"[{i,6}]{host_str} {trend}t:{last_time,3}(avg:{avg_time,6:f1})ms ttl:{last_ttl} lost:{lost_count}({lost_p,5:f1}%)");

            var end_time = Environment.TickCount64;
            time_interval = TimeSpan.FromMilliseconds(end_time - start_time);
        }
        catch (PingException ex)
        {
            WriteAt(RowIndex, ConsoleLock, ConsoleColor.Red, $"[err ]{Host} — {ex.Message}");

            if (!Opts.IgnoreErrors)
                return new PingResult(Host, i, n, lost_count, min_time, avg_time, max_time, 2);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }

    // Итог по хосту
    var loss_pct = i > 0 ? (double)lost_count / i * 100 : 0d;
    WriteAt(RowIndex, ConsoleLock, ConsoleColor.White,
        $"[{i,6}]{Host} — complete: {n} recv, {loss_pct:f1}% loss"
        + (n > 0 ? $", avg:{avg_time:f1}ms" : ""));

    return new PingResult(Host, i, n, lost_count, min_time, avg_time, max_time, 0);
}

/// <summary>Обновляет строку консоли по указанному индексу, затирая предыдущее содержимое</summary>
static void WriteAt(int Row, object Lock, ConsoleColor Color, string Text)
{
    if (Console.IsOutputRedirected)
    {
        using (new OutputColor(Color))
            Console.WriteLine(Text);
        return;
    }

    lock (Lock)
    {
        try
        {
            Console.CursorTop = Row;
            Console.CursorLeft = 0;
            var w = Console.BufferWidth - 1;
            Console.Write(Text.Length < w ? Text.PadRight(w) : Text[..w]);
        }
        catch
        {
            using (new OutputColor(Color))
                Console.WriteLine(Text);
        }
    }
}

/// <summary>Результат пинга одного хоста</summary>
internal sealed record PingResult(string Host, int Total, int Received, int Lost, long MinTime, double AvgTime, long MaxTime, int ExitCode);

/// <summary>Устанавливает цвет консоли и восстанавливает при Dispose</summary>
internal readonly struct OutputColor : IDisposable
{
    private readonly ConsoleColor _Background = Console.BackgroundColor;
    private readonly ConsoleColor _Foreground = Console.ForegroundColor;

    public OutputColor(ConsoleColor Foreground, ConsoleColor? Background = null)
    {
        Console.ForegroundColor = Foreground;
        if (Background is { } bg)
            Console.BackgroundColor = bg;
    }

    public void Dispose()
    {
        Console.ForegroundColor = _Foreground;
        Console.BackgroundColor = _Background;
    }
}
