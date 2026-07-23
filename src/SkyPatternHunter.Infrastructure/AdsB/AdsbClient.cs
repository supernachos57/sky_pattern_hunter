using System.Net.Sockets;
using System.Text;

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

    public async Task<IReadOnlyList<string>> ReadMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, cancellationToken);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lines = new List<string>();
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines.Add(line);
        }

        return lines;
    }
}
