# MEC Compliance Matrix (requirement → code → test → evidence)

Conventions: evidence = executed command + verbatim result on 2026-11-01,
SDK 10.0.401, net10.0, Win x64. `—` = no implementation; never mark verified
without a passing test.

## §6.2 State machines

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Engagement Draft→Authorized→Active→Paused→Active→Completed, invalid rejected | `Ares.Engagements/EngagementService.cs` (locked) | `EngagementLifecycleTests` (4) | PASS |
| Execution Proposed→AwaitingApproval→Approved→Queued→[Leased]→Running→Verifying→Completed + terminal states | `Ares.Execution/Services/ExecutionService.cs` | `ExecutionApprovalBindingTests` (4) + `DurableQueueTests` (6) | PASS |
| API exposes transitions with correct codes | `Ares.Api/Program.cs` | API tests (3, over HTTP) | PASS |

## §10 Deterministic policy (§10.1 14 checks, §10.2 decisions, §10.3 binding)

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| 1+2 active engagement required | `PolicyEngine.EvaluateAsync` | `EvaluateAsync_authorized_but_not_active_must_not_permit` | PASS |
| 3+4 scope + exclusion (incl. adversarial §4.3) | `PolicyEngine.EvaluateScopeAsync` + matcher | 3 P0 + 13 P1 scope tests | PASS |
| 5 category allow-list | `EvaluateAsync` | via P0/P1 policy tests | PASS |
| 9 approval exists where required; §10.3 exact binding, expiry, single-use | `ApprovalService` + `EvaluateWithApprovalAsync` + store-validated grant | 5 P1 approval + grant tests | PASS |
| 11 concurrency limit check | `EvaluateAsync` | policy tests (limit branch) | PASS |
| 14 emergency stop fails closed | `EmergencyStop` + latch in policy + queue/lease/start guards | `EmergencyStopTests` (3) | PASS |
| 6,7,8,10,12,13 (signatures, versions, capabilities, racy expiry, tenant store, attestation) | — | — | NOT MET (fail closed by RequireApproval/Deny; PLANNED) |

## §11.3 Execution controls (idempotency, lease, cancel, retry, dead-letter)

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Idempotent propose | `ExecutionService.ProposeAsync(key)` | 2 idempotency tests | PASS |
| Lease + heartbeat + reclaim | `LeaseAsync/HeartbeatAsync/ReclaimExpiredLeasesAsync` | 2 lease tests | PASS |
| Cancel propagates, audited, terminal-safe | `CancelAsync(by,reason)` + `EmergencyStop` gate | cancel tests (unit + API double-cancel) | PASS |
| Bounded retry + dead-letter | `RecordAttemptFailureAsync` | dead-letter test | PASS |

## §13 / blueprint §6 Evidence

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| SHA-256 at ingest, re-verifiable | `EvidenceService.IngestAsync/VerifyAsync` | P4 ingest/verify tests | PASS |
| Hash chain + tamper detection | `RecordHash/PreviousHash/Sequence`, `VerifyChainAsync`, `CorruptForTest` | chain + tamper tests | PASS |
| Tenant isolation | tenant checks on get/verify/chain/export | cross-tenant tests | PASS |
| Export manifest + independent verify | `ExportManifestAsync` + `VerifyManifest` | manifest tests | PASS |
| Intelligence wiring (no fabricated tenant) | `IntelligenceService.GenerateEvidenceAsync(engagement,…)` | 3 P5 tests | PASS |
| Immutable storage, encryption, signed envelopes, retention lock | — | — | NOT MET (KD-09) |

## §§15–16 API

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Engagements + executions + approvals over HTTP with correct codes | `Program.cs` (DI singletons + routes) | 3 API tests | PASS |
| Tenants/policy/evidence/findings/reports routes, authN/Z, pagination, audit events | — | — | NOT MET (KD-06) |

## §13 Verification (ladder + independence)

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Validated ladder, no jumps, terminals terminal | `Ares.Verification/Services/VerificationService.cs` | 5 `FindingVerificationTests` | PASS |
| Verified requires evidence + passed independent check; confidence never advances | same | full-path + high-confidence tests | PASS |
| Tenant-bound evidence attach | same (store-enforced) | cross-tenant test | PASS |
| Live network re-test, Inconclusive/Remediated/AcceptedRisk flows, HTTP routes | — | — | NOT MET (P8 continued) |

## Phases 03/05/06/10/11/12 + §11.1/11.2 workers, OPA, persistence, benchmarks

No implementation beyond in-memory milestones above. Status PLANNED/PARTIALLY_IMPLEMENTED per CAPABILITY_CATALOG.md. The matrix stays red here on purpose — green only with executed evidence.
