using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using ShoppingLab.Core;

namespace ShoppingLab.Web;

/// <summary>
/// Fetches public URLs for the "check your catalog" audit. Every connection (including redirects) is checked after DNS
/// resolution, so private, loopback, link-local and other non-public addresses cannot be reached. Only ports 80/443.
/// </summary>
public sealed class PublicFetcher
{
    public const string UserAgent = "SparkdataReadinessAudit/1.0 (+https://examples.sparkdata.pl/ai-shopping-readiness-lab/)";
    private const int MaxBytes = 2 * 1024 * 1024;
    private readonly HttpClient client;

    public PublicFetcher()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 4,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseProxy = false,
            ConnectCallback = async (context, ct) =>
            {
                var port = context.DnsEndPoint.Port;
                if (port is not (80 or 443)) throw new HttpRequestException("Only ports 80 and 443 are allowed.");
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                var target = addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault(IsPublic) ?? throw new HttpRequestException("Address is not public.");
                if (addresses.Any(a => !IsPublic(a))) throw new HttpRequestException("Address is not public.");
                var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(target, port), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/*;q=0.8,*/*;q=0.5");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl,en;q=0.8");
    }

    public static bool IsAllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.HostNameType is UriHostNameType.Dns
        && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
        && !uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
        && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
                || (b[0] == 198 && b[1] is 18 or 19)
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                || (b[0] == 203 && b[1] == 0 && b[2] == 113));
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.Equals(IPAddress.IPv6None)) return false;
            var b = address.GetAddressBytes();
            if ((b[0] & 0xfe) == 0xfc) return false; // unique local fc00::/7
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false; // documentation
            if (b[0] == 0x00) return false; // ::/8, including the NAT64 prefix 64:ff9b::/96
            return (b[0] & 0xe0) == 0x20; // global unicast 2000::/3
        }
        return false;
    }

    public async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
    {
        if (!IsAllowedUrl(url)) return new FetchResult(0, null, "");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.RequestMessage?.RequestUri is { } final && !IsAllowedUrl(final.ToString())) return new FetchResult(0, null, "");
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new FetchResult((int)response.StatusCode, contentType, "");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[MaxBytes];
            var total = 0;
            int read;
            while (total < MaxBytes && (read = await stream.ReadAsync(buffer.AsMemory(total, MaxBytes - total), ct)) > 0)
            {
                total += read;
            }
            var encoding = Encoding.UTF8;
            try
            {
                if (response.Content.Headers.ContentType?.CharSet is { Length: > 0 } charset) encoding = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
            }
            return new FetchResult((int)response.StatusCode, contentType, encoding.GetString(buffer, 0, total));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or SocketException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            return new FetchResult(0, null, "");
        }
    }
}
