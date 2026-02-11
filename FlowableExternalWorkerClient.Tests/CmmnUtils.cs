using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FlowableExternalWorkerClient.Tests;

public static class CmmnUtils
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<string> DeployCase(HttpClient client, string baseUrl, string filePath)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await client.PostAsync(baseUrl + "/cmmn-api/cmmn-repository/deployments", content);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        return json.GetProperty("id").GetString()!;
    }

    public static async Task DeleteCaseDeployment(HttpClient client, string baseUrl, string deploymentId)
    {
        var response = await client.DeleteAsync(
            baseUrl + "/cmmn-api/cmmn-repository/deployments/" + deploymentId);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<string> GetCaseDefinitionId(HttpClient client, string baseUrl, string deploymentId)
    {
        var response = await client.GetAsync(
            baseUrl + "/cmmn-api/cmmn-repository/case-definitions?deploymentId=" + deploymentId);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        var data = json.GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        return data[0].GetProperty("id").GetString()!;
    }

    public static async Task<string> StartCase(HttpClient client, string baseUrl, string caseDefinitionId)
    {
        var requestBody = new StringContent(
            JsonSerializer.Serialize(new { caseDefinitionId }, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(
            baseUrl + "/cmmn-api/cmmn-runtime/case-instances", requestBody);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        return json.GetProperty("id").GetString()!;
    }

    public static async Task TerminateCase(HttpClient client, string baseUrl, string caseInstanceId)
    {
        var response = await client.DeleteAsync(
            baseUrl + "/cmmn-api/cmmn-runtime/case-instances/" + caseInstanceId);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<JsonElement?> GetCaseVariable(HttpClient client, string baseUrl,
        string caseInstanceId, string variableName)
    {
        var response = await client.GetAsync(
            baseUrl + "/cmmn-api/cmmn-history/historic-variable-instances?caseInstanceId=" +
            caseInstanceId + "&variableName=" + variableName);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
        var data = json.GetProperty("data");
        if (data.GetArrayLength() == 1)
        {
            return data[0].GetProperty("variable");
        }

        return null;
    }
}
