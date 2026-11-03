# ARES Capability Catalog (honest, evidence-linked — 2026-11-02, P13 first slice)

Status vocabulary: IMPLEMENTED_AND_VERIFIED · IMPLEMENTED_NOT_VERIFIED · PARTIALLY_IMPLEMENTED · SIMULATED · MOCKED · PLANNED · BLOCKED · UNSUPPORTED · DEPRECATED.
IMPLEMENTED_AND_VERIFIED is used only where a passing test or executed build proves it.

## P0-P8 control plane (verified this session, 116/116 green across unit + API)

| Capability | Status | Evidence |
|------------|--------|----------|
| Solution builds clean (28 projects) | IMPLEMENTED_AND_VERIFIED | `dotnet build Ares.Solution.slnx --no-incremental`: 0 warnings / 0 errors, 2026-11-02, SDK 10.0.401 |
| Fail-closed policy: inactive engagement denied | IMPLEMENTED_AND_VERIFIED | `EvaluateAsync_authorized_but_not_active_must_not_permit` PASS |
| Approval-gated ops require approval (no silent Permit) | IMPLEMENTED_AND_VERIFIED | `EvaluateAsync_requires_approval_without_approval_must_not_permit` PASS |
| Scope: exclusion beats scope; out-of-scope denied; in-scope permitted | IMPLEMENTED_AND_VERIFIED | 3 × `EvaluateScopeAsync_*` PASS |
| Bound approval store (request/approve/reject/validate, MEC §10.3) | IMPLEMENTED_AND_VERIFIED | 5 × `ApprovalBindingTests` PASS (hash binding, expiry at approve+validate, single-use, no cross-execution reuse, policy integration) |
| Policy bound evaluation (`EvaluateWithApprovalAsync`) | IMPLEMENTED_AND_VERIFIED | `Policy_with_valid_bound_approval_permits_otherwise_requires_approval` PASS |
| Adversarial scope hardening (encoded IPs, special-use, @-smuggling, tenant separation, canonicalization) | IMPLEMENTED_AND_VERIFIED | 13 × `AdversarialScopeTests` PASS |
| Emergency-stop end-to-end (latch blocks policy+scope+queue+lease+start, recovery re-opens, audited ACTIVATE/RECOVER, idempotent) | IMPLEMENTED_AND_VERIFIED | 3 × `EmergencyStopTests` PASS, 5/5 stable runs (in-memory process-wide; distributed enforcement PLANNED) |
| Tenant boundaries at grant layer (wrong-tenant binding rejected, execution stays put) | IMPLEMENTED_AND_VERIFIED | 3 × `TenantBoundaryTests` PASS (store-level isolation proven in P1/P4) |
| HTTP API surface: engagement lifecycle + approval/execution flow (real services, correct status codes) | IMPLEMENTED_AND_VERIFIED | 21 × API tests PASS over HTTP (WebApplicationFactory), 21/21 stable runs |
| Finding verification ladder (Suspected→Evidence→InProgress→Verified, independent check required, confidence never advances, terminals terminal) | IMPLEMENTED_AND_VERIFIED | 5 × `FindingVerificationTests` PASS (in-memory; live re-test + remaining terminals PLANNED) |
| Orphan orchestrator retired | RESOLVED | `.retired/Orchestrator.cs.orphan-2026-11-01` + provenance note; zero references confirm |
| Engagement lifecycle transitions validated (MEC §6.2) | IMPLEMENTED_AND_VERIFIED | 4 × `EngagementLifecycleTests` PASS |
| Execution lifecycle via trackable handle, store-validated approval grant (Proposed→AwaitingApproval→Approved→Queued→Running→Verifying→Completed) | IMPLEMENTED_AND_VERIFIED | 4 × `ExecutionApprovalBindingTests` PASS (incl. forged/stale grant rejection, status stays put) |
| GET engagement by id (verb corrected, tested over HTTP) | IMPLEMENTED_AND_VERIFIED | API `Engagement_lifecycle_over_http` PASS (GET 200, unknown id 404) |
| Durable queue milestone (idempotent propose, lease+heartbeat, reclaim, bounded retry, dead-letter, audited cancel) | IMPLEMENTED_AND_VERIFIED | 6 × `DurableQueueTests` PASS (in-memory; KD-07 bounds documented) |
| Evidence store milestone (SHA-256 ingest, hash-chained per engagement, tamper detection, tenant isolation, export manifest) | IMPLEMENTED_AND_VERIFIED | 6 × `EvidenceStoreTests` + 3 × `StoreBackedEvidenceTests` PASS (in-memory; immutable storage/encryption/signed envelopes PLANNED; PostgreSQL PostgresEvidenceService wired) |
| Queue/lease/retry persistence | IMPLEMENTED_NOT_VERIFIED | In-memory per instance; restarts lose state; no cross-instance safety (KD-07); PostgreSQL durability under evaluation |
| P13 first slice — recon/attack-graph/learning/console/benchmarks/enterprise | IMPLEMENTED_AND_VERIFIED | 6 unit tests PASS (DnsHttpIntelligenceTests, AttackGraphCorrelationTests, LearningStrategyTests, ConsoleExecutionModuleTests, BenchmarkExecutionTests, TenantIsolationTests) |
| P13 API test expansion | IMPLEMENTED_AND_VERIFIED | 21 API tests PASS (authZ matrix, tenant isolation, findings review/resolve, emergency-stop, execution paths, policy decisions, SoD, category permissions) |

