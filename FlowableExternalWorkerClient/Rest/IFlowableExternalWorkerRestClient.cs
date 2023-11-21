namespace FlowableExternalWorkerClient.Rest;

public interface IFlowableExternalWorkerRestClient
{
    Task<Page<ExternalWorkerJobResponse>> ListJobs();

    Task<ExternalWorkerJobResponse> GetJob(string jobId);

    Task<List<ExternalWorkerAcquireJobResponse>> AcquireJobs(
        string topic,
        string lockDuration,
        int numberOfTasks = 1,
        int numberOfRetries = 5,
        string? workerId = null,
        string? scopeType = null
    );

    Task CompleteJob(string jobId, List<EngineRestVariable>? variables = null, string? workerId = null);

    Task JobWithBpmnError(
        string jobId,
        List<EngineRestVariable>? variables = null,
        string? errorCode = null,
        string? workerId = null
    );

    Task JobWithCmmnTerminate(
        string jobId,
        List<EngineRestVariable>? variables = null,
        string? workerId = null
    );

    Task FailJob(
        string jobId,
        string? errorMessage = null,
        string? errorDetails = null,
        int? retries = null,
        string? retryTimeout = null,
        string? workerId = null
    );
}