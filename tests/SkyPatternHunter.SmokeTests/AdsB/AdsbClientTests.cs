using System.Net;
using System.Net.Sockets;
using System.Text;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.SmokeTests.AdsB;

public class AdsbClientTests
{
    [Fact]
    public async Task ReadMessagesAsync_ReconnectsAndContinuesAfterDisconnect()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var firstMessage = "{\"hex\":\"A1B2C3\"}";
        var secondMessage = "{\"hex\":\"D4E5F6\"}";

        try
        {
            var serverTask = RunServerAsync(listener, firstMessage, secondMessage);
            var connectAttemptCount = 0;
            var connectedCount = 0;
            var reconnectScheduledCount = 0;
            var errorCount = 0;

            var client = new AdsbClient(
                IPAddress.Loopback.ToString(),
                endpoint.Port,
                onConnectAttempt: () => connectAttemptCount++,
                onConnected: () => connectedCount++,
                onError: _ => errorCount++,
                onReconnectScheduled: _ => reconnectScheduledCount++);
            var receivedMessages = new List<string>();

            try
            {
                await foreach (var message in client.ReadMessagesAsync(cancellationTokenSource.Token))
                {
                    receivedMessages.Add(message);

                    if (receivedMessages.Count == 2)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
            {
            }

            await serverTask;

            Assert.Equal(new[] { firstMessage, secondMessage }, receivedMessages);
            Assert.True(connectAttemptCount >= 2);
            Assert.True(connectedCount >= 2);
            Assert.True(reconnectScheduledCount >= 1);
            Assert.Equal(0, errorCount);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RunServerAsync(TcpListener listener, string firstMessage, string secondMessage)
    {
        await SendOneMessageThenCloseAsync(listener, firstMessage);
        await SendOneMessageThenCloseAsync(listener, secondMessage);
    }

    private static async Task SendOneMessageThenCloseAsync(TcpListener listener, string message)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        await writer.WriteLineAsync(message);
    }
}