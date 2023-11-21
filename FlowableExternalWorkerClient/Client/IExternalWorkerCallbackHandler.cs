using FlowableExternalWorkerClient.Rest;

namespace FlowableExternalWorkerClient.Client;

public interface IExternalWorkerCallbackHandler
{
    IWorkResult? Handle(ExternalWorkerAcquireJobResponse job, IWorkResultBuilder work);
}