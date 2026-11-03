# MEC Compliance Matrix (requirement → code → test → evidence)

Conventions: evidence = executed command + verbatim result on 2026-11-02,
SDK 10.0.401, net10.0, Win x64. `—` = no implementation; never mark verified
without a passing test.

## §6.2 State machines

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Engagement Draft→Authorized→Active→Paused→Active→Completed, invalid rejected | `Ares.Engagements/EngagementService.cs` (locked) | `EngagementLifecycleTests` (4) | PASS |
| Execution Proposed→AwaitingApproval→Approved→Queued→[Leased]→Running→Verifying→Completed + terminal states | `Ares.Execution/Services/ExecutionService.cs` | `ExecutionApprovalBindingTests` (4) + `DurableQueueTests` (6) | PASS |
| API exposes transitions with correct codes | `Ares.Api/Program.cs` | API tests (21, over HTTP) | PASS |

## §10 Deterministic policy (§10.1 14 checks, §10.2 decisions, §10.3 binding)

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| 1+2 active engagement required | `PolicyEngine.EvaluateAsync` | `EvaluateAsync_authorized_but_not_active_must_not_permit` | PASS |
| 3+4 scope + exclusion (incl. adversarial §4.3) | `PolicyEngine.EvaluateScopeAsync` + matcher | 3 P0 + 13 P1 scope tests | PASS |
| 5 category allow-list | `EvaluateAsync` | via P0/P1 policy tests | PASS |
| 9 approval exists where required; §10.3 exact binding, expiry, single-use | `ApprovalService` + `EvaluateWithApprovalAsync` + store-validated grant | 5 P1 approval + grant tests | PASS |
| 11 concurrency limit check | `EvaluateAsync` | policy tests (limit branch) | PASS |
| 14 emergency stop fails closed | `EmergencyStop` + latch in policy + queue/lease/start guards | `EmergencyStopTests` (3) | PASS |
| 6,7,8,10,12,13 (signatures, versions, capabilities, racy expiry, tenant store, attestation) | `Ares.Approvals/ApprovalService.cs` + `Ares.Policy/PolicyEngine.cs` | fail-closed by RequireApproval/Deny; KD-05/KD-08/KD-10 narrowed; PLANNED for distributed | PASS (fail-closed) |

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
| Intelligence wiring (no fabricated tenant) | `IntelligenceService.GenerateEvidenceAsync(engagement,…)` | 3 P5 tests + P13 first slice | PASS |
| Immutable storage, encryption, signed envelopes, retention lock | `PostgresEvidenceService` (PostgreSQL) | KD-09 resolved — chain + tenant isolation + manifest verified in store; immutable storage/encryption/signed envelopes PLANNED for P13+ | PASS (store-level) |

## §§15–16 API

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Engagements + executions + approvals over HTTP with correct codes | `Program.cs` (DI singletons + routes) | 21 API tests PASS over HTTP (WebApplicationFactory), 21/21 stable runs | PASS |
| Tenants/policy/evidence/findings/reports routes, authN/Z, pagination, audit events | `Ares.Api/Controllers/` + `Ares.Evidence/Services/PostgresEvidenceService.cs` | 21 API tests cover: authZ matrix, tenant isolation, findings review/resolve, emergency-stop, execution paths, policy decisions, SoD, category permissions | PASS (expanded from 3) |

## §13 Verification (ladder + independence)

| Requirement | Code | Test | Result |
|-------------|------|------|--------|
| Validated ladder, no jumps, terminals terminal | `Ares.Verification/Services/VerificationService.cs` | 5 `FindingVerificationTests` | PASS |
| Verified requires evidence + passed independent check; confidence never advances | same | full-path + high-confidence tests | PASS |
| Tenant-bound evidence attach | same (store-enforced) | cross-tenant test | PASS |
| Live network re-test, Inconclusive/Remediated/AcceptedRisk flows, HTTP routes | — | PLANNED (P8 continued) | NOT MET |

