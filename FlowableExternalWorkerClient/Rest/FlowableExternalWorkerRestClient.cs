using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FlowableExternalWorkerClient.Rest;

public class FlowableExternalWorkerRestClient : IFlowableExternalWorkerRestClient
{
    private const string _jobApi = "/external-job-api";

    private readonly HttpClient _httpClient;
    private readonly string _flowableHost;
    private readonly string _workerId;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true
    };

    public FlowableExternalWorkerRestClient(
        string flowableHost,
        string workerId,
        AuthenticationHeaderValue? authentication = null,
        IHttpClientCustomizer? customizer = null
    )
    {
        _httpClient = new HttpClient();
        if (authentication != null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = authentication;
        }

        if (customizer != null)
        {
            customizer.Customize(_httpClient);
        }

        _flowableHost = flowableHost;
        _workerId = workerId;
    }

    public async Task<Page<ExternalWorkerJobResponse>> ListJobs()
    {
        var response = await _httpClient.GetStreamAsync(_flowableHost + _jobApi + "/jobs");
        return await JsonSerializer.DeserializeAsync<Page<ExternalWorkerJobResponse>>(response, _jsonSerializerOptions)
               ?? throw new Exception("Failed to convert response to JSON structure");
    }

    public async Task<ExternalWorkerJobResponse> GetJob(string jobId)
    {
        var response = await _httpClient.GetStreamAsync(_flowableHost + _jobApi + "/jobs/" + jobId);
        return await JsonSerializer.DeserializeAsync<ExternalWorkerJobResponse>(response, _jsonSerializerOptions)
               ?? throw new Exception("Failed to convert response to JSON structure");
    }

    public async Task<List<ExternalWorkerAcquireJobResponse>> AcquireJobs(string topic, string lockDuration,
        int numberOfTasks = 1, int numberOfRetries = 5,
        string? workerId = null, string? scopeType = null)
    {
        var acquireJobRequest = new AcquireJobRequest(
            topic,
            lockDuration,
            numberOfTasks,
            numberOfRetries,
            workerId ?? _workerId,
            scopeType
        );
        var requestBody = PrepareRequestBody(acquireJobRequest);

        var response = await _httpClient.PostAsync(_flowableHost + _jobApi + "/acquire/jobs", requestBody);
        await VerifyStatusCode(response, HttpStatusCode.OK);

        var responseStream = await response.Content.ReadAsStreamAsync();

        return await JsonSerializer.DeserializeAsync<List<ExternalWorkerAcquireJobResponse>>(
            responseStream, _jsonSerializerOptions
        ) ?? throw new Exception("Failed to convert response to JSON structure");
    }

    public async Task CompleteJob(string jobId, List<EngineRestVariable>? variables = null, string? workerId = null)
    {
        var completeJobRequest = new CompleteJobRequest(
            workerId ?? _workerId,
            variables
        );

        var requestBody = PrepareRequestBody(completeJobRequest);
        var response = await _httpClient.PostAsync(
            _flowableHost + _jobApi + "/acquire/jobs/" + jobId + "/complete",
            requestBody
        );

        await VerifyStatusCode(response, HttpStatusCode.NoContent);
    }

    public async Task JobWithBpmnError(string jobId, List<EngineRestVariable>? variables = null,
        string? errorCode = null,
        string? workerId = null)
    {
        var jobWithBpmnErrorRequest = new JobWithBpmnErrorRequest(
            workerId ?? _workerId,
            errorCode,
            variables
        );

        var requestBody = PrepareRequestBody(jobWithBpmnErrorRequest);
        var response = await _httpClient.PostAsync(
            _flowableHost + _jobApi + "/acquire/jobs/" + jobId + "/bpmnError",
            requestBody
        );

        await VerifyStatusCode(response, HttpStatusCode.NoContent);
    }

    public async Task JobWithCmmnTerminate(string jobId, List<EngineRestVariable>? variables = null, string? workerId = null)
    {
        var cmmnWithTerminateRequest = new CmmnWithTerminateRequest(
            workerId ?? _workerId,
            variables
        );
        
        var requestBody = PrepareRequestBody(cmmnWithTerminateRequest);
        var response = await _httpClient.PostAsync(
            _flowableHost + _jobApi + "/acquire/jobs/" + jobId + "/cmmnTerminate",
            requestBody
        );

        await VerifyStatusCode(response, HttpStatusCode.NoContent);
    }

    public async Task FailJob(string jobId, string? errorMessage = null, string? errorDetails = null,
        int? retries = null,
        string? retryTimeout = null, string? workerId = null)
    {
        var failJobRequest = new FailJobRequest(
            workerId ?? _workerId,
            errorMessage,
            errorDetails,
            retries,
            retryTimeout
        );

        var requestBody = PrepareRequestBody(failJobRequest);
        var response = await _httpClient.PostAsync(
            _flowableHost + _jobApi + "/acquire/jobs/" + jobId + "/fail",
            requestBody
        );

        await VerifyStatusCode(response, HttpStatusCode.NoContent);
    }

    protected async Task VerifyStatusCode(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        if (response.StatusCode != expectedStatusCode)
        {
            var body = Encoding.Default.GetString(await response.Content.ReadAsByteArrayAsync());
            throw new Exception(
                "Failed to execute request with status code '" + response.StatusCode + "' and body: '" + body + "'"
            );
        }
    }

    protected StringContent PrepareRequestBody<T>(T requestObject)
    {
        var serializedAcquireJob = JsonSerializer.Serialize(requestObject, _jsonSerializerOptions);
        var stringContent = new StringContent(serializedAcquireJob, Encoding.UTF8, "application/json");
        return stringContent;
    }
}

class AcquireJobRequest
{
    public string Topic;
    public string LockDuration;
    public int NumberOfTasks;
    public int NumberOfRetries;
    public string WorkerId;
    public string? ScopeType;

    public AcquireJobRequest(string topic, string lockDuration, int numberOfTasks, int numberOfRetries, string workerId,
        string? scopeType)
    {
        Topic = topic;
        LockDuration = lockDuration;
        NumberOfTasks = numberOfTasks;
        NumberOfRetries = numberOfRetries;
        WorkerId = workerId;
        ScopeType = scopeType;
    }
}

class CompleteJobRequest
{
    public string WorkerId;
    public List<EngineRestVariable>? Variables;

    public CompleteJobRequest(string workerId, List<EngineRestVariable>? variables)
    {
        WorkerId = workerId;
        Variables = variables;
    }
}

class JobWithBpmnErrorRequest
{
    public string WorkerId;
    public string? ErrorCode;
    public List<EngineRestVariable>? Variables;

    public JobWithBpmnErrorRequest(string workerId, string? errorCode, List<EngineRestVariable>? variables)
    {
        WorkerId = workerId;
        ErrorCode = errorCode;
        Variables = variables;
    }
}

class CmmnWithTerminateRequest
{
    public string WorkerId;
    public List<EngineRestVariable>? Variables;

    public CmmnWithTerminateRequest(string workerId, List<EngineRestVariable>? variables)
    {
        WorkerId = workerId;
        Variables = variables;
    }
}

class FailJobRequest
{
    public string WorkerId;
    public string? ErrorMessage;
    public string? ErrorDetails;
    public int? Retries;
    public string? RetryTimeout;

    public FailJobRequest(string workerId, string? errorMessage, string? errorDetails, int? retries,
        string? retryTimeout)
    {
        WorkerId = workerId;
        ErrorMessage = errorMessage;
        ErrorDetails = errorDetails;
        Retries = retries;
        RetryTimeout = retryTimeout;
    }
}