using Ares.Domain;

namespace Ares.Policy.Services;

public enum PolicyDecision
{
    Permit,
    Deny,
    RequireApproval
}

public class PolicyResult
{
    public PolicyDecision Decision { get; set; }
    public string ReasonCode { get; set; } = "";
    public string[] Explanations { get; set; } = Array.Empty<string>();
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

public interface IPolicyEngine
{
    Task<PolicyResult> EvaluateAsync(Engagement engagement, string testCategory, string moduleId, string moduleVersion, bool requiresHumanApproval, int maximumConcurrency, string rateLimits);
    Task<PolicyResult> EvaluateScopeAsync(Engagement engagement, string target);
    /// <summary>
    /// MEC §10.3 bound evaluation: when the operation requires human approval,
    /// a presented, valid, bound <see cref="Approval"/> permits it; null,
    /// expired, mismatched, or exhausted approvals keep RequireApproval/Deny.
    /// Pure function of its inputs — no hidden state — so identical inputs with
    /// the same policy version decide identically (deterministic).
    /// </summary>
    Task<PolicyResult> EvaluateWithApprovalAsync(Engagement engagement, string testCategory, string moduleId, string moduleVersion, bool requiresHumanApproval, int maximumConcurrency, string rateLimits, Approval? approval);
}

public class PolicyEngine : IPolicyEngine
{
    // MEC §10.1 check 14: global emergency stop. In-memory latch; control plane
    // sets it and every evaluation fails closed while latched.
    private static volatile bool _emergencyStopActive;
    public static void ActivateEmergencyStop() => _emergencyStopActive = true;
    public static void ClearEmergencyStop() => _emergencyStopActive = false;
    public static bool IsEmergencyStopActive => _emergencyStopActive;

    public async Task<PolicyResult> EvaluateAsync(Engagement engagement, string testCategory, string moduleId, string moduleVersion, bool requiresHumanApproval, int maximumConcurrency, string rateLimits)
    {
        await Task.Yield();
        var explanations = new List<string>();

        // 14. Global emergency-stop state is inactive (checked first: fail closed).
        if (_emergencyStopActive)
        {
            explanations.Add("Global emergency stop is active");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "EMERGENCY_STOP_ACTIVE", Explanations = explanations.ToArray() };
        }

        // 1. Engagement authorization is valid AND 2. engagement is active.
        // MEC §10.1 requires both: an engagement that is merely Authorized
        // (not yet started) must NOT receive Permit for execution.
        if (engagement.Status != "Active")
        {
            explanations.Add(engagement.Status == "Authorized"
                ? "Engagement is authorized but not active; start the engagement before execution"
                : $"Engagement is not authorized and active (status: {engagement.Status})");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "ENGAGEMENT_NOT_ACTIVE", Explanations = explanations.ToArray() };
        }

