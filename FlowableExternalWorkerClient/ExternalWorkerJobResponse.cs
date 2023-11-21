namespace FlowableExternalWorkerClient;

public class ExternalWorkerJobResponse
{
    public string Id { get; }

    public string Url { get; }

    public string CorrelationId { get; }

    public string? ProcessInstanceId { get; }

    public string? ExecutionId { get; }

    public string? ScopeId { get; }

    public string? SubScopeId { get; }

    public string? ScopeDefinitionId { get; }

    public string? ScopeType { get; }

    public string ElementId { get; }

    public string ElementName { get; }

    public int Retries { get; }

    public string? ExceptionMessage { get; }

    public DateTime? DueDate { get; }

    public DateTime CreateTime { get; }

    public string TenantId { get; }

    public string? LockOwner { get; }

    public DateTime? LockExpirationTime { get; }

    public ExternalWorkerJobResponse(string id, string url, string correlationId, string? processInstanceId,
        string? executionId, string? scopeId, string? subScopeId, string? scopeDefinitionId, string? scopeType,
        string elementId, string elementName, int retries, string? exceptionMessage, DateTime? dueDate,
        DateTime createTime, string tenantId, string? lockOwner, DateTime? lockExpirationTime)
    {
        Id = id;
        Url = url;
        CorrelationId = correlationId;
        ProcessInstanceId = processInstanceId;
        ExecutionId = executionId;
        ScopeId = scopeId;
        SubScopeId = subScopeId;
        ScopeDefinitionId = scopeDefinitionId;
        ScopeType = scopeType;
        ElementId = elementId;
        ElementName = elementName;
        Retries = retries;
        ExceptionMessage = exceptionMessage;
        DueDate = dueDate;
        CreateTime = createTime;
        TenantId = tenantId;
        LockOwner = lockOwner;
        LockExpirationTime = lockExpirationTime;
    }

    public override string ToString()
    {
        return $"{nameof(Id)}: {Id}";
    }
}