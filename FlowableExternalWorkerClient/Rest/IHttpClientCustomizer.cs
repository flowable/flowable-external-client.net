namespace FlowableExternalWorkerClient.Rest;

public interface IHttpClientCustomizer
{
    void Customize(HttpClient client);
}