using System.Net;
using System.Net.NetworkInformation;

var cancellation = new CancellationTokenSource();
var cancel = cancellation.Token;

var (exit_code, options) = await CommandLineOptions.ParseAsync(args, cancel);

if (exit_code is not null)
    return exit_code.Value;

var opts = options!;

var buffer = new byte[opts.Length];
var ping_options = new PingOptions { Ttl = opts.Ttl };
var ping = new Ping();

var i = 0;
var n = 0;
var last_ttl = 0;
var last_time = 0L;
var avg_time = 0d;
var last_ip = IPAddress.None;
var lost_count = 0;
var last_cursor_pos = 0;

// Для ASCII-графика
var graph_width = 40;
var graph_values = new CircularBuffer<int>(graph_width);

var ip_str = "";
var host_str = "";

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

if (opts.Clean)
    Console.Clear();

var min_time = long.MaxValue;
var max_time = long.MinValue;

try
{
    var time_interval = TimeSpan.Zero;
    var remaining = opts.Count;
    while (!cancel.IsCancellationRequested && (remaining == -1 || remaining-- > 0))
    {
        i++;
        var current_pause = opts.Pause - time_interval.Milliseconds;
        if(current_pause > 0)
            await Task.Delay(current_pause, cancel);
        var start_time = Environment.TickCount64;

        if (!Console.IsOutputRedirected)
            Console.CursorLeft = 0;

        try
        {
            var target = last_ip != IPAddress.None
                ? last_ip
                : (await Dns.GetHostAddressesAsync(opts.Host, cancel))[0];
            var reply = await ping.SendPingAsync(target, opts.Timeout, buffer, ping_options);

            switch (reply.Status)
            {
                case IPStatus.Success:
                    if (!reply.Address.Equals(last_ip))
                    {
                        last_ip = reply.Address;
                        ip_str = last_ip.ToString();
                        host_str = opts.Host == ip_str ? $"ip:{ip_str}" : $"host:{opts.Host}({ip_str})";
                    }

                    last_ttl = reply.Options!.Ttl;
                    last_time = reply.RoundtripTime;
                    n++;

                    if (n == 1)
                        avg_time = last_time;
                    else
                        avg_time += (last_time - avg_time) / (opts.AverageWindow <= 0 ? n : opts.AverageWindow);

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

            using (new OutputColor(last_time == 0 ? ConsoleColor.Red : last_time > avg_time * 1.5 ? ConsoleColor.Yellow : ConsoleColor.Green))
                Console.Write($"[{i,6}]{host_str} {trend}t:{last_time,3}(avg:{avg_time,6:f1})ms ttl:{last_ttl} lost:{lost_count}({lost_p,5:f1}%)");

            // ASCII-график
            if (!Console.IsOutputRedirected && last_cursor_pos > 0)
            {
                var graph_line = BuildGraph(graph_values, graph_width);
                Console.Write($" |{graph_line}");
            }

            var cursor_pos = Console.CursorLeft;
            if(last_cursor_pos > cursor_pos)
                for(var d = last_cursor_pos - cursor_pos; d >= 0; d--)
                    Console.Write(' ');

            last_cursor_pos = cursor_pos;

            try { Console.Title = $"{opts.Host}[{i,4}] {trend}t:{avg_time,5:0.0}ms lost:{lost_p,5:f1}%"; } catch { /* ignored */ }

        }
        catch (PingException ex)
        {
            using (new OutputColor(ConsoleColor.Red))
                Console.WriteLine($"Ping failed: {ex.Message}");

            if (!Console.IsOutputRedirected && last_cursor_pos > 0)
                Console.CursorLeft = 0;

            if (!opts.IgnoreErrors)
                return 2;
        }

        var end_time = Environment.TickCount64;
        time_interval = TimeSpan.FromMilliseconds(end_time - start_time);
    }
}
catch(OperationCanceledException)
{
    // ignored
}

// Статистика при завершении
var total = i;
var received = n;
var lost = lost_count;
var loss_pct = total > 0 ? (double)lost / total * 100 : 0d;

Console.WriteLine();
Console.WriteLine($"--- {opts.Host} ping statistics ---");
Console.WriteLine($"{total} packets transmitted, {received} received, {loss_pct:f1}% packet loss");
if (received > 0)
    Console.WriteLine($"rtt min/avg/max = {min_time}/{avg_time:f1}/{max_time} ms");

return cancel.IsCancellationRequested ? -1 : 0;

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

/// <summary>Кольцевой буфер для хранения последних N значений</summary>
internal sealed class CircularBuffer<T> : IEnumerable<T>
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

    public IEnumerator<T> GetEnumerator()
    {
        for (var j = 0; j < _Count; j++)
            yield return this[j];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Максимальное значение среди положительных</summary>
    public int MaxValue()
    {
        var max = 0;
        for (var j = 0; j < _Count; j++)
        {
            var v = (int)(object)this[j]!;
            if(v > max) max = v;
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
