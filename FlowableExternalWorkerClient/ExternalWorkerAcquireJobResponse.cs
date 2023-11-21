namespace FlowableExternalWorkerClient;

public class ExternalWorkerAcquireJobResponse : ExternalWorkerJobResponse
{
    public List<EngineRestVariable> Variables { get; }

    public ExternalWorkerAcquireJobResponse(string id, string url, string correlationId, string? processInstanceId,
        string? executionId, string? scopeId, string? subScopeId, string? scopeDefinitionId, string? scopeType,
        string elementId, string elementName, int retries, string? exceptionMessage, DateTime? dueDate,
        DateTime createTime, string tenantId, string? lockOwner, DateTime? lockExpirationTime,
        List<EngineRestVariable> variables) : base(id, url, correlationId, processInstanceId, executionId, scopeId,
        subScopeId, scopeDefinitionId, scopeType, elementId, elementName, retries, exceptionMessage, dueDate,
        createTime, tenantId, lockOwner, lockExpirationTime)
    {
        Variables = variables;
    }
}