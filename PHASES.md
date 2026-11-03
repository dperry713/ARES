# ARES Phase-by-Phase Development Plan (living document)

Source of truth for sequencing. MEC §24 defines 12 phases; this file maps them
to executed engineering slices (P0–P7 done, evidence-linked) and planned slices
(P8+). Status vocabulary is the blueprint §2.3 set.

Status rollup (2026-11-01) — detail per phase below:

| MEC phase | Plan slice | Status | Evidence |
|-----------|------------|--------|----------|
| 01 Foundation | P0 (repo, build, domain shapes) | PARTIALLY_IMPLEMENTED | 28 projects build 0/0; PostgreSQL/Redis/auth/OTel/CI PLANNED |
| 02 Engagement Management | P0 lifecycle + P7 API | PARTIALLY_IMPLEMENTED | 4 lifecycle + 3 HTTP tests green; roles/audit PLANNED |
| 03 Reconnaissance | — | PLANNED | Asset records exist; discovery/fingerprinting absent |
| 04 Vulnerability Intelligence | P2/P5 (hardcoded EPSS/CWE + store wiring) | PARTIALLY_IMPLEMENTED | Fallbacks work, marked MOCKED; live feeds PLANNED |
| 05 Attack Graph | — | PLANNED | Types + interface only |
| 06 AI Planning | P2 (deterministic fallback + null-engagement guard) | PARTIALLY_IMPLEMENTED | Real fallback paths tested; providers PLANNED |
| 07 Policy and Approval | P0/P1/P2/P6 (fail-closed policy, bound approvals, e-stop) | PARTIALLY_IMPLEMENTED | 12 policy/scope/approval/e-stop unit tests green; OPA service PLANNED |
| 08 Execution Runtime | P2/P3 (trackable contract, durable queue milestone) | PARTIALLY_IMPLEMENTED | Lifecycle + idempotency/lease/cancel/dead-letter green in-memory; Rust workers PLANNED |
| 09 Verification and Evidence | P4/P5/P8 (hash-chained store + wiring + verification ladder) | PARTIALLY_IMPLEMENTED | 6 + 3 + 5 evidence/verification tests green in-memory; immutable storage + live re-test + remaining terminals PLANNED |
| 09 Verification and Evidence | P4/P5 (hash-chained store + wiring) | PARTIALLY_IMPLEMENTED | 9 evidence tests green in-memory; immutable storage + verification levels PLANNED |
| 10 Learning Engine | — | PLANNED | Nothing implemented |
| 11 Frontend | P7 (API is the contract the console will call) | PLANNED | React dirs empty; API proven over HTTP ready for it |
| 12 Enterprise Readiness | — | PLANNED | Docs exist; deployment/benchmarks/release gates unmet |

## Slice log (executed)

- **P0 — Fail-closed control plane.** Policy denied inactive engagements + approval-gated Permit-all; scope Permit-all; engagement free transitions; untrackable execution contract; GET-verb fix. 12 tests (7 FAIL baseline → 12 PASS). KD-01..KD-06 opened.
- **P1 — Bound approvals + adversarial scope.** Real `ApprovalService` (hash binding, expiry at approve+validate, single-use, no cross-execution reuse) + `EvaluateWithApprovalAsync`; scope hardening (encoded IPs, special-use, @-smuggling, canonicalization, explicit-token escape hatch, tenant split). 18 tests. KD-04..KD-08 context.
- **P2 — Execution↔approval wiring + null-engagement negatives.** `GrantApprovalAsync` validates via store (forged/stale rejected, status stays put); `GenerateHypothesesAsync(null)` → domain error. 5 tests (P0 file grew to 13 with the e2e grant test). KD-02 closed.
- **P3 — Durable queue milestone.** Idempotent propose, `Leased` state, lease+heartbeat+reclaim, audited cancel, bounded retry + dead-letter. 6 tests. In-memory bounds → KD-07.
- **P4 — Tamper-evident evidence store.** SHA-256 ingest, per-engagement hash chain, tamper detection, tenant isolation, export manifest + independent `VerifyManifest`. 6 tests. KD-09.
- **P5 — IntelligenceService → store wiring.** `GenerateEvidenceAsync(engagement, execution, …)` persists with real tenant; binding enforced; dead SHA helper removed. 3 tests. KD-01 closed.
- **P6 — Emergency-stop end-to-end.** Shared latch (Policy+Execution agree), gate blocks queue/lease/start, recovery re-opens, audited ACTIVATE/RECOVER, idempotent. 3 tests + parallel-runner flake fixed (parallelization off, 5/5 stable). KD-10.
- **P7 — Tenant boundaries + HTTP surface + orphan retired.** Grant-layer tenant rejection, HTTP lifecycle + approval flow over the wire (fail-first 404 → singleton+locks, KD-11), orphan `Orchestrator.cs` → `.retired/`. 3 unit + 3 API tests. KD-03 resolved.

## Next slices (planned, in dependency order)

- **P8 — Finding verification lifecycle (DONE first slice).** Validated ladder + tenant-bound attach + independent-check harness + confidence-never-advances + terminals terminal; 5 tests green. Continued: live re-test, remaining terminals, HTTP routes.
- **P8b — Finding verification continued (PLANNED).** OBSERVATION→…→VERIFIED/… state machine + independent re-check service + HTTP routes. (Unblocks MEC §13 acceptance.)
- **P9 — Tenant/policy/evidence HTTP routes.** Tenants CRUD, policy evaluate, evidence ingest/verify/export over the API with tenant-scoped auth stubs. (Narrows KD-06.)
- **P10 — Reconciliation + audit trail.** Append-only audit log service; engagement/execution/approval/evidence events recorded; HTTP query surface. (Unblocks Phase 02 audit acceptance.)
- **P11 — Persistence swap.** EF Core + PostgreSQL for engagement/approval/execution/evidence stores behind the existing interfaces. (Closes KD-07/KD-09 storage half.)
- **P12 — Worker isolation milestone.** Out-of-process module host + Job Objects (Windows) with attestation; policy checks `isolation_verified` for real. (Unblocks Phase 08.)
- **P13+ — Recon modules, attack-graph build-out, learning engine, React console, benchmarks, enterprise deployment.** Each gets its own fail-first slice on arrival — no prefatory code without tests.

## How to work a slice (mandatory)

1. Write RED tests naming the MEC/blueprint clause.
2. Implement the minimum to GREEN (no weakening tests, ever).
3. Run `dotnet build Ares.Solution.slnx --no-incremental` (expect 0/0) + both test projects.
4. Update TEST_RESULTS.md, CAPABILITY_CATALOG.md, KNOWN_DEFECTS.md, MASTER_EXECUTION_LEDGER.md, MEC_COMPLIANCE_MATRIX.md, progress.json.
5. Keep `progress.html` rendering from `progress.json` — never hand-edit the dashboard numbers.
