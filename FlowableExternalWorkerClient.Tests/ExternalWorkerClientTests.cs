using System.Net.Http.Headers;
using System.Text;
using FlowableExternalWorkerClient.Client;
using FlowableExternalWorkerClient.Rest;
using Xunit;

namespace FlowableExternalWorkerClient.Tests;

public class ExternalWorkerClientTests
{
    private const string BaseUrl = "http://localhost:8090/flowable-work";

    private static readonly AuthenticationHeaderValue Auth =
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:test")));

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Subscribes to a topic, waits for a single job to be processed (including the
    /// work result's Execute call), and returns the job. Uses an ExecutionAwareWorkResult
    /// wrapper to properly await the subscription's async work, avoiding static delays.
    /// </summary>
    private async Task<ExternalWorkerAcquireJobResponse> ConsumeOneCallAsync(
        ExternalWorkerClient client,
        Func<ExternalWorkerAcquireJobResponse, IWorkResultBuilder, IWorkResult?> callbackHandler,
        string topic = "myTopic")
    {
        ExternalWorkerAcquireJobResponse? resultJob = null;
        var executionCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new DelegateCallbackHandler((job, work) =>
        {
            var result = callbackHandler(job, work);
            resultJob = job;
            if (result == null)
            {
                executionCompleted.TrySetResult(true);
                return null;
            }
            return new ExecutionAwareWorkResult(result, executionCompleted);
        });

        var subscription = client.Subscribe(topic, handler, waitPeriodSeconds: 1);

        try
        {
            await executionCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            subscription.Unsubscribe();
            Assert.Fail("Callback was not invoked or execution did not complete within the timeout period");
        }

        subscription.Unsubscribe();
        return resultJob!;
    }

    [Fact]
    public async Task TestConsume()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new ExternalWorkerClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            await ConsumeOneCallAsync(client, (job, work) => work.Success());

            var activityIds = await BpmnUtils.ExecutedActivityIds(httpClient, BaseUrl, processInstanceId);
            Assert.Equal(
                new List<string>
                {
                    "bpmnEndEvent_1", "bpmnSequenceFlow_2", "bpmnSequenceFlow_4", "bpmnTask_3", "startnoneevent1"
                },
                activityIds);
        }
        finally
        {
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestConsumeWithFailure()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new ExternalWorkerClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            await ConsumeOneCallAsync(client, (job, work) =>
                work.Failure()
                    .ErrorMessage("Test Error Message")
                    .ErrorDetails("Some error details")
                    .Retries(3)
                    .RetryTimeout("PT5M")
            );

            var activityIds = await BpmnUtils.ExecutedActivityIds(httpClient, BaseUrl, processInstanceId);
            Assert.Equal(
                new List<string> { "bpmnSequenceFlow_2", "bpmnTask_3", "startnoneevent1" },
                activityIds);
        }
        finally
        {
            await BpmnUtils.TerminateProcess(httpClient, BaseUrl, processInstanceId);
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestConsumeWithCmmnTerminate()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new ExternalWorkerClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await CmmnUtils.DeployCase(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Cases", "externalWorkerCase.cmmn"));
        var definitionId = await CmmnUtils.GetCaseDefinitionId(httpClient, BaseUrl, deploymentId);
        var caseInstanceId = await CmmnUtils.StartCase(httpClient, BaseUrl, definitionId);
        try
        {
            await ConsumeOneCallAsync(
                client,
                (job, work) => work.CmmnTerminate().Variable("testVar", "test terminate"),
                topic: "cmmnTopic"
            );

            var variable = await CmmnUtils.GetCaseVariable(httpClient, BaseUrl, caseInstanceId, "testVar");
            Assert.NotNull(variable);
            Assert.Equal("string", variable.Value.GetProperty("type").GetString());
            Assert.Equal("test terminate", variable.Value.GetProperty("value").GetString());
        }
        finally
        {
            await CmmnUtils.DeleteCaseDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestSubscribeWithBpmnError()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new ExternalWorkerClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "withBoundaryEvent.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            await ConsumeOneCallAsync(client, (job, work) =>
                work.BpmnError()
                    .Variable("testVar", "test failure")
                    .ErrorCode("errorCode1")
            );

            var variable =
                await BpmnUtils.GetProcessVariable(httpClient, BaseUrl, processInstanceId, "testVar");
            Assert.NotNull(variable);
            Assert.Equal("string", variable.Value.GetProperty("type").GetString());
            Assert.Equal("test failure", variable.Value.GetProperty("value").GetString());

            var activityIds = await BpmnUtils.ExecutedActivityIds(httpClient, BaseUrl, processInstanceId);
            Assert.Equal(
                new List<string>
                {
                    "bpmnBoundaryEvent_3", "bpmnBoundaryEvent_4", "bpmnEndEvent_5", "bpmnSequenceFlow_2",
                    "bpmnSequenceFlow_6", "bpmnTask_3", "startnoneevent1"
                },
                activityIds);
        }
        finally
        {
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    private class DelegateCallbackHandler : IExternalWorkerCallbackHandler
    {
        private readonly Func<ExternalWorkerAcquireJobResponse, IWorkResultBuilder, IWorkResult?> _handler;

        public DelegateCallbackHandler(
            Func<ExternalWorkerAcquireJobResponse, IWorkResultBuilder, IWorkResult?> handler)
        {
            _handler = handler;
        }

        public IWorkResult? Handle(ExternalWorkerAcquireJobResponse job, IWorkResultBuilder work)
        {
            return _handler(job, work);
        }
    }

    /// <summary>
    /// Wraps an IWorkResult to signal a TaskCompletionSource when Execute completes.
    /// This lets ConsumeOneCallAsync wait for the subscription's async work to finish
    /// instead of using a static delay.
    /// </summary>
    private class ExecutionAwareWorkResult : IWorkResult
    {
        private readonly IWorkResult _inner;
        private readonly TaskCompletionSource<bool> _tcs;

        public ExecutionAwareWorkResult(IWorkResult inner, TaskCompletionSource<bool> tcs)
        {
            _inner = inner;
            _tcs = tcs;
        }

        public async Task Execute(IFlowableExternalWorkerRestClient client)
        {
            try
            {
                await _inner.Execute(client);
                _tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                throw;
            }
        }
    }
}
