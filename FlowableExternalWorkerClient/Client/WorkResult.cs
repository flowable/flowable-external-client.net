using FlowableExternalWorkerClient.Rest;

namespace FlowableExternalWorkerClient.Client;

public interface IWorkResult
{
    Task Execute(IFlowableExternalWorkerRestClient flowableExternalWorkerRestClient);
}

public class WorkResultSuccess : WorkResultWithVariables<WorkResultSuccess>
{
    protected readonly string _jobId;

    public WorkResultSuccess(ExternalWorkerAcquireJobResponse job)
    {
        _jobId = job.Id;
    }

    public override Task Execute(IFlowableExternalWorkerRestClient flowableExternalWorkerRestClient)
    {
        return flowableExternalWorkerRestClient.CompleteJob(_jobId, _variables);
    }

    protected override WorkResultSuccess GetThis()
    {
        return this;
    }
}

public class WorkResultFailure : IWorkResult
{
    protected readonly string _jobId;
    protected string? _errorMessage;
    protected string? _errorDetails;
    protected int? _retries;
    protected string? _retryTimeout;

    public WorkResultFailure(ExternalWorkerAcquireJobResponse job)
    {
        _jobId = job.Id;
    }

    WorkResultFailure ErrorMessage(string errorMessage)
    {
        _errorMessage = errorMessage;
        return this;
    }

    WorkResultFailure ErrorDetails(string errorDetails)
    {
        _errorDetails = errorDetails;
        return this;
    }

    WorkResultFailure Retries(int retries)
    {
        _retries = retries;
        return this;
    }

    WorkResultFailure RetryTimeout(string retryTimeout)
    {
        _retryTimeout = retryTimeout;
        return this;
    }

    public Task Execute(IFlowableExternalWorkerRestClient flowableExternalWorkerRestClient)
    {
        return flowableExternalWorkerRestClient.FailJob(_jobId, _errorMessage, _errorDetails, _retries, _retryTimeout);
    }
}

public class WorkResultBpmnError : WorkResultWithVariables<WorkResultBpmnError>
{
    protected readonly string _jobId;
    protected string? _errorCode;

    public WorkResultBpmnError(ExternalWorkerAcquireJobResponse job)
    {
        _jobId = job.Id;
    }

    public WorkResultBpmnError ErrorCode(string errorCode)
    {
        _errorCode = errorCode;
        return this;
    }

    protected override WorkResultBpmnError GetThis()
    {
        return this;
    }

    public override Task Execute(IFlowableExternalWorkerRestClient flowableExternalWorkerRestClient)
    {
        return flowableExternalWorkerRestClient.JobWithBpmnError(_jobId, _variables, _errorCode);
    }
}

public class WorkResultCmmnTerminate : WorkResultWithVariables<WorkResultCmmnTerminate>
{
    private string _jobId;

    public WorkResultCmmnTerminate(ExternalWorkerAcquireJobResponse job)
    {
        _jobId = job.Id;
    }

    protected override WorkResultCmmnTerminate GetThis()
    {
        return this;
    }

    public override Task Execute(IFlowableExternalWorkerRestClient flowableExternalWorkerRestClient)
    {
        return flowableExternalWorkerRestClient.JobWithCmmnTerminate(_jobId, _variables);
    }
}

public abstract class WorkResultWithVariables<T> : IWorkResult where T : WorkResultWithVariables<T>
{

    protected List<EngineRestVariable> _variables = new();

    protected abstract T GetThis();
    
    public abstract Task Execute(IFlowableExternalWorkerRestClient flowableExternalWorkerRestClient);

    public T Variable(string name, string value)
    {
        _variables.Add(new EngineRestVariable(name, "string", value));
        return GetThis();
    }

    public T Variable(string name, int value)
    {
        _variables.Add(new EngineRestVariable(name, "integer", value));
        return GetThis();
    }

    public T Variable(string name, float value)
    {
        _variables.Add(new EngineRestVariable(name, "double", value));
        return GetThis();
    }

    public T Variable(string name, double value)
    {
        _variables.Add(new EngineRestVariable(name, "double", value));
        return GetThis();
    }

    public T Variable(string name, bool value)
    {
        _variables.Add(new EngineRestVariable(name, "boolean", value));
        return GetThis();
    }

    public T Variable(string name, object value)
    {
        _variables.Add(new EngineRestVariable(name, "json", value));
        return GetThis();
    }

}