namespace FlowableExternalWorkerClient;

public class EngineRestVariable
{
    public string Name { get; }
    public string Type { get; }
    public string? Value { get; }
    public string? ValueUrl { get; }

    public EngineRestVariable(string name, string type, string? value, string? valueUrl = null)
    {
        Name = name;
        Type = type;
        Value = value;
        ValueUrl = valueUrl;
    }
}