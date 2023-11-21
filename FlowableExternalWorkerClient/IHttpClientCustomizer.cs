namespace FlowableExternalWorkerClient;

public interface IHttpClientCustomizer
{
    void Customize(HttpClient client);
}