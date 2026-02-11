using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FlowableExternalWorkerClient.Tests;

public static class BpmnUtils
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<string> DeployProcess(HttpClient client, string baseUrl, string filePath)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await client.PostAsync(baseUrl + "/process-api/repository/deployments", content);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        return json.GetProperty("id").GetString()!;
    }

    public static async Task DeleteDeployment(HttpClient client, string baseUrl, string deploymentId)
    {
        var response = await client.DeleteAsync(
            baseUrl + "/process-api/repository/deployments/" + deploymentId);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<string> GetProcessDefinitionId(HttpClient client, string baseUrl, string deploymentId)
    {
        var response = await client.GetAsync(
            baseUrl + "/process-api/repository/process-definitions?deploymentId=" + deploymentId);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        var data = json.GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        return data[0].GetProperty("id").GetString()!;
    }

    public static async Task<string> StartProcess(HttpClient client, string baseUrl, string processDefinitionId)
    {
        var requestBody = new StringContent(
            JsonSerializer.Serialize(new { processDefinitionId }, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(
            baseUrl + "/process-api/runtime/process-instances", requestBody);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        return json.GetProperty("id").GetString()!;
    }

    public static async Task TerminateProcess(HttpClient client, string baseUrl, string processInstanceId)
    {
        var response = await client.DeleteAsync(
            baseUrl + "/process-api/runtime/process-instances/" + processInstanceId);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<JsonElement?> GetProcessVariable(HttpClient client, string baseUrl,
        string processInstanceId, string variableName)
    {
        var response = await client.GetAsync(
            baseUrl + "/process-api/history/historic-variable-instances?processInstanceId=" +
            processInstanceId + "&variableName=" + variableName);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        var data = json.GetProperty("data");
        if (data.GetArrayLength() == 1)
        {
            return data[0].GetProperty("variable");
        }

        return null;
    }

    public static async Task<List<string>> ExecutedActivityIds(HttpClient client, string baseUrl,
        string processInstanceId)
    {
        var response = await client.GetAsync(
            baseUrl + "/process-api/history/historic-activity-instances?processInstanceId=" + processInstanceId);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        var data = json.GetProperty("data");
        var activityIds = new List<string>();
        foreach (var item in data.EnumerateArray())
        {
            activityIds.Add(item.GetProperty("activityId").GetString()!);
        }

        activityIds.Sort();
        return activityIds;
    }
}
