# ARES Capability Catalog (honest, evidence-linked — 2026-11-01, P1 update)

Status vocabulary: IMPLEMENTED_AND_VERIFIED · IMPLEMENTED_NOT_VERIFIED · PARTIALLY_IMPLEMENTED · SIMULATED · MOCKED · PLANNED · BLOCKED · UNSUPPORTED · DEPRECATED.
IMPLEMENTED_AND_VERIFIED is used only where a passing test or executed build proves it.

## P0+P1 control plane (fixed + verified this session, 30/30 green)

| Capability | Status | Evidence |
|------------|--------|----------|
| Solution builds clean (28 projects) | IMPLEMENTED_AND_VERIFIED | `dotnet build Ares.Solution.slnx --no-incremental`: 0 warnings / 0 errors, 2026-11-01, SDK 10.0.401 |
| Fail-closed policy: inactive engagement denied | IMPLEMENTED_AND_VERIFIED | `EvaluateAsync_authorized_but_not_active_must_not_permit` PASS |
| Approval-gated ops require approval (no silent Permit) | IMPLEMENTED_AND_VERIFIED | `EvaluateAsync_requires_approval_without_approval_must_not_permit` PASS |
| Scope: exclusion beats scope; out-of-scope denied; in-scope permitted | IMPLEMENTED_AND_VERIFIED | 3 × `EvaluateScopeAsync_*` PASS |
| Bound approval store (request/approve/reject/validate, MEC §10.3) | IMPLEMENTED_AND_VERIFIED | 5 × `ApprovalBindingTests` PASS (hash binding, expiry at approve+validate, single-use, no cross-execution reuse, policy integration) |
| Policy bound evaluation (`EvaluateWithApprovalAsync`) | IMPLEMENTED_AND_VERIFIED | `Policy_with_valid_bound_approval_permits_otherwise_requires_approval` PASS |
| Adversarial scope hardening (encoded IPs, special-use, @-smuggling, tenant separation, canonicalization) | IMPLEMENTED_AND_VERIFIED | 13 × `AdversarialScopeTests` PASS |
| Emergency-stop end-to-end (latch blocks policy+scope+queue+lease+start, recovery re-opens, audited ACTIVATE/RECOVER, idempotent) | IMPLEMENTED_AND_VERIFIED | 3 × `EmergencyStopTests` PASS, 5/5 stable runs (in-memory process-wide; distributed enforcement PLANNED) |
| Tenant boundaries at grant layer (wrong-tenant binding rejected, execution stays put) | IMPLEMENTED_AND_VERIFIED | 3 × `TenantBoundaryTests` PASS (store-level isolation proven in P1/P4) |
| HTTP API surface: engagement lifecycle + approval/execution flow (real services, correct status codes) | IMPLEMENTED_AND_VERIFIED | 3 × API tests PASS over HTTP (WebApplicationFactory), 3/3 runs stable |
| Finding verification ladder (Suspected→Evidence→InProgress→Verified, independent check required, confidence never advances, terminals terminal) | IMPLEMENTED_AND_VERIFIED | 5 × `FindingVerificationTests` PASS (in-memory; live re-test + remaining terminals PLANNED) |
| Orphan orchestrator retired | RESOLVED | `.retired/Orchestrator.cs.orphan-2026-11-01` + README provenance; zero references confirm |
| Engagement lifecycle transitions validated (MEC §6.2) | IMPLEMENTED_AND_VERIFIED | 4 × `EngagementLifecycleTests` PASS |
| Execution lifecycle via trackable handle, store-validated approval grant (Proposed→AwaitingApproval→Approved→Queued→Running→Verifying→Completed) | IMPLEMENTED_AND_VERIFIED | 4 × `ExecutionApprovalBindingTests` PASS (incl. forged/stale grant rejection, status stays put) |
| GET engagement by id (verb corrected, tested over HTTP) | IMPLEMENTED_AND_VERIFIED | API `Engagement_lifecycle_over_http` PASS (GET 200, unknown id 404) |
| Durable queue milestone (idempotent propose, lease+heartbeat, reclaim, bounded retry, dead-letter, audited cancel) | IMPLEMENTED_AND_VERIFIED | 6 × `DurableQueueTests` PASS (in-memory; KD-07 bounds documented) |
| Evidence store milestone (SHA-256 ingest, hash-chained per engagement, tamper detection, tenant isolation, export manifest) | IMPLEMENTED_AND_VERIFIED | 6 × `EvidenceStoreTests` + 3 × `StoreBackedEvidenceTests` PASS (in-memory; immutable storage/encryption/signed envelopes PLANNED) |
| Queue/lease/retry persistence | MOCKED | In-memory per instance; restarts lose state; no cross-instance safety (KD-07) |

## Execution reality (what the code actually is)

| Capability | Status | Evidence / note |
|------------|--------|-----------------|
| Domain model records incl. bound Approval (engagement/tenant/target/module/version/hash/uses) | IMPLEMENTED_NOT_VERIFIED | Shapes match MEC §6.1; persistence is in-memory `Dictionary`, no EF/PostgreSQL wiring |
| Engagement/tenant/approval storage | MOCKED | In-memory dictionaries; no database, no audit log, no distributed lock |
| EPSS score / CWE mapping in IntelligenceService | MOCKED | Hardcoded 0.5 + 2-entry CVE→CWE map; flagged in code and TEST_RESULTS |
| Evidence SHA-256 helper | IMPLEMENTED_NOT_VERIFIED | Real SHA-256 of input string; no immutable store, no signed manifests |
| AI provider abstraction / orchestration | PARTIALLY_IMPLEMENTED | `IIntelligenceProvider` + deterministic fallback exist; orphan `src/ai/Orchestrator.cs` is NOT compiled by any project (dead code); no provider exercised |
| Attack graph service | PARTIALLY_IMPLEMENTED | Types + interface only (`AttackGraphService.cs`); no implementation body |
| Null-engagement fail-closed (hypotheses + analysis) | IMPLEMENTED_AND_VERIFIED | 2 × `NullEngagementFailClosedTests` PASS (domain error, never NullReference) |
| Destination validation limits | PARTIALLY_IMPLEMENTED | Canonicalized host-token matching + special-use/encoding rejection verified; controlled-condition DNS resolution + redirect revalidation NOT implemented (no network layer exists) |

## Explicitly not present (PLANNED unless noted)

Recon modules, worker runtime + isolation (Windows Job Objects / Linux / K8s), module registry + signatures, OPA/Rego service, verification levels, evidence store, learning pipeline, React console (dirs exist, empty), API authN/Z + tenant isolation, audit events, OpenTelemetry, Docker/K8s/Terraform manifests, CI workflows, benchmarks, reports, migrations. 22 of 27 service projects remain `Class1.cs` shells (`Ares.Approvals` now has a real service).

## Deliberately excluded (UNSUPPORTED, per MEC §2.4)

Credential theft/harvesting, password spraying, persistence installation, unauthorized lateral movement, data exfiltration, destructive exploitation, ransomware-like actions, unrestricted remote shell, unrestricted exploit payload generation, EDR evasion, security-control disabling, unauthorized privesc, production modification.
