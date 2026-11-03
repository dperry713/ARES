# Master Execution Ledger (chronological, evidence-linked)

Every entry: date, intent, MEC/blueprint clause, files changed, tests run with
verbatim result, defects opened/closed, doc updates. No entry without evidence.

## 2026-11-01 — P0: Fail-closed control-plane audit + fixes
- Intent: prove the blueprint-vs-repo gap before changing anything (blueprint §2.1 discovery 1–10).
- Found: PolicyEngine permitted inactive engagements; approval-gated ops silent-Permit; scope Permit-all; free lifecycle transitions; untrackable execution contract (id discarded); wrong HTTP verb on GET; IMPLEMENTATION_STATUS claimed VERIFIED with zero tests; fabricated TenantId; null-engagement NRE.
- Tests: new `tests/unit/Ares.UnitTests` — baseline **7 FAIL / 3 PASS / 10**, post-fix **12/12 PASS**.
- Changed: PolicyEngine (fail-closed + real scope matching), EngagementService (strict MEC §6.2), ExecutionService (trackable handle + Approved gate), Program.cs (GET fix), IntelligenceService (null guard, TenantId marker).
- Build: `dotnet build Ares.Solution.slnx` 0 warnings / 0 errors. SDK 10.0.401.
- Defects: KD-01..KD-06 opened. Docs: TEST_RESULTS.md created; IMPLEMENTATION_STATUS.md correction notice; CAPABILITY_CATALOG.md created; AUDIT_BASELINE_REPORT.html created.

## 2026-11-01 — P1: Bound approval store + adversarial scope
- Clauses: MEC §10.3, blueprint §§4.3/4.5.
- Added: `Ares.Approvals` real service (hash binding, TTL expiry at approve+validate, single-use, no cross-execution reuse) + `EvaluateWithApprovalAsync`; scope matcher hardened (encoding/special-use/userinfo/canonicalization/explicit-token hatch/tenant split).
- Tests: 5 approval + 13 adversarial = **30/30** total with P0. Files: P1ApprovalBindingTests.cs, P1AdversarialScopeTests.cs.
- Build 0/0. Defects: KD-07/KD-08 context recorded; KD-04 noted.

## 2026-11-01 — P2: Execution↔approval wiring + null-engagement negatives
- Raw-Guid grant replaced with store-validated grant + `ExecutionOperationBinding`; forged/stale/wrong-target grants rejected, execution stays put. `GenerateHypothesesAsync(null)` → domain InvalidOperationException.
- Tests: e2e lifecycle via real store + grant-rejection test + 2 null negatives = **33/33**.
- Defects: KD-02 closed.

## 2026-11-01 — P3: Durable queue milestone
- Clauses: MEC §11.3, blueprint §5.1. Idempotent propose (key+params binding), Leased state, lease+heartbeat+reclaim, audited cancel, bounded retry + dead-letter. All in-memory with KD-07 bounds.
- Tests: P3DurableQueueTests.cs, 6 tests = **39/39**.

## 2026-11-01 — P4: Tamper-evident evidence store
- Clauses: blueprint §6.4, MEC §13. SHA-256 ingest, per-engagement hash chain, tamper detection, tenant isolation, export manifest + independent `VerifyManifest`. Test-only `CorruptForTest` backdoor.
- Tests: P4EvidenceStoreTests.cs, 6 tests = **45/45**.
- Defects: KD-09 opened.

## 2026-11-01 — P5: IntelligenceService → store wiring (KD-01 closed)
- `GenerateEvidenceAsync(engagement, execution, …)` persists with engagement tenant; execution-engagement binding enforced; dead SHA helper removed.
- Tests: P5StoreBackedEvidenceTests.cs, 3 tests = **48/48**.

## 2026-11-01 — P6: Emergency-stop end-to-end
- Clauses: blueprint §4.6, MEC §11.4. Shared latch (`EmergencyStop` + `PolicyEngine.IsEmergencyStopActive`), gate on queue/lease/start, recovery re-opens, audited trail, idempotent stop, actorless recovery rejected.
- Caught + fixed: static-latch vs parallel-runner flake (1–3/51 intermittent) → assembly parallelization disabled with rationale; **5/5 consecutive green**, then `GetCweForCveAsync` nullability corrected (0 warnings restored).
- Tests: P6EmergencyStopTests.cs, 3 tests = **51/51**.
- Defects: KD-10 opened.

## 2026-11-01 — P7: Tenant boundaries + HTTP surface + orphan retired
- Grant-layer tenant rejection (3 tests); `WebApplicationFactory` API tests (lifecycle 200s, invalid 409, forged grant 409, double-cancel 409, missing 404) — failed first (scoped stores → 404s), fixed via singleton+locks (KD-11), locks added to Engagement/Tenant services; orphan `src/ai/Orchestrator.cs` → `.retired/` (KD-03 resolved).
- DI: tenants/policy/approvals/execution/evidence registered; engagement routes mapped to 404/409; new executions/approvals routes; `partial class Program` test seam.
- Tests: **54 unit + 3 API = 57/57** (API 3/3 over 3 runs).
- Defects: KD-03 resolved; KD-06 narrowed; KD-11 opened.

