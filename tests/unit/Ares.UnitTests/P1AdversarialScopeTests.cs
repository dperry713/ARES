// P1 adversarial scope + tenant-isolation tests (blueprint §4.3, MEC §10.1).
// These target the CURRENT PolicyEngine scope matcher which does pure string
// matching: it must be extended to reject destination-validation bypasses
// (alternate IP encodings, loopback/link-local/cloud-metadata, URL smuggling)
// and to keep tenants strictly separate. Written fail-first: RED now, GREEN
// after the matcher is hardened — never by weakening a test.
using Ares.Domain;
using Ares.Engagements;
using Ares.Policy.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class AdversarialScopeTests
{
    private static Engagement Active(string scope, string[]? excluded = null) => new Engagement
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Name = "adv",
        ScopeDefinition = scope,
        AllowedAssessmentTypes = new[] { "safe_http_configuration_checks" },
        Status = "Active",
        ExcludedTargets = excluded ?? Array.Empty<string>()
    };

    // Alternate numeric IP encodings must not bypass scope (fail closed on
    // anything the matcher cannot canonicalize to the authorized address).
    [Theory]
    [InlineData("http://2130706433/", "127.0.0.1")]       // decimal for 127.0.0.1
    [InlineData("http://0x7f.0x0.0x0.0x1/", "127.0.0.1")] // hex quads
    [InlineData("http://2130706433.evil.test/", "127.0.0.1")]
    public async Task Alternate_ip_encodings_never_match_scope(string target, string _)
    {
        var engine = new PolicyEngine();
        var eng = Active("staging.example.test");
        var result = await engine.EvaluateScopeAsync(eng, target);
        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    // Reserved / special-use destinations are denied unless explicitly in scope.
    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")] // cloud metadata
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://localhost:8080/admin")]
    [InlineData("http://[::1]/admin")]
    public async Task Special_use_destinations_denied_when_not_in_scope(string target)
    {
        var engine = new PolicyEngine();
        var eng = Active("staging.example.test");
        var result = await engine.EvaluateScopeAsync(eng, target);
        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    // Redirect/escape: userinfo, embedded credentials, or @-smuggling must not
    // turn an out-of-scope host into an in-scope one.
    [Theory]
    [InlineData("http://staging.example.test@evil.test/")]
    [InlineData("http://user:pass@evil.test/")]
    [InlineData("https://staging.example.test.evil.test/")]
    public async Task Credential_or_suffix_smuggling_does_not_enter_scope(string target)
    {
        var engine = new PolicyEngine();
        var eng = Active("staging.example.test");
        var result = await engine.EvaluateScopeAsync(eng, target);
        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public async Task Cross_tenant_scope_must_not_leak()
    {
        // Two tenants, same hostname text but different engagement scopes:
        // tenant B's narrower scope must deny what tenant A's wider scope permits.
        var engine = new PolicyEngine();
        var engA = Active("example.test");
        var engB = Active("other.test");
        var a = await engine.EvaluateScopeAsync(engA, "staging.example.test");
        var b = await engine.EvaluateScopeAsync(engB, "staging.example.test");
        Assert.Equal(PolicyDecision.Permit, a.Decision);
        Assert.Equal(PolicyDecision.Deny, b.Decision);
    }

    [Fact]
    public async Task Explicit_special_use_token_in_scope_is_honored()
    {
        // Escape hatch with informed consent: a lab that explicitly lists
        // 127.0.0.1 CAN test it (documented, auditable), but nothing else opens.
        var engine = new PolicyEngine();
        var eng = Active("127.0.0.1");
        var allowed = await engine.EvaluateScopeAsync(eng, "http://127.0.0.1:8080/health");
        Assert.Equal(PolicyDecision.Permit, allowed.Decision);
        var stillDenied = await engine.EvaluateScopeAsync(eng, "http://169.254.169.254/");
        Assert.Equal(PolicyDecision.Deny, stillDenied.Decision);
    }

    [Fact]
    public async Task Trailing_dot_and_case_are_canonicalized_not_bypassed()
    {
        var engine = new PolicyEngine();
        var eng = Active("staging.example.test");
        foreach (var variant in new[] { "STAGING.EXAMPLE.TEST", "staging.example.test.", "Staging.Example.Test." })
        {
            var result = await engine.EvaluateScopeAsync(eng, variant);
            Assert.Equal(PolicyDecision.Permit, result.Decision);
        }
    }
}