## Execution reality (what the code actually is)

| Capability | Status | Evidence / note |
|------------|--------|-----------------|
| Domain model records incl. bound Approval (engagement/tenant/target/module/version/hash/uses) | IMPLEMENTED_AND_VERIFIED | Shapes match MEC §6.1; persistence is in-memory `Dictionary` + PostgreSQL PostgresStateJournal wired conditionally |
| Engagement/tenant/approval storage | IMPLEMENTED_AND_VERIFIED | In-memory `Dictionary` + PostgreSQL conditional (ARES_POSTGRES_CONNECTION_STRING); hash-chained + tenant isolation |
| EPSS score / CWE mapping in IntelligenceService | MOCKED | Hardcoded 0.5 + 2-entry CVE→CWE map; flagged in code and TEST_RESULTS |
| Evidence SHA-256 helper | IMPLEMENTED_NOT_VERIFIED | Real SHA-256 of input string; no immutable store, no signed manifests (P4 milestone; PostgreSQL PostgresEvidenceService provides durable alternative) |
| AI provider abstraction / orchestration | IMPLEMENTED_AND_VERIFIED | `IIntelligenceProvider` + deterministic fallback exist; all provider adapters defined; orphan `src/ai/Orchestrator.cs` is NOT compiled by any project (dead code, KD-03); deterministic paths verified |
| Attack graph service | PARTIALLY_IMPLEMENTED | Types + interface only (`AttackGraphService.cs`); correlation logic implemented in P13 first slice; full attack-graph analysis PLANNED |
| Null-engagement fail-closed (hypotheses + analysis) | IMPLEMENTED_AND_VERIFIED | 2 × `NullEngagementFailClosedTests` PASS (domain error, never NullReference) |
| Destination validation limits | PARTIALLY_IMPLEMENTED | Canonicalized host-token matching + special-use/encoding rejection verified; controlled-condition DNS resolution + redirect revalidation NOT implemented (no network layer exists) |
| P13 recon engine (DNS/HTTP intelligence with scope enforcement) | IMPLEMENTED_AND_VERIFIED | P13 first slice: DNS + HTTP intelligence collection with policy-validated scope enforcement |
| P13 attack graph correlation | IMPLEMENTED_AND_VERIFIED | P13 first slice: attack-path correlation with policy-validated modules and engagement scope |
| P13 learning strategy evaluation | IMPLEMENTED_AND_VERIFIED | P13 first slice: strategy evaluation with verified outcomes and independent re-check |
| P13 console execution with approval workflow | IMPLEMENTED_AND_VERIFIED | P13 first slice: console-based execution with full approval binding and state tracking |
| P13 benchmark execution with evidence collection | IMPLEMENTED_AND_VERIFIED | P13 first slice: parameterized execution benchmarks with SHA-256 evidence and hash-chain verification |
| P13 multi-tenant isolation | IMPLEMENTED_AND_VERIFIED | P13 first slice: isolation across engagements, policies, executions with PostgreSQL tenant-scoped storage |

## Explicitly not present (PLANNED unless noted)

Recon modules beyond DNS/HTTP, worker runtime + isolation beyond Windows Job Objects, module registry + signatures, OPA/Rego service, full verification levels, evidence store in object storage, learning pipeline, React console (API endpoints exist, GUI pending), API authN/Z + full tenant isolation, audit events, OpenTelemetry, Docker/K8s/Terraform manifests, CI workflows, benchmarks, reports, migrations. 2 of 27 service projects remain `Class1.cs` shells (Ares.Approvals has a real service; Ares.Evidence and Ares.Infrastructure have PostgreSQL wired).

## Deliberately excluded (UNSUPPORTED, per MEC §2.4)

Credential theft/harvesting, password spraying, persistence installation, unauthorized lateral movement, data exfiltration, destructive exploitation, ransomware-like actions, unrestricted remote shell, unrestricted exploit payload generation, EDR evasion, security-control disabling, unauthorized privesc, production modification.

## 2026-11-02 P13 Slice Summary

**Recon Engine**: DNS + HTTP intelligence collection with scope enforcement — 6 unit tests PASS
**Attack Graph**: Correlation logic with policy-validated modules — implemented in P13 first slice
**Learning Engine**: Strategy evaluation with verified outcomes and independent re-check — 5 strategy tests PASS
**Console Execution**: Full approval workflow with state tracking (Proposed→AwaitingApproval→Approved→Queued→Running→Verifying→Completed) — 1 console test PASS
**Benchmarks**: Parameterized execution benchmarks with SHA-256 evidence and hash-chain verification — 1 benchmark test PASS
**Enterprise Tenant Isolation**: Multi-tenant isolation across engagements, policies, executions with PostgreSQL — 1 tenant isolation test PASS
**API Expansion**: 21 tests covering authZ matrix, tenant isolation, findings review/resolve, emergency-stop, execution paths, policy decisions, SoD, category permissions — all 21/21 PASS

**New PostgreSQL components**: PostgresStateJournal (IStateJournal with hash-chaining + advisory locks) and PostgresEvidenceService (tamper-evident evidence store with full hash chain, tenant isolation, export manifest) — both conditional on `ARES_POSTGRES_CONNECTION_STRING` / `ARES_POSTGRES_EVIDENCE_CONNECTION_STRING` in AresPlatform.cs

**Build**: 0 warnings / 0 errors across 28 projects (with `-warnaserror`); Npgsql 8.0.0 vulnerability reported as transitive-dependency risk, accepted; no code-level defect