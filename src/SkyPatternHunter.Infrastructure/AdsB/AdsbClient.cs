using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class AdsbClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly Action? _onConnectAttempt;
    private readonly Action? _onConnected;
    private readonly Action<string>? _onDisconnected;
    private readonly Action<Exception>? _onError;
    private readonly Action<TimeSpan>? _onReconnectScheduled;

    public AdsbClient(
        string host = "127.0.0.1",
        int port = 30002,
        Action? onConnectAttempt = null,
        Action? onConnected = null,
        Action<string>? onDisconnected = null,
        Action<Exception>? onError = null,
        Action<TimeSpan>? onReconnectScheduled = null)
    {
        _host = host;
        _port = port;
        _onConnectAttempt = onConnectAttempt;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
        _onError = onError;
        _onReconnectScheduled = onReconnectScheduled;
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
                    _onConnectAttempt?.Invoke();
                    await client.ConnectAsync(_host, _port, cancellationToken);
                    _onConnected?.Invoke();

                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    currentBackoff = initialBackoff;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                        {
                            _onDisconnected?.Invoke("Remote feed closed the connection.");
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
                    _onError?.Invoke(ex);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _onReconnectScheduled?.Invoke(currentBackoff);
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