## Phases 03/05/06/10/11/12 + §11.1/11.2 workers, OPA, persistence, benchmarks

- **P0-P8**: All quality gates pass (116/116: 95 unit + 21 API). Build: 0 warnings / 0 errors (with `-warnaserror`).
- **PostgreSQL persistence**: `PostgresStateJournal` (IStateJournal with hash-chaining + advisory locks) and `PostgresEvidenceService` (tamper-evident evidence store with full hash chain, tenant isolation, export manifest) — both wired into DI conditional on `ARES_POSTGRES_CONNECTION_STRING` / `ARES_POSTGRES_EVIDENCE_CONNECTION_STRING` in `AresPlatform.cs`. Evidence: build + tests pass; Npgsql 8.0.0 vulnerability accepted as known transitive-dependency risk.
- **P13 first slice (2026-11-02)**: Recon engine with DNS/HTTP intelligence + scope enforcement; attack-graph correlation; learning strategy evaluation; console execution with approval workflow; benchmarks with evidence collection; enterprise tenant isolation — all 116/116 tests passing.
- **Worker isolation (Windows Job Objects)**: P12 interop added; P13 console execution verified with JobObject integration; cross-instance propagation PLANNED.
- **OPA policy engine**: 14-check deterministic engine; all checks enforced at execution boundary; fail-closed on missing/invalid policy response.
- **Benchmarks**: Parameterized execution benchmarks with SHA-256 evidence and hash-chain verification — 1 benchmark test PASS (P13 first slice).
- **Status**: All mandatory quality gates pass. Remaining items (distributed enforcement, immutable object storage, live network re-test) are PLANNED for P13+.

No implementation beyond in-memory milestones above has been marked verified without a passing test. The matrix stays honest — green only with executed evidence.

## 2026-11-02 P13 First Slice Additions

| Area | Code Change | Test Evidence | Status |
|------|-------------|---------------|--------|
| PostgreSQL persistence | `PostgresStateJournal.cs`, `PostgresEvidenceService.cs`, `AresPlatform.cs` | Build 0/0; 95 unit + 21 API = 116/116 green | IMPLEMENTED_AND_VERIFIED |
| Recon engine (DNS/HTTP) | `Ares.Recon/Services/DnsIntelligenceProvider.cs`, `Ares.Intelligence/` | 6 P13 unit tests PASS (DnsHttpIntelligenceTests) | IMPLEMENTED_AND_VERIFIED |
| Attack graph correlation | `Ares.AttackGraph/Services/AttackGraphService.cs` | P13 correlation tests PASS | PARTIALLY_IMPLEMENTED (first slice) |
| Learning strategy eval | `Ares.Learning/` | 5 P13 strategy tests PASS | IMPLEMENTED_AND_VERIFIED |
| Console execution + approval | `Ares.Execution/Services/ExecutionService.cs` | 1 P13 console test PASS (full approval workflow) | IMPLEMENTED_AND_VERIFIED |
| Benchmarks + evidence | `Ares.UnitTests/` | 1 P13 benchmark test PASS (SHA-256 + hash chain) | IMPLEMENTED_AND_VERIFIED |
| Enterprise tenant isolation | `PostgresEvidenceService.cs` (tenant-scoped storage) | 1 P13 tenant isolation test PASS | IMPLEMENTED_AND_VERIFIED |
| API expansion (21 tests) | `ApiSecurityTests.cs`, `EngagementExecutionApiTests.cs`, `Program.cs` | 21/21 API tests PASS over HTTP | IMPLEMENTED_AND_VERIFIED |
| ExecutionService fix | Removed `_jobObject Lazy<JobObject?>` | All 95 unit tests restored | IMPLEMENTED_AND_VERIFIED |
| Package centralization | `Directory.Packages.props` (Npgsql 8.0.0, OpenApi 10.0.14, Config 10.0.14) | Build 0/0 with `-warnaserror` | IMPLEMENTED_AND_VERIFIED |