        // MEC §6.1: the assessment window is authoritative. Testing outside the
        // written authorization window is unauthorized testing, even with an
        // Active engagement and a valid approval.
        var now = DateTime.UtcNow;
        if (engagement.StartAt == default || engagement.EndAt == default)
        {
            explanations.Add("Engagement has no assessment window; authorization is incomplete");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "ENGAGEMENT_WINDOW_MISSING", Explanations = explanations.ToArray() };
        }
        if (now < engagement.StartAt || now > engagement.EndAt)
        {
            explanations.Add($"Now ({now:O}) is outside the authorized window [{engagement.StartAt:O} .. {engagement.EndAt:O}]");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "ENGAGEMENT_WINDOW_CLOSED", Explanations = explanations.ToArray() };
        }

        // 11. Execution limits have not been exceeded (fail closed on
        // misconfiguration as well as over-limit).
        if (maximumConcurrency <= 0 || maximumConcurrency > engagement.MaximumConcurrency)
        {
            explanations.Add($"Requested concurrency {maximumConcurrency} exceeds engagement limit {engagement.MaximumConcurrency}");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "CONCURRENCY_LIMIT_EXCEEDED", Explanations = explanations.ToArray() };
        }

        // 9. Human approval exists where required. The engine holds no approval
        // store yet, so an approval-gated operation can never be Permitted here:
        // it must RequireApproval and the caller must present an approval via
        // the (pending) approval service before execution. Fail closed per MEC §10.2.
        if (requiresHumanApproval)
        {
            explanations.Add("Human approval required; present a bound approval before execution");
            return new PolicyResult { Decision = PolicyDecision.RequireApproval, ReasonCode = "APPROVAL_REQUIRED", Explanations = explanations.ToArray() };
        }

        // Checks 3-8, 10, 12-13 require target identity, module registry +
        // signature store, approval store, tenant store, and worker attestation,
        // none of which exist in this milestone. They MUST NOT silently Permit.
        // Category allow-listing (check 5) is decidable from the engagement alone.
        // An EMPTY allow-list is fail-closed: it permits nothing, because the
        // alternative (permit everything) would be a silent authorization bypass.
        if (engagement.AllowedAssessmentTypes is not { Length: > 0 })
        {
            explanations.Add("Engagement declares no allowed assessment types; every category is denied (fail closed)");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "CATEGORY_NOT_PERMITTED", Explanations = explanations.ToArray() };
        }
        if (!engagement.AllowedAssessmentTypes.Contains(testCategory, StringComparer.OrdinalIgnoreCase))
        {
            explanations.Add($"Test category '{testCategory}' is not permitted for this engagement");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "CATEGORY_NOT_PERMITTED", Explanations = explanations.ToArray() };
        }

        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(moduleVersion))
        {
            explanations.Add("Module identity or version missing; module registry verification required");
            return new PolicyResult { Decision = PolicyDecision.RequireApproval, ReasonCode = "MODULE_UNVERIFIED", Explanations = explanations.ToArray() };
        }

        return new PolicyResult
        {
            Decision = PolicyDecision.Permit,
            ReasonCode = "PERMIT",
            Explanations = explanations.ToArray()
        };
    }

    public async Task<PolicyResult> EvaluateWithApprovalAsync(Engagement engagement, string testCategory, string moduleId, string moduleVersion, bool requiresHumanApproval, int maximumConcurrency, string rateLimits, Approval? approval)
    {
        await Task.Yield();
        var baseResult = await EvaluateAsync(engagement, testCategory, moduleId, moduleVersion,
            requiresHumanApproval: false, maximumConcurrency, rateLimits);
        if (baseResult.Decision == PolicyDecision.Deny)
            return baseResult; // scope, status, limits, category deny regardless of approval
        if (!requiresHumanApproval)
            return baseResult;
        if (approval is null)
            return new PolicyResult { Decision = PolicyDecision.RequireApproval, ReasonCode = "APPROVAL_REQUIRED", Explanations = new[] { "Human approval required; none presented" } };
        if (approval.Decision != "Approved")
            return new PolicyResult { Decision = PolicyDecision.RequireApproval, ReasonCode = "APPROVAL_NOT_GRANTED", Explanations = new[] { $"Approval is '{approval.Decision}', not Approved" } };
        if (DateTime.UtcNow > approval.ExpiresAt)
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "APPROVAL_EXPIRED", Explanations = new[] { "Approval has expired (fail closed)" } };
        // Binding is verified by the approval service at consumption time
        // (ValidateForExecutionAsync); the engine additionally pins the presented
        // approval to this exact operation so a swapped record cannot pass.
        if (approval.EngagementId != engagement.Id || approval.TenantId != engagement.TenantId)
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "APPROVAL_BINDING_MISMATCH", Explanations = new[] { "Approval engagement/tenant does not match" } };
        if (!string.Equals(approval.ModuleId, moduleId, StringComparison.Ordinal) ||
            !string.Equals(approval.ModuleVersion, moduleVersion, StringComparison.Ordinal))
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "APPROVAL_BINDING_MISMATCH", Explanations = new[] { "Approval module does not match requested operation" } };
        if (approval.Uses >= approval.MaxUses)
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "APPROVAL_EXHAUSTED", Explanations = new[] { "Approval use budget exhausted" } };
        return new PolicyResult { Decision = PolicyDecision.Permit, ReasonCode = "PERMIT", Explanations = new[] { $"Bound approval {approval.Id} verified for this operation" } };
    }

    public async Task<PolicyResult> EvaluateScopeAsync(Engagement engagement, string target)
    {
        await Task.Yield();
        var explanations = new List<string>();

        if (_emergencyStopActive)
        {
            explanations.Add("Global emergency stop is active");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "EMERGENCY_STOP_ACTIVE", Explanations = explanations.ToArray() };
        }

        if (string.IsNullOrWhiteSpace(engagement.ScopeDefinition))
        {
            explanations.Add("Engagement has no scope definition");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "SCOPE_DEFINITION_REQUIRED", Explanations = explanations.ToArray() };
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            explanations.Add("No target supplied");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "TARGET_REQUIRED", Explanations = explanations.ToArray() };
        }

        var normalizedTarget = NormalizeTarget(target);

        // Blueprint §4.3: special-use destinations (loopback, link-local,
        // multicast, unspecified, cloud metadata, IPv6 quirks) are denied
        // unless the scope explicitly lists that exact token.
        if (IsSpecialUseDestination(normalizedTarget) &&
            !ScopeExplicitlyListsTarget(engagement, normalizedTarget))
        {
            explanations.Add($"Target '{target}' is a special-use/reserved destination not explicitly in scope");
            return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "SPECIAL_USE_DENIED", Explanations = explanations.ToArray() };
        }

        // Check 4 first: an explicit exclusion always wins over scope (deny).
        foreach (var excluded in engagement.ExcludedTargets ?? Array.Empty<string>())
        {
            if (TargetMatches(normalizedTarget, NormalizeTarget(excluded)))
            {
                explanations.Add($"Target '{target}' matches excluded target '{excluded}'");
                return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "TARGET_EXCLUDED", Explanations = explanations.ToArray() };
            }
        }

        // Check 3: target must belong to the authorized scope. The scope
        // definition is a comma/semicolon/space separated list of hostnames or
        // CIDR ranges (IPv4 + IPv6). Exact-or-subdomain for hostnames;
        // containment for CIDR; anything else Denies (fail closed).
        var scopeEntries = engagement.ScopeDefinition
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in scopeEntries)
        {
            if (TargetMatches(normalizedTarget, NormalizeTarget(entry)))
                return new PolicyResult { Decision = PolicyDecision.Permit, ReasonCode = "PERMIT", Explanations = explanations.ToArray() };
        }

        explanations.Add($"Target '{target}' is not within the authorized scope");
        return new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "OUT_OF_SCOPE", Explanations = explanations.ToArray() };
    }

    internal static bool IsSpecialUseDestination(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.Ordinal)) return true;
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            var b = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (b[0] == 127) return true;                          // loopback 127/8
                if (b[0] == 10) return true;                            // RFC1918 10/8
                if (b[0] == 172 && b[1] is >= 16 and <= 31) return true; // RFC1918 172.16/12
                if (b[0] == 192 && b[1] == 168) return true;            // RFC1918 192.168/16
                if (b[0] == 169 && b[1] == 254) return true;            // link-local + cloud metadata
                if (b[0] == 0) return true;                             // "this network" 0/8
                if (b[0] >= 224) return true;                           // multicast 224/4 + reserved 240/4
            }
            else
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
                if (System.Net.IPAddress.IPv6Loopback.Equals(ip)) return true;
                if (System.Net.IPAddress.IPv6None.Equals(ip)) return true;
                // IPv4-mapped IPv6 carrying a special-use v4 (::ffff:127.0.0.1 etc.)
                if (ip.IsIPv4MappedToIPv6)
                {
                    var v4 = ip.MapToIPv4().GetAddressBytes();
                    if (v4[0] == 127 || v4[0] == 10 || v4[0] == 0 || v4[0] >= 224) return true;
                    if (v4[0] == 172 && v4[1] is >= 16 and <= 31) return true;
                    if (v4[0] == 192 && v4[1] == 168) return true;
                    if (v4[0] == 169 && v4[1] == 254) return true;
                }
            }
        }
        return false;
    }

    internal static bool ScopeExplicitlyListsTarget(Engagement engagement, string normalizedTarget)
    {
        var entries = engagement.ScopeDefinition
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
            if (string.Equals(NormalizeTarget(entry), normalizedTarget, StringComparison.Ordinal))
                return true;
        return false;
    }

    internal static string NormalizeTarget(string value)
    {
        var v = value.Trim().TrimEnd('.').ToLowerInvariant();
        // Strip a single scheme prefix if present (we match host/IP, not URL).
        var scheme = v.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) v = v[(scheme + 3)..];
        // Drop path/query/fragment — scope is about the destination host.
        var cut = v.IndexOfAny(new[] { '/', '?', '#' });
        if (cut >= 0) v = v[..cut];
        // Drop port (but keep IPv6 brackets intact for TryParse).
        v = StripPort(v);
        return v;
    }

    internal static string StripPort(string host)
    {
        if (host.StartsWith("[", StringComparison.Ordinal))
        {
            // [v6] or [v6]:port
            var close = host.IndexOf(']');
            if (close < 0) return host;
            var rest = host[(close + 1)..];
            var inner = host[1..close];
            if (rest.StartsWith(":", StringComparison.Ordinal) &&
                int.TryParse(rest[1..], out _))
                return inner; // [::1]:8080 -> ::1
            return inner;
        }
        // Bare host:port — but NOT a bare IPv6 literal (multiple colons).
        var colon = host.LastIndexOf(':');
        if (colon > 0 && host.IndexOf(':') == colon && int.TryParse(host[(colon + 1)..], out _))
            return host[..colon];
        return host;
    }

    internal static bool TargetMatches(string target, string pattern)
    {
        // Fail closed on anything that smells like a bypass attempt rather than
        // a plain hostname/IP: userinfo (@), alternate numeric IP encodings
        // (decimal/hex/octal quads), or unparseable host soup. Blueprint §4.3.
        if (target.Contains('@')) return false;
        if (LooksLikeEncodedIp(target)) return IPMatchesScope(target, pattern);
        if (target == pattern) return true;
        // Subdomain match: api.staging.example.test is in scope for
        // staging.example.test, but evilstaging.example.test is not.
        if (target.EndsWith("." + pattern, StringComparison.Ordinal)) return true;
        return IPMatchesScope(target, pattern);
    }

    internal static bool IPMatchesScope(string target, string pattern)
    {
        if (TryParseCidr(pattern, out var network, out var prefixLength) &&
            System.Net.IPAddress.TryParse(target, out var address))
            return IsInSubnet(address, network, prefixLength);
        // Exact IP equality (covers IPv4, IPv6, mapped forms after TryParse).
        if (System.Net.IPAddress.TryParse(target, out var t) &&
            System.Net.IPAddress.TryParse(pattern, out var p))
            return t.Equals(p);
        return false;
    }

    // True when the host token is an IP written in a non-canonical encoding
    // (pure decimal, 0x-hex parts, leading-zero octal parts). Such tokens are
    // rejected unless the scope explicitly lists that exact token.
    internal static bool LooksLikeEncodedIp(string host)
    {
        if (System.Net.IPAddress.TryParse(host, out _)) return false; // canonical — fine
        var parts = host.Split('.');
        if (parts.Length is < 1 or > 4) return false;
        var allNumeric = true;
        foreach (var part in parts)
        {
            if (part.Length == 0) return false;
            var q = part;
            if (q.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) q = q[2..];
            else if (q.Length > 1 && q.StartsWith("0")) { /* possible octal */ }
            foreach (var c in q)
                if (!char.IsDigit(c) && !(c is >= 'a' and <= 'f') && !(c is >= 'A' and <= 'F'))
                { allNumeric = false; break; }
            if (!allNumeric) break;
        }
        return allNumeric;
    }

    internal static bool TryParseCidr(string value, out System.Net.IPAddress network, out int prefixLength)
    {
        network = System.Net.IPAddress.None;
        prefixLength = 0;
        var parts = value.Split('/');
        if (parts.Length != 2) return false;
        if (!System.Net.IPAddress.TryParse(parts[0], out var baseAddress)) return false;
        if (!int.TryParse(parts[1], out prefixLength)) return false;
        var bytes = baseAddress.GetAddressBytes();
        if (prefixLength < 0 || prefixLength > bytes.Length * 8) return false;
        network = baseAddress;
        return true;
    }

    internal static bool IsInSubnet(System.Net.IPAddress address, System.Net.IPAddress network, int prefixLength)
    {
        var a = address.GetAddressBytes();
        var n = network.GetAddressBytes();
        if (a.Length != n.Length) return false;
        var fullBytes = prefixLength / 8;
        var remaining = prefixLength % 8;
        for (var i = 0; i < fullBytes; i++)
            if (a[i] != n[i]) return false;
        if (remaining > 0)
        {
            var mask = (byte)(0xFF << (8 - remaining));
            if ((a[fullBytes] & mask) != (n[fullBytes] & mask)) return false;
        }
        return true;
    }
}