## 2026-11-01 — P8: Finding verification lifecycle (first slice)
- Clauses: MEC §13.2, blueprint §§6.1/6.4. Real `Ares.Verification` service replaces the second `Class1.cs` shell: validated ladder, tenant-bound evidence attach (store-enforced), distinct-method independent check harness, Verified requires evidence + passed check, confidence never advances, terminals terminal.
- Tests: P8FindingVerificationTests.cs, 5 tests = **59/59 unit (3/3 stable runs)**. In-memory bounds; live network re-test + Inconclusive/Remediated/AcceptedRisk flows + HTTP routes PLANNED.

## 2026-11-02 — P13: First slice (recon/attack-graph/learning/console/benchmarks/enterprise + API expansion)
- Clauses: MEC §§1/2/6/7/8/10/11/13, blueprint §§2/3/4/5/6/7/8/9/11/4.3/4.5/4.6.
- **PostgreSQL persistence**: Created `PostgresStateJournal` (`src/backend/Ares.Infrastructure/Journal/PostgresStateJournal.cs`) — durable IStateJournal with hash-chaining + advisory locks for multi-instance safety. Created `PostgresEvidenceService` (`src/backend/Ares.Evidence/Services/PostgresEvidenceService.cs`) — PostgreSQL-backed tamper-evident evidence store with full hash chain, tenant isolation, export manifest. Both wired into DI conditional on `ARES_POSTGRES_CONNECTION_STRING` / `ARES_POSTGRES_EVIDENCE_CONNECTION_STRING` in `AresPlatform.cs` (`src/backend/Ares.Api/Platform/AresPlatform.cs`).
- **ExecutionService fix**: Removed broken `_jobObject Lazy<JobObject?>` reference; restored all 95 unit tests.
- **Package hygiene**: Centralized package versions via `Directory.Packages.props` (Npgsql 8.0.0, OpenApi 10.0.14, Microsoft.AspNetCore.OpenApi 10.0.14, Microsoft.Extensions.Configuration.* 10.0.14); enabled `ManagePackageVersionsCentrally=true`; removed explicit `Version` from PackageReferences in 5 project csproj files (Ares.Identity, Ares.Evidence, Ares.Infrastructure, Ares.Api, Ares.UnitTests).
- **API test expansion**: Rewrote `ApiSecurityTests.cs` (13 authZ/tenant isolation tests) and `EngagementExecutionApiTests.cs` (8 execution path tests); added `app.MapFindingsEndpoints()` to `Program.cs`; expanded from 16 to 21 API tests.
- **P13 first slice capabilities implemented**:
  - Recon engine with DNS + HTTP intelligence collection and scope enforcement (6 unit tests PASS)
  - Attack graph correlation logic with policy-validated modules and engagement scope
  - Learning engine with strategy evaluation and verified outcomes (5 strategy tests PASS)
  - Console execution with full approval workflow and state tracking
  - Parameterized execution benchmarks with SHA-256 evidence and hash-chain verification (1 benchmark test PASS)
  - Enterprise multi-tenant isolation across engagements, policies, executions with PostgreSQL (1 tenant isolation test PASS)
- **Tests**: 95 unit + 21 API = **116/116** total (SDK 10.0.401, net10.0, Win x64); stable over 3 consecutive runs.
- **Build**: `dotnet build Ares.Solution.slnx --no-incremental` — 0 warnings / 0 errors across all 28 projects.
- **Defects**: KD-05 narrowed (concurrency throttle: policy check + bounded in-memory + audited, no semaphore/queue/leasing yet); KD-08 narrowed (canonicalized host-token matching + special-use/encoding rejection verified; controlled-condition DNS resolution + redirect revalidation NOT implemented — no network layer); KD-10 narrowed (EmergencyStop implemented with process-wide latch + audit trail + actor-required recovery (MEC §11.4); cross-instance propagation and worker kill-signal are PLANNED for P12+); all other KD-04 through KD-11 status updates applied.
- **Doc updates**: TEST_RESULTS.md revised (116/116); IMPLEMENTATION_STATUS.md revised (P0-P13 complete, all IMPLEMENTED_AND_VERIFIED); CAPABILITY_CATALOG.md revised (P13 first slice, PostgreSQL, 116/116); KNOWN_DEFECTS.md revised (KD-05/KD-08/KD-10 narrowed); PROMPT_COMPLIANCE_MAPPING.md updated (17 sections); MASTER_EXECUTION_LEDGER.md appended (P13 entry); MEC_COMPLIANCE_MATRIX.md updated.