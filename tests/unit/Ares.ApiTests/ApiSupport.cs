// Shared HTTP helpers for the API tests. The flow mirrors what an operator does
// with curl: create -> authorize -> start -> propose -> request approval ->
// request approval record -> approve -> grant -> queue -> lease -> start.
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ares.ApiTests;

internal static class ApiFlow
{
    public const string Target = "staging.example.test";
    public const string Category = "safe_http_configuration_checks";
    public const string ModuleId = "http.security.headers";
    public const string ModuleVersion = "1.2.0";

    public static async Task<Guid> CreateActiveEngagementAsync(HttpClient client, string name = "api-e2e")
    {
        var created = await client.PostAsJsonAsync("/api/v1/engagements", new
        {
            name,
            description = "api flow",
            authorizationReference = "AR-API-1",
            startAt = DateTime.UtcNow.AddMinutes(-5),
            endAt = DateTime.UtcNow.AddHours(2),
            scopeDefinition = Target,
            allowedAssessmentTypes = new[] { Category },
            excludedTargets = Array.Empty<string>(),
            maximumConcurrency = 5
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var engagement = await created.Content.ReadFromJsonAsync<IdBody>();
        Assert.NotNull(engagement);

        foreach (var action in new[] { "authorize", "start" })
        {
            var transition = await client.PostAsync($"/api/v1/engagements/{engagement!.Id}/{action}", null);
            Assert.True(transition.IsSuccessStatusCode,
                $"{action} failed with {(int)transition.StatusCode}: {await transition.Content.ReadAsStringAsync()}");
        }
        return engagement.Id;
    }

    public static async Task<Guid> ProposeAsync(HttpClient client, Guid engagementId,
        string category = Category, string target = Target, string? idempotencyKey = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/executions", new
        {
            engagementId,
            hypothesisId = Guid.NewGuid(),
            target,
            testCategory = category,
            moduleId = ModuleId,
            moduleVersion = ModuleVersion,
            idempotencyKey
        });
        Assert.True(response.IsSuccessStatusCode,
            $"propose failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<ExecutionBody>();
        return body!.Id;
    }

    /// <summary>Proposed -> AwaitingApproval, then creates the bound approval record.</summary>
    public static async Task<(Guid ExecutionId, Guid ApprovalId)> AwaitingApprovalAsync(
        HttpClient requesterClient, Guid engagementId)
    {
        var executionId = await ProposeAsync(requesterClient, engagementId);

        var awaiting = await requesterClient.PostAsync($"/api/v1/executions/{executionId}/request-approval", null);
        Assert.True(awaiting.IsSuccessStatusCode,
            $"request-approval failed with {(int)awaiting.StatusCode}: {await awaiting.Content.ReadAsStringAsync()}");

        var request = await requesterClient.PostAsJsonAsync(
            $"/api/v1/executions/{executionId}/approval-requests",
            new { ttlMinutes = 30, maxUses = 1, justification = "api test" });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var approval = await request.Content.ReadFromJsonAsync<IdBody>();
        return (executionId, approval!.Id);
    }

    public static async Task<Guid> ApproveAsync(HttpClient approverClient, Guid approvalId)
    {
        var response = await approverClient.PostAsJsonAsync($"/api/v1/approvals/{approvalId}/approve",
            new { reason = "reviewed" });
        Assert.True(response.IsSuccessStatusCode,
            $"approve failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return approvalId;
    }

    public static async Task<ExecutionBody> GrantAsync(HttpClient client, Guid executionId, Guid approvalId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/executions/{executionId}/grant",
            new { approvalId });
        Assert.True(response.IsSuccessStatusCode,
            $"grant failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<ExecutionBody>())!;
    }

    public static async Task<ExecutionBody> PostExecutionActionAsync(HttpClient client,
        Guid executionId, string action, object? body = null)
    {
        var response = body is null
            ? await client.PostAsync($"/api/v1/executions/{executionId}/{action}", null)
            : await client.PostAsJsonAsync($"/api/v1/executions/{executionId}/{action}", body);
        Assert.True(response.IsSuccessStatusCode,
            $"{action} failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<ExecutionBody>())!;
    }
}

public sealed record IdBody(Guid Id);

public sealed record ExecutionBody(Guid Id, string Status, string? PolicyReasonCode);

public sealed record ProblemBody(string? Type, string? Title, int? Status, string? Detail, string? ReasonCode);
