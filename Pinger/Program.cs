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

// Запускаем параллельный пинг для каждого хоста
var tasks = opts.Hosts.Select((host, idx) => PingHostAsync(host, idx, opts, cancel));
var results = await Task.WhenAll(tasks);

// Итоговая статистика
Console.WriteLine();
if (results.Length > 1)
{
    var total_tx = results.Sum(r => r.Total);
    var total_rx = results.Sum(r => r.Received);
    var total_lost = total_tx - total_rx;
    var total_loss_pct = total_tx > 0 ? (double)total_lost / total_tx * 100 : 0d;
    Console.WriteLine($"--- all hosts ping statistics ---");
    Console.WriteLine($"{total_tx} packets transmitted, {total_rx} received, {total_loss_pct:f1}% packet loss");
}

return cancel.IsCancellationRequested ? -1 : results.All(r => r.ExitCode == 0) ? 0 : 2;

/// <summary>Пингует хост в отдельном потоке, выводя результаты на закреплённой строке консоли</summary>
async Task<PingResult> PingHostAsync(string Host, int RowIndex, CommandLineOptions Opts, CancellationToken Cancel)
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

    var graph_width = 20;
    var graph_values = new CircularBuffer<int>(graph_width);
    var console_lock = new object();

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
            // Резолвим IP, если нужно
            IPAddress target;
            if (need_resolve || last_ip == IPAddress.None)
            {
                var addresses = await Dns.GetHostAddressesAsync(Host, Cancel);
                if (addresses.Length == 0)
                {
                    lock (console_lock) WriteLineAt(RowIndex, $"[err ]{Host} — DNS resolve failed");
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

                    graph_values.Add((int)last_time);
                    break;

                case IPStatus.TimedOut:
                    lost_count++;
                    last_time = 0;
                    last_ttl = 0;
                    graph_values.Add(-1);
                    break;

                default:
                    lost_count++;
                    last_time = 0;
                    last_ttl = 0;
                    graph_values.Add(-1);
                    break;
            }

            var lost_p = lost_count > 0 ? (double)lost_count / i * 100 : 0d;
            var trend = last_time > avg_time ? '+' : last_time < avg_time ? '-' : '=';

            // Вывод на закреплённой строке
            var line_color = last_time == 0 ? ConsoleColor.Red
                : last_time > avg_time * 1.5 ? ConsoleColor.Yellow
                : ConsoleColor.Green;

            lock (console_lock)
            {
                using (new OutputColor(line_color))
                    WriteLineAt(RowIndex,
                        $"[{i,6}]{host_str} {trend}t:{last_time,3}(avg:{avg_time,6:f1})ms ttl:{last_ttl} lost:{lost_count}({lost_p,5:f1}%)"
                        + $" |{BuildGraph(graph_values, graph_width)}");
            }

            var end_time = Environment.TickCount64;
            time_interval = TimeSpan.FromMilliseconds(end_time - start_time);
        }
        catch (PingException ex)
        {
            lock (console_lock)
            {
                using (new OutputColor(ConsoleColor.Red))
                    WriteLineAt(RowIndex, $"[err ]{Host} — {ex.Message}");
            }

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
    lock (console_lock)
    {
        using (new OutputColor(ConsoleColor.White))
            WriteLineAt(RowIndex,
                $"[{i,6}]{Host} — complete: {n} recv, {loss_pct:f1}% loss"
                + (n > 0 ? $", avg:{avg_time:f1}ms" : ""));
    }

    return new PingResult(Host, i, n, lost_count, min_time, avg_time, max_time, 0);
}

/// <summary>Выводит текст на указанной строке консоли, не сдвигая другие строки</summary>
static void WriteLineAt(int Row, string Text)
{
    if (Console.IsOutputRedirected)
    {
        Console.WriteLine(Text);
        return;
    }

    try
    {
        var (left, top) = (Console.CursorLeft, Console.CursorTop);
        Console.SetCursorPosition(0, Row);
        Console.Write(new string(' ', Console.BufferWidth - 1));
        Console.SetCursorPosition(0, Row);
        Console.Write(Text);
        Console.SetCursorPosition(left, top);
    }
    catch
    {
        Console.WriteLine(Text);
    }
}

/// <summary>Строит ASCII-график из последних значений</summary>
static string BuildGraph(CircularBuffer<int> Values, int Width)
{
    if (Values.Count == 0) return new string(' ', Width);

    var max = Values.MaxValue();
    if (max == 0) return new string(' ', Width);

    var chars = new char[Width];
    for (var j = 0; j < Width; j++)
    {
        var idx = Values.Count - Width + j;
        if (idx < 0) { chars[j] = ' '; continue; }

        var v = Values[idx];
        if (v < 0)
            chars[j] = 'x';
        else
        {
            var h = (int)((double)v / max * 4);
            chars[j] = h switch
            {
                0 => '\u2581',
                1 => '\u2582',
                2 => '\u2584',
                3 => '\u2586',
                _ => '\u2588',
            };
        }
    }
    return new string(chars);
}

/// <summary>Результат пинга одного хоста</summary>
internal sealed record PingResult(string Host, int Total, int Received, int Lost, long MinTime, double AvgTime, long MaxTime, int ExitCode);

/// <summary>Кольцевой буфер для хранения последних N значений</summary>
internal sealed class CircularBuffer<T>
{
    private readonly T[] _Buffer;
    private int _Head;
    private int _Count;

    public CircularBuffer(int Capacity) => _Buffer = new T[Capacity];

    public int Count => _Count;

    public T this[int Index]
    {
        get
        {
            if (Index < 0 || Index >= _Count)
                throw new ArgumentOutOfRangeException(nameof(Index));
            return _Buffer[(_Head - _Count + Index + _Buffer.Length) % _Buffer.Length];
        }
    }

    public void Add(T Value)
    {
        _Buffer[_Head] = Value;
        _Head = (_Head + 1) % _Buffer.Length;
        if (_Count < _Buffer.Length)
            _Count++;
    }

    /// <summary>Максимальное значение среди положительных</summary>
    public int MaxValue()
    {
        var max = 0;
        for (var j = 0; j < _Count; j++)
        {
            var v = (int)(object)this[j]!;
            if (v > max) max = v;
        }
        return max;
    }
}

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
