using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class AdsbClient
{
    private readonly string _host;
    private readonly int _port;

    public AdsbClient(string host = "127.0.0.1", int port = 30001)
    {
        _host = host;
        _port = port;
    }

    public async IAsyncEnumerable<string> ReadMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var pumpTask = PumpMessagesAsync(channel.Writer, cancellationToken);

        await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }

        await pumpTask;
    }

    private async Task PumpMessagesAsync(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        var initialBackoff = TimeSpan.FromMilliseconds(250);
        var maximumBackoff = TimeSpan.FromSeconds(5);
        var currentBackoff = initialBackoff;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(_host, _port, cancellationToken);

                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    currentBackoff = initialBackoff;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                        {
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        await writer.WriteAsync(line, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await Task.Delay(currentBackoff, cancellationToken);
                currentBackoff = TimeSpan.FromMilliseconds(
                    Math.Min(currentBackoff.TotalMilliseconds * 2, maximumBackoff.TotalMilliseconds));
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }
}
