using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ZhongHong.BridgeService.Exceptions;

namespace ZhongHong.BridgeService.Services.Implementations;

public class HttpPointNineClient : IHttpPointNineClient
{
    private readonly ILogger<HttpPointNineClient> _logger;

    private const string Http0dot9Headers = "GET <<<URI>>> HTTP/0.9\r\n" +
                                            "Host: <<<HOST>>>\r\n" +
                                            "Authorization: Basic <<<AUTH>>>\r\n" +
                                            "User-Agent: suck-0.9\r\n" +
                                            "Accept: */*\r\n\r\n";

    public HttpPointNineClient(ILogger<HttpPointNineClient> logger)
    {
        _logger = logger;
    }
    
    public async Task<string> GetAsync(Uri uri, string auth)
    {
        string header = Http0dot9Headers
            .Replace("<<<URI>>>", uri.PathAndQuery)
            .Replace("<<<HOST>>>", uri.Host)
            .Replace("<<<AUTH>>>", auth);
        try
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(uri.Host, 80);
            await using NetworkStream netStream = client.GetStream();
            await netStream.WriteAsync(Encoding.UTF8.GetBytes(header));
            byte[] receiveBuffer = new byte[1024];
            StringBuilder responseBuilder = new StringBuilder();

            do
            {
                var bytesReceived = await netStream.ReadAsync(receiveBuffer);
                string data = Encoding.UTF8.GetString(receiveBuffer.AsSpan(0, bytesReceived));
                responseBuilder.Append(data);
            } while (netStream.DataAvailable);

            var response = responseBuilder.ToString();
            if (response.StartsWith("HTTP/")) // this is an HTTP status message
            {
                var lines = response.Split("\r\n");
                throw new ZhongHongException($"HTTP error: {lines.First()}");
            }

            return response;
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            throw new ZhongHongException("Network error", ex);
        }
    }

    public async Task<T> GetJsonAsync<T>(Uri uri, string auth)
    {
        string content = await GetAsync(uri, auth);

        try
        {
            var json = JsonSerializer.Deserialize<T>(content);
            return json ?? throw new ZhongHongException("Json failure. Bug?");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new ZhongHongException("Invalid server response", ex);
        }
    }
}