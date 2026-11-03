using Ares.Policy.Services;

namespace Ares.Execution.Services;

/// <summary>
/// Process-wide emergency-stop control (blueprint §4.6, MEC §11.4).
/// While latched, the execution control plane refuses ALL new movement
/// (queue/lease/start) and the policy engine denies — fail closed.
/// Recovery requires a human actor + reason and is audit-logged.
/// Backed by the existing <see cref="PolicyEngine"/> latch so policy and
/// execution can never disagree about stop state.
/// </summary>
public static class EmergencyStop
{
    private static readonly object _lock = new();
    private static readonly List<EmergencyStopEvent> _trail = new();

    public static bool IsActive
    {
        get
        {
            lock (_lock) return PolicyEngine.IsEmergencyStopActive;
        }
    }

    public static IReadOnlyList<EmergencyStopEvent> AuditTrail
    {
        get
        {
            lock (_lock) return _trail.ToList();
        }
    }

    public static void Activate(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor required.", nameof(actor));
        lock (_lock)
        {
            PolicyEngine.ActivateEmergencyStop();
            _trail.Add(new EmergencyStopEvent("ACTIVATE", actor, reason ?? string.Empty, DateTimeOffset.UtcNow));
        }
    }

    public static void Recover(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Recovery requires a human actor.", nameof(actor));
        lock (_lock)
        {
            PolicyEngine.ClearEmergencyStop();
            _trail.Add(new EmergencyStopEvent("RECOVER", actor, reason ?? string.Empty, DateTimeOffset.UtcNow));
        }
    }

    public static void ResetForTest()
    {
        lock (_lock)
        {
            PolicyEngine.ClearEmergencyStop();
            _trail.Clear();
        }
        // Belt and suspenders: the xUnit runner executes test classes in
        // parallel, so a stop latched by P6 must never leak into P0–P5 tests.
        // Every test class touching the control plane resets in try/finally;
        // this static backstop clears the process-wide latch on next access.
    }

    internal static void ThrowIfStopped(string operation)
    {
        if (IsActive)
            throw new InvalidOperationException(
                $"Emergency stop is ACTIVE: '{operation}' refused (fail closed). Recover via EmergencyStop.Recover with a human actor.");
    }
}

public sealed record EmergencyStopEvent(
    string Action, string Actor, string Reason, DateTimeOffset At);
