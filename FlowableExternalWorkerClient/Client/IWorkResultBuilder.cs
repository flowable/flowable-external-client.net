namespace FlowableExternalWorkerClient.Client;

public interface IWorkResultBuilder
{
    WorkResultSuccess Success();

    WorkResultFailure Failure();

    WorkResultBpmnError BpmnError();

    WorkResultCmmnTerminate CmmnTerminate();
}