using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FlowableExternalWorkerClient.Tests;

/// <summary>
/// A DelegatingHandler that records HTTP interactions to a JSON cassette file
/// and replays them in order on subsequent runs.
///
/// Mode: Auto - if cassette file exists, replays; otherwise records.
/// Replay is sequential (ordered), not matching-based, to correctly handle
/// repeated calls to the same endpoint with different responses.
/// </summary>
public class CassetteHandler : DelegatingHandler
{
    private readonly string _cassettePath;
    private readonly List<RecordedInteraction> _interactions;
    private int _replayIndex;
    private readonly bool _isReplaying;

    public CassetteHandler(string cassettePath)
        : base(new HttpClientHandler())
    {
        _cassettePath = cassettePath;
        if (File.Exists(cassettePath))
        {
            var json = File.ReadAllText(cassettePath);
            _interactions = JsonSerializer.Deserialize<List<RecordedInteraction>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? new();
            _isReplaying = true;
        }
        else
        {
            _interactions = new();
            _isReplaying = false;
        }
    }

    public bool IsReplaying => _isReplaying;

    public HttpClient CreateHttpClient()
    {
        return new HttpClient(this, disposeHandler: false);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_isReplaying)
        {
            return Replay(request);
        }

        return await Record(request, cancellationToken);
    }

    private HttpResponseMessage Replay(HttpRequestMessage request)
    {
        int index;
        lock (_interactions)
        {
            index = _replayIndex++;
        }

        if (index >= _interactions.Count)
        {
            throw new InvalidOperationException(
                $"Cassette '{Path.GetFileName(_cassettePath)}' has no more recorded interactions " +
                $"(tried index {index}, total {_interactions.Count}). " +
                $"Delete the cassette file to re-record.");
        }

        return _interactions[index].ToHttpResponseMessage();
    }

    private async Task<HttpResponseMessage> Record(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Buffer the response body so both recording and caller can use it
        var bodyBytes = response.Content != null
            ? await response.Content.ReadAsByteArrayAsync(cancellationToken)
            : Array.Empty<byte>();

        var interaction = new RecordedInteraction
        {
            Method = request.Method.Method,
            Uri = request.RequestUri?.ToString() ?? "",
            StatusCode = (int)response.StatusCode,
            ResponseBody = Convert.ToBase64String(bodyBytes),
            ResponseContentType = response.Content?.Headers.ContentType?.ToString()
        };

        foreach (var header in response.Headers)
        {
            interaction.ResponseHeaders[header.Key] = header.Value.ToArray();
        }

        lock (_interactions)
        {
            _interactions.Add(interaction);
        }

        // Return a new response with buffered body so the caller can read it
        var newResponse = new HttpResponseMessage(response.StatusCode);
        newResponse.Content = new ByteArrayContent(bodyBytes);
        if (response.Content?.Headers.ContentType != null)
        {
            newResponse.Content.Headers.ContentType = response.Content.Headers.ContentType;
        }

        foreach (var header in response.Headers)
        {
            newResponse.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        newResponse.RequestMessage = request;
        return newResponse;
    }

    public void Save()
    {
        if (!_isReplaying)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cassettePath)!);
            var json = JsonSerializer.Serialize(_interactions, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(_cassettePath, json);
        }
    }
}

public class RecordedInteraction
{
    public string Method { get; set; } = "";
    public string Uri { get; set; } = "";
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseContentType { get; set; }
    public Dictionary<string, string[]> ResponseHeaders { get; set; } = new();

    public HttpResponseMessage ToHttpResponseMessage()
    {
        var response = new HttpResponseMessage((HttpStatusCode)StatusCode);

        if (ResponseBody != null)
        {
            var bytes = Convert.FromBase64String(ResponseBody);
            response.Content = new ByteArrayContent(bytes);
            if (ResponseContentType != null)
            {
                response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ResponseContentType);
            }
        }

        foreach (var header in ResponseHeaders)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }
}
