namespace FlowableExternalWorkerClient.Client;

public interface IExternalWorkerClient
{
    IExternalWorkerSubscription Subscribe(
        string topic,
        IExternalWorkerCallbackHandler callbackHandler,
        string lockDuration = "PT1M",
        int numberOfRetries = 5,
        string? scopeType = null,
        int waitPeriodSeconds = 30,
        int numberOfTasks = 1
    );
    
}