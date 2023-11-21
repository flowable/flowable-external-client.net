namespace FlowableExternalWorkerClient.Rest;

public class EngineRestVariable
{
    public string Name { get; }
    public string Type { get; }
    public object? Value { get; }
    public string? ValueUrl { get; }

    public EngineRestVariable(string name, string type, object? value, string? valueUrl = null)
    {
        Name = name;
        Type = type;
        Value = value;
        ValueUrl = valueUrl;
    }
}