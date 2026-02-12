using System.Net.Http.Headers;
using System.Text;
using FlowableExternalWorkerClient.Rest;
using Xunit;

namespace FlowableExternalWorkerClient.Tests;

public class RestClientTests
{
    private const string BaseUrl = "http://localhost:8090/flowable-work";

    private static readonly AuthenticationHeaderValue Auth =
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:test")));

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public async Task TestListJobs()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var jobs = await client.ListJobs();
            Assert.True(jobs.Total >= 1);
            var entry = jobs.Data.First(j => j.ProcessInstanceId == processInstanceId);
            Assert.Equal("External Worker task", entry.ElementName);
            Assert.Equal(3, entry.Retries);
            Assert.True(entry.CreateTime < DateTime.Now);
        }
        finally
        {
            await BpmnUtils.TerminateProcess(httpClient, BaseUrl, processInstanceId);
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestListJobsWithCustomizer()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var client = new FlowableExternalWorkerRestClient(
                BaseUrl, "test-worker", httpClient, customizer: new BasicAuthCustomizer());

            var jobs = await client.ListJobs();
            Assert.True(jobs.Total >= 1);
        }
        finally
        {
            await BpmnUtils.TerminateProcess(httpClient, BaseUrl, processInstanceId);
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestListJobsInvalidAuth()
    {
        using var cassette = Vcr.UseCassette();
        var validClient = cassette.CreateHttpClient();
        validClient.DefaultRequestHeaders.Authorization = Auth;

        var invalidClient = cassette.CreateHttpClient();
        invalidClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid:auth")));

        var deploymentId = await BpmnUtils.DeployProcess(
            validClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(validClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(validClient, BaseUrl, definitionId);
        try
        {
            var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", invalidClient);
            await Assert.ThrowsAnyAsync<Exception>(() => client.ListJobs());
        }
        finally
        {
            await BpmnUtils.TerminateProcess(validClient, BaseUrl, processInstanceId);
            await BpmnUtils.DeleteDeployment(validClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestGetJob()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var jobs = await client.ListJobs();
            Assert.True(jobs.Total >= 1);
            var entry = jobs.Data.First(j => j.ProcessInstanceId == processInstanceId);
            Assert.Equal("External Worker task", entry.ElementName);
            Assert.Equal(3, entry.Retries);
            Assert.True(entry.CreateTime < DateTime.Now);

            var job = await client.GetJob(entry.Id);
            Assert.Equal("External Worker task", job.ElementName);
            Assert.Equal(3, job.Retries);
            Assert.True(job.CreateTime < DateTime.Now);
        }
        finally
        {
            await BpmnUtils.TerminateProcess(httpClient, BaseUrl, processInstanceId);
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestAcquireJobs()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("myTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);
            Assert.True(firstEntry.CreateTime < DateTime.Now);
            Assert.True(firstEntry.Variables.Count >= 1);

            var initiatorVar = firstEntry.Variables.Find(v => v.Name == "initiator");
            Assert.NotNull(initiatorVar);
            Assert.Equal("admin", initiatorVar.Value?.ToString());
        }
        finally
        {
            await BpmnUtils.TerminateProcess(httpClient, BaseUrl, processInstanceId);
            await BpmnUtils.DeleteDeployment(httpClient, BaseUrl, deploymentId);
            cassette.Save();
        }
    }

    [Fact]
    public async Task TestCompleteJob()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("myTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);

            var variables = new List<EngineRestVariable>
            {
                new("testVar", "string", "test content")
            };

            await client.CompleteJob(firstEntry.Id, variables);

            var variable = await BpmnUtils.GetProcessVariable(httpClient, BaseUrl, processInstanceId, "testVar");
            Assert.NotNull(variable);
            Assert.Equal("string", variable.Value.GetProperty("type").GetString());
            Assert.Equal("test content", variable.Value.GetProperty("value").GetString());

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
    public async Task TestJobWithBpmnErrorWithoutErrorCode()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "withBoundaryEvent.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("myTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);

            var variables = new List<EngineRestVariable>
            {
                new("testVar", "string", "test failure")
            };

            await client.JobWithBpmnError(firstEntry.Id, variables);

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

    [Fact]
    public async Task TestJobWithBpmnErrorWithErrorCode1()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "withBoundaryEvent.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("myTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);

            var variables = new List<EngineRestVariable>
            {
                new("testVar", "string", "test failure")
            };

            await client.JobWithBpmnError(firstEntry.Id, variables, errorCode: "errorCode1");

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

    [Fact]
    public async Task TestJobWithBpmnErrorWithErrorCode2()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "withBoundaryEvent.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("myTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);

            var variables = new List<EngineRestVariable>
            {
                new("testVar", "string", "test failure")
            };

            await client.JobWithBpmnError(firstEntry.Id, variables, errorCode: "errorCode2");

            var variable =
                await BpmnUtils.GetProcessVariable(httpClient, BaseUrl, processInstanceId, "testVar");
            Assert.NotNull(variable);
            Assert.Equal("string", variable.Value.GetProperty("type").GetString());
            Assert.Equal("test failure", variable.Value.GetProperty("value").GetString());

            var activityIds = await BpmnUtils.ExecutedActivityIds(httpClient, BaseUrl, processInstanceId);
            Assert.Equal(
                new List<string>
                {
                    "bpmnBoundaryEvent_3", "bpmnBoundaryEvent_4", "bpmnEndEvent_7", "bpmnSequenceFlow_2",
                    "bpmnSequenceFlow_8", "bpmnTask_3", "startnoneevent1"
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
    public async Task TestFailJob()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await BpmnUtils.DeployProcess(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Processes", "externalWorkerProcess.bpmn"));
        var definitionId = await BpmnUtils.GetProcessDefinitionId(httpClient, BaseUrl, deploymentId);
        var processInstanceId = await BpmnUtils.StartProcess(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("myTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);
            Assert.Equal(3, firstEntry.Retries);

            await client.FailJob(firstEntry.Id);

            var jobAfterFail = await client.GetJob(firstEntry.Id);
            Assert.Equal(2, jobAfterFail.Retries);

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
    public async Task TestJobWithCmmnTerminate()
    {
        using var cassette = Vcr.UseCassette();
        var httpClient = cassette.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = Auth;
        var client = new FlowableExternalWorkerRestClient(BaseUrl, "test-worker", httpClient);

        var deploymentId = await CmmnUtils.DeployCase(
            httpClient, BaseUrl, Path.Combine(FixturesPath, "Cases", "externalWorkerCase.cmmn"));
        var definitionId = await CmmnUtils.GetCaseDefinitionId(httpClient, BaseUrl, deploymentId);
        var caseInstanceId = await CmmnUtils.StartCase(httpClient, BaseUrl, definitionId);
        try
        {
            var acquiredJobs = await client.AcquireJobs("cmmnTopic", "PT10S");
            Assert.Equal(1, acquiredJobs.Count);

            var firstEntry = acquiredJobs[0];
            Assert.Equal("External Worker task", firstEntry.ElementName);

            var variables = new List<EngineRestVariable>
            {
                new("testVar", "string", "test terminate")
            };

            await client.JobWithCmmnTerminate(firstEntry.Id, variables);

            var variable =
                await CmmnUtils.GetCaseVariable(httpClient, BaseUrl, caseInstanceId, "testVar");
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

    private class BasicAuthCustomizer : IHttpClientCustomizer
    {
        public void Customize(HttpClient client)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:test")));
        }
    }
}
