# Flowable External Worker Library for .NET


[License:
![license](https://img.shields.io/hexpm/l/plug.svg)](https://github.com/flowable/flowable-external-client.net/blob/main/LICENSE)

![Flowable Actions CI](https://github.com/flowable/flowable-external-client.net/actions/workflows/main.yml/badge.svg?branch=main)

An _External Worker Task_ in BPMN or CMMN is a task where the custom logic of that task is executed externally to Flowable, i.e. on another server.
When the process or case engine arrives at such a task, it will create an **external job**, which is exposed over the REST API.
Through this REST API, the job can be acquired and locked.
Once locked, the custom logic is responsible for signalling over REST that the work is done and the process or case can continue.

This project makes implementing such custom logic in .NET easy by not having the worry about the low-level details of the REST API and focus on the actual custom business logic.
Integrations for other languages are available, too.

This project is still a work-in-progress client, expect that the API will change.

## Authentication

This library uses the [AuthenticationHeaderValue](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.authenticationheadervalue.-ctor?view=net-9.0) class to provide the authentication.
This allows to provide a `Bearer` token as well as `Basic` authentication by providing the correct parameters.

## Installation

To install the external worker library, execute the following command:
```
dotnet add package Flowable.ExternalWorkerClient
```

## Sample

### Cloud

The usage with Flowable Cloud is simpler, since everything is pre-configured.
However, it's required to either use the user credentials or to pre-configure a personal access token.

```csharp
var auth = new AuthenticationHeaderValue("Bearer", "<personal-access-token>");

var externalWorkerClient = new ExternalWorkerClient(authentication: auth);
var subscription = externalWorkerClient.Subscribe("myTopic", new HandleJobs());

// ...

class HandleJobs : IExternalWorkerCallbackHandler
{
    public IWorkResult Handle(ExternalWorkerAcquireJobResponse job, IWorkResultBuilder work)
    {
        Console.WriteLine("Handle job: " + job.Id);
        return work
            .Success()
            .Variable("testVar", "Hello from C#");
    }
}
```