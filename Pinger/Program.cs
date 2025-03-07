using System.Net;
using System.Net.NetworkInformation;

if (args is [.., { Length: > 0 } host])
{
}
else
{
    Console.Error.WriteLine("Please provide a host address.");
    return 1;
}

var ttl = 54;
var pause = 500;
var timeout = 1000;
var length = 32;
var count = -1L;
var avg_w_t = 10;
var avg_w_l = 300;
var ignore_error = false;

var buffer = new byte[length];
var options = new PingOptions { Ttl = ttl };

var ping = new Ping();

//Console.WriteLine($"Ping {host} {(count < 0 ? "continiusly" : count)} times, ttl:{ttl}, timeout:{timeout}ms, buffer:{length}b, ignor err:{ignore_error}");

var i = 0;
var n = 0;
var last_ttl = 0;
var last_time = 0L;
var avg_time = 0d;
var last_ip = IPAddress.None;
var lost_count = 0;
while (count-- != 0)
{
    i++;
    await Task.Delay(pause);

    Console.CursorLeft = 0;
    try
    {
        var reply = ping.Send(host, timeout, buffer, options);

        switch (reply.Status)
        {
            case IPStatus.Success:
                last_ip = reply.Address;
                last_ttl = reply.Options!.Ttl;
                last_time = reply.RoundtripTime;
                n++;
                avg_time += (last_time - avg_time) / (avg_w_t <= 0 ? n : avg_w_t);
                break;

            case IPStatus.TimedOut:
                lost_count++;
                break;
            default:
                break;
        }

        var lost_p = (double)lost_count / (avg_w_l <= 0 ? i : avg_w_l) * 100;
        Console.Write($"[{i,5}]ip:{last_ip} t:{last_time,3}(avg:{avg_time:f1})ms ttl:{last_ttl} lost:{lost_count}({lost_p,5:f1}%)");
        Console.Title = $"{host}[{i,4}] t:{avg_time,5:0.0ms} lost:{lost_p,5:f1}%";

        //Console.WriteLine(reply.Status == IPStatus.Success 
        //    ? $"Ping successful, time: {reply.RoundtripTime}ms" 
        //    : "Ping failed");
    }
    catch (PingException ex)
    {
        Console.Write($"Ping failed: {ex.Message}");

        if (!ignore_error)
            return 2;
    }
}

Console.WriteLine();
Console.WriteLine($"Ping {host} complete.");

return 0;
