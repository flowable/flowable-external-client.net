# Flowable External Worker Library for .NET


[License:
![license](https://img.shields.io/hexpm/l/plug.svg)](https://github.com/flowable/flowable-external-client.net/blob/main/LICENSE)

An _External Worker Task_ in BPMN or CMMN is a task where the custom logic of that task is executed externally to Flowable, i.e. on another server.
When the process or case engine arrives at such a task, it will create an **external job**, which is exposed over the REST API.
Through this REST API, the job can be acquired and locked.
Once locked, the custom logic is responsible for signalling over REST that the work is done and the process or case can continue.

This project makes implementing such custom logic in .NET easy by not having the worry about the low-level details of the REST API and focus on the actual custom business logic.
Integrations for other languages are available, too.

This project is still a work-in-progress client, expect that the API will change.
Currently, it's a basic wrapper for the REST API's without any further services.

## Sample

### Cloud

```csharp
var auth = new AuthenticationHeaderValue("Bearer", "<personal-access-token>");
var flowableExternalWorkerRestClient = new FlowableExternalWorkerRestClient("https://cloud.flowable.com/work", "test-worker", auth);

var acquireJobs = flowableExternalWorkerRestClient.AcquireJobs("testTopic", "PT5M").Result;

if (acquireJobs.Count > 0)
{
    var jobId = acquireJobs[0].Id;
    flowableExternalWorkerRestClient.CompleteJob(jobId);
}
```