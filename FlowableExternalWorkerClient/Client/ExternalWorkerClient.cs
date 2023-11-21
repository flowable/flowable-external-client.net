using System.Diagnostics;
using System.Net.Http.Headers;
using FlowableExternalWorkerClient.Rest;

namespace FlowableExternalWorkerClient.Client;

public class ExternalWorkerClient : IExternalWorkerClient
{
    private FlowableExternalWorkerRestClient _flowableExternalWorkerRestClient;

    public ExternalWorkerClient(
        string flowableHost = "https://cloud.flowable.com/work",
        string? workerId = null,
        AuthenticationHeaderValue? authentication = null,
        IHttpClientCustomizer? customizer = null
    )
    {
        _flowableExternalWorkerRestClient = new FlowableExternalWorkerRestClient(
            flowableHost,
            workerId ?? GetDefaultWorkerId(),
            authentication,
            customizer
        );
    }

    public IExternalWorkerSubscription Subscribe(
        string topic,
        IExternalWorkerCallbackHandler callbackHandler,
        string lockDuration = "PT1M",
        int numberOfRetries = 5,
        string? scopeType = null,
        int waitPeriodSeconds = 30,
        int numberOfTasks = 1
    )
    {
        var externalWorkerSubscription = new ExternalWorkerSubscription(
            _flowableExternalWorkerRestClient,
            topic,
            callbackHandler,
            lockDuration,
            numberOfRetries,
            scopeType,
            waitPeriodSeconds,
            numberOfTasks
        );
        externalWorkerSubscription.Start();
        return externalWorkerSubscription;
    }

    protected string GetDefaultWorkerId()
    {
        return System.Net.Dns.GetHostName() + "-" + Process.GetCurrentProcess().Id;
    }
}

class ExternalWorkerSubscription : IExternalWorkerSubscription
{
    protected bool Subscribed = true;
    protected readonly Thread Thread;
    protected readonly FlowableExternalWorkerRestClient FlowableExternalWorkerRestClient;
    protected readonly string Topic;
    protected readonly IExternalWorkerCallbackHandler ExternalWorkerCallbackHandler;
    protected readonly string LockDuration;
    protected readonly int NumberOfRetries;
    protected readonly string? ScopeType;
    protected int WaitPeriodSeconds;
    protected readonly int NumberOfTasks;


    public ExternalWorkerSubscription(
        FlowableExternalWorkerRestClient flowableExternalWorkerRestClient,
        string topic,
        IExternalWorkerCallbackHandler callbackHandler,
        string lockDuration,
        int numberOfRetries,
        string? scopeType,
        int waitPeriodSeconds,
        int numberOfTasks
    )
    {
        Thread = new Thread(DoConsume);
        FlowableExternalWorkerRestClient = flowableExternalWorkerRestClient;
        Topic = topic;
        ExternalWorkerCallbackHandler = callbackHandler;
        LockDuration = lockDuration;
        NumberOfRetries = numberOfRetries;
        ScopeType = scopeType;
        WaitPeriodSeconds = waitPeriodSeconds;
        NumberOfTasks = numberOfTasks;
    }

    public async void DoConsume()
    {
        do
        {
            var acquiredJobs = await FlowableExternalWorkerRestClient.AcquireJobs(
                Topic, LockDuration, NumberOfTasks, NumberOfRetries, null, ScopeType
            );
            foreach (var job in acquiredJobs)
            {
                var workResult = ExternalWorkerCallbackHandler.Handle(job, new WorkResultBuilder(job));
                if (workResult == null)
                {
                    await FlowableExternalWorkerRestClient.CompleteJob(job.Id);
                }
                else
                {
                    await workResult.Execute(FlowableExternalWorkerRestClient);
                }
            }

            // Only wait in case we did not process jobs, otherwise continue directly
            if (acquiredJobs.Count == 0)
            {
                Thread.Sleep(WaitPeriodSeconds * 1000);
            }
        } while (Subscribed);
    }

    public void Start()
    {
        Thread.Start();
    }

    public void Unsubscribe()
    {
        Subscribed = false;
        Thread.Join();
    }
}

class WorkResultBuilder : IWorkResultBuilder
{
    protected readonly ExternalWorkerAcquireJobResponse Job;

    public WorkResultBuilder(ExternalWorkerAcquireJobResponse job)
    {
        Job = job;
    }

    public WorkResultSuccess Success()
    {
        return new WorkResultSuccess(Job);
    }

    public WorkResultFailure Failure()
    {
        return new WorkResultFailure(Job);
    }

    public WorkResultBpmnError BpmnError()
    {
        return new WorkResultBpmnError(Job);
    }

    public WorkResultCmmnTerminate CmmnTerminate()
    {
        return new WorkResultCmmnTerminate(Job);
    }
}