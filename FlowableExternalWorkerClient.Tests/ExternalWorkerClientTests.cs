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

    private ExternalWorkerAcquireJobResponse ConsumeOneCall(
        ExternalWorkerClient client,
        Func<ExternalWorkerAcquireJobResponse, IWorkResultBuilder, IWorkResult?> callbackHandler,
        string topic = "myTopic")
    {
        ExternalWorkerAcquireJobResponse? resultJob = null;
        var completed = false;

        var handler = new DelegateCallbackHandler((job, work) =>
        {
            var result = callbackHandler(job, work);
            resultJob = job;
            completed = true;
            return result;
        });

        var subscription = client.Subscribe(topic, handler, waitPeriodSeconds: 1);

        var attempts = 0;
        while (attempts < 50 && !completed)
        {
            Thread.Sleep(200);
            attempts++;
        }

        subscription.Unsubscribe();
        Assert.True(completed, "Callback was not invoked within the timeout period");
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
            ConsumeOneCall(client, (job, work) => work.Success());

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
            ConsumeOneCall(client, (job, work) =>
                work.Failure()
                    .ErrorMessage("Test Error Message")
                    .ErrorDetails("Some error details")
                    .Retries(3)
                    .RetryTimeout("PT5M")
            );

            // Give the server a moment to process
            if (!cassette.IsReplaying) await Task.Delay(500);

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
            ConsumeOneCall(
                client,
                (job, work) => work.CmmnTerminate().Variable("testVar", "test terminate"),
                topic: "cmmnTopic"
            );

            // Give the server a moment to process
            if (!cassette.IsReplaying) await Task.Delay(500);

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
            ConsumeOneCall(client, (job, work) =>
                work.BpmnError()
                    .Variable("testVar", "test failure")
                    .ErrorCode("errorCode1")
            );

            // Give the server a moment to process
            if (!cassette.IsReplaying) await Task.Delay(500);

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
}
