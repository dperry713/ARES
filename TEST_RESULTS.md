# ARES Test Results (executed evidence, not claims)

## P0 fail-closed audit suite — `tests/unit/Ares.UnitTests` (new, 2026-11-01) — now 30 tests

| Run | Command | Result |
|-----|---------|--------|
| Baseline (pre-fix) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **7 failed / 3 passed / 10 total** — gaps proven (policy permitted inactive engagements, scope permitted everything, lifecycle accepted invalid jumps, execution contract untrackable) |
| Post P0 fix | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **12 passed / 0 failed / 12 total** (SDK 10.0.401, net10.0, Win x64) |
| Post P1 (approval store + adversarial scope) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **30 passed / 0 failed / 30 total** (SDK 10.0.401, net10.0, Win x64) |
| Post P2 (execution↔approval wiring + null-engagement negatives) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **33 passed / 0 failed / 33 total** (SDK 10.0.401, net10.0, Win x64) |
| Post P3 (durable queue: idempotency, lease+heartbeat, cancel, dead-letter) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **39 passed / 0 failed / 39 total** (SDK 10.0.401, net10.0, Win x64) |
| Post P4 (tamper-evident evidence store) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **45 passed / 0 failed / 45 total** (SDK 10.0.401, net10.0, Win x64) |
| Post P5 (IntelligenceService → evidence store wiring, KD-01 closed) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **48 passed / 0 failed / 48 total** (SDK 10.0.401, net10.0, Win x64) |
| Post P6 (emergency-stop end-to-end) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **51 passed / 0 failed / 51 total, stable over 5 consecutive runs** (SDK 10.0.401, net10.0, Win x64) |
| Post P7 (tenant boundaries + orphan retired + HTTP API surface) | `dotnet test tests/unit/Ares.UnitTests` + `dotnet test tests/unit/Ares.ApiTests` | **54 + 3 passed / 0 failed / 57 total** (SDK 10.0.401, net10.0, Win x64; API 3/3 over 3 runs) |
| Post P8 (finding verification lifecycle) | `dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj` | **59 passed / 0 failed / 59 total, stable 3/3 runs** (SDK 10.0.401, net10.0, Win x64) |
| Solution build | `dotnet build Ares.Solution.slnx --no-incremental` | **0 warnings / 0 errors** (all 28 projects, incl. real `Ares.Approvals` service) |

Full per-test list (post-P1, from `tests/unit/Ares.UnitTests/last_run.txt`):

P0 (12):
- Passed `PolicyEngineFailClosedTests.EvaluateAsync_authorized_but_not_active_must_not_permit`
- Passed `PolicyEngineFailClosedTests.EvaluateAsync_requires_approval_without_approval_must_not_permit`
- Passed `PolicyEngineFailClosedTests.EvaluateScopeAsync_excluded_target_must_be_denied`
- Passed `PolicyEngineFailClosedTests.EvaluateScopeAsync_out_of_scope_target_must_be_denied`
- Passed `PolicyEngineFailClosedTests.EvaluateScopeAsync_in_scope_target_must_be_permitted`
- Passed `EngagementLifecycleTests.StartAsync_from_draft_must_be_rejected`
- Passed `EngagementLifecycleTests.PauseAsync_from_draft_must_be_rejected`
- Passed `EngagementLifecycleTests.AuthorizeAsync_is_idempotent_only_from_draft_or_authorized`
- Passed `EngagementLifecycleTests.ResumeAsync_from_active_must_be_rejected`
- Passed `ExecutionApprovalBindingTests.ProposeAsync_returns_a_trackable_handle_with_Proposed_status`
- Passed `ExecutionApprovalBindingTests.Queueing_without_a_bound_approval_is_rejected`
- Passed `ExecutionApprovalBindingTests.Full_lifecycle_passes_through_the_Approved_state`

P1 approval binding (5):
- Passed `ApprovalBindingTests.Approval_is_bound_to_exact_operation_hash_material_change_invalidates`
- Passed `ApprovalBindingTests.Expired_approval_is_rejected_fail_closed` (TTL lapse enforced at approve AND validate time)
- Passed `ApprovalBindingTests.Single_use_approval_cannot_be_reused`
- Passed `ApprovalBindingTests.Approval_for_execution_A_cannot_authorize_execution_B`
- Passed `ApprovalBindingTests.Policy_with_valid_bound_approval_permits_otherwise_requires_approval`

P1 adversarial scope (13):
- Passed `Alternate_ip_encodings_never_match_scope` ×3 (decimal, hex-quad, suffix-smuggled decimal)
- Passed `Special_use_destinations_denied_when_not_in_scope` ×4 (cloud metadata 169.254.169.254, ::ffff:127.0.0.1, localhost, ::1)
- Passed `Credential_or_suffix_smuggling_does_not_enter_scope` ×3 (user@host, userinfo, suffix domain)
- Passed `Cross_tenant_scope_must_not_leak`
- Passed `Explicit_special_use_token_in_scope_is_honored` (127.0.0.1 explicitly listed permits :8080/health but not metadata IP)
- Passed `Trailing_dot_and_case_are_canonicalized_not_bypassed`

P2 execution↔approval wiring (3):
- Passed `ExecutionApprovalBindingTests.Full_lifecycle_passes_through_the_Approved_state` (now via real store: request → human approve → grant with binding)
- Passed `ExecutionApprovalBindingTests.Grant_with_swapped_or_stale_binding_is_rejected_execution_stays_put` (forged id, tampered hash, wrong target all rejected; status stays AwaitingApproval)

P2 null-engagement negatives (2, closes KD-02):
- Passed `NullEngagementFailClosedTests.GenerateHypothesesAsync_null_engagement_fails_closed_with_domain_error` (domain InvalidOperationException naming engagement, never NullReference)
- Passed `NullEngagementFailClosedTests.AnalyzeAsync_null_engagement_denies_scope_without_throwing`

P3 durable queue (6, in-memory milestone — no DB/distributed lock, KD-07 scope):
- Passed `Propose_with_same_idempotency_key_returns_same_execution` (retry-safe; same key + different params rejected)
- Passed `Propose_with_same_key_but_different_module_is_rejected`
- Passed `Lease_expires_without_heartbeat_and_execution_requeues` (stuck leases reclaimed to Queued)
- Passed `Heartbeat_keeps_lease_alive` (wrong-worker heartbeat rejected by design)
- Passed `Cancel_propagates_to_queued_and_running_with_audit_trail` (who/reason recorded; double-cancel fails)
- Passed `Exhausted_retries_dead_letter_with_attempts_preserved` (bounded retry then Failed, error preserved)

P4 evidence store (6, in-memory milestone — no immutable storage/encryption/signed envelopes, KD-09 scope):
- Passed `Ingest_computes_sha256_and_verify_passes` (64-hex SHA-256, re-verifiable)
- Passed `Records_form_a_hash_chain_second_commits_to_first` (per-engagement chain links)
- Passed `Tamper_is_detected_chain_verification_fails` (content mutation breaks record + chain verify)
- Passed `Wrong_tenant_cannot_read_or_verify` (UnauthorizedAccessException, never leak)
- Passed `Export_manifest_verifies_independently` (manifest-only chain re-verification)
- Passed `Empty_content_and_missing_record_fail_closed`

P5 store-backed evidence (3, closes KD-01 — `Guid.Empty` fabrication path retired):
- Passed `GenerateEvidence_persists_with_real_tenant_and_verifies` (tenant from engagement, record verifies via store)
- Passed `GenerateEvidence_null_engagement_fails_closed` (no record, domain error)
- Passed `GenerateEvidence_tenant_mismatch_fails_closed` (execution bound to a different engagement rejected, nothing persisted)

P6 emergency-stop end-to-end (3, blueprint §4.6 / MEC §11.4):
- Passed `Stop_latch_blocks_policy_scope_queue_lease_and_start` (policy + scope deny with EMERGENCY_STOP_ACTIVE; queue/lease/start throw; state preserved, not destroyed)
- Passed `Recovery_reopens_the_gate_and_audit_trail_records_both` (same execution proceeds post-recovery; ACTIVATE + RECOVER audited with actor)
- Passed `Stop_is_idempotent_and_recovery_requires_actor` (double-activate safe; actorless recovery rejected, latch held)
- Stability fix: process-wide static latch vs parallel runner caused flakes (3/51 in one run); assembly parallelization disabled with rationale in `AssemblyInfo.cs`; 5/5 consecutive green runs after fix.
- `IIntelligenceProvider.GetCweForCveAsync` nullability corrected to `Task<string?>` (CS8613 warnings eliminated; solution back to 0 warnings).

P7 tenant boundaries + API surface (3 unit + 3 HTTP; KD-03 resolved):
- Passed `Engagement_records_pin_their_tenant` / `Grant_with_wrong_tenant_binding_does_not_advance_execution` / `Engagement_lifecycle_ignores_unknown_tenant_records`
- Passed API `Engagement_lifecycle_over_http` (create→authorize→start→pause→resume→complete→GET; unknown id = 404)
- Passed API `Invalid_transition_is_409_not_500` (Draft→start = 409 Conflict)
- Passed API `Execution_approval_flow_over_http_with_forged_grant_rejected` (propose→request-approval→request→approve→forged grant 409→grant 200→cancel 200→double-cancel 409→missing 404)
- Fail-first proof: API tests initially 3 FAIL (scoped in-memory stores = fresh Dictionary per request → 404s); fixed by singleton lifetime + locks (EngagementService/TenantService hardened), NOT by weakening tests.
- KD-03 resolved: orphan `src/ai/Orchestrator.cs` retired to `.retired/` with provenance note (uncompilable, unreferenced); live paths documented.

P8 finding verification (5, MEC §13.2 / blueprint §6.1 — first slice of the verification milestone):
- Passed `Full_path_to_Verified_requires_evidence_plus_independent_check` (no jumps; evidence rung + distinct-method re-check required)
- Passed `Failed_recheck_moves_to_NotReproducible_not_Verified` (empty re-observation fails; terminal blocks Verified)
- Passed `High_AI_confidence_alone_cannot_produce_Verified` (0.99 confidence advances nothing)
- Passed `Evidence_from_another_tenant_is_rejected` (UnauthorizedAccessException via store)
- Passed `Terminal_states_reject_further_transitions` (FalsePositive terminal)
- Real `Ares.Verification` service replaces the second `Class1.cs` shell; in-memory per instance; independent check is a deterministic re-observation harness, NOT a live network re-test (documented bound).

## What is NOT covered (honest)

- No integration / contract / security / architecture / performance / resilience suites exist yet (`tests/*` subdirs were empty; only `tests/unit/Ares.UnitTests` added).
- No frontend, worker-isolation, DB, Redis, OPA, benchmark, or deployment tests exist. Nothing in those areas is marked verified.
- EPSS/CWE values in `IntelligenceService` remain hardcoded fallbacks (marked MOCKED in capability catalog, not evidence).
- Approval store is in-memory `Dictionary` (no DB, no audit log, no distributed lock). Concurrency throttle is a policy check only, no semaphore. DNS resolution under controlled conditions + redirect revalidation (blueprint §4.3 items 3/7) are NOT implemented — the matcher works on canonicalized host tokens, which is documented in code comments.
- `ExecutionService.GrantApprovalAsync` now validates through the approval store (binding re-checked, forged/stale rejected). Remaining: queue/start still trust in-process state; no durable queue/lease yet.
- Queue/lease/retry state is in-memory per instance (KD-07 extended): restarts lose it; no cross-instance safety. Durable persistence is the next slice.
- `IntelligenceService.CalculateSha256` dead helper removed (hashing lives in the store now).
- Finding verification check history is in-memory per instance (extends KD-07); re-checks against live targets, Inconclusive/Remediated/AcceptedRisk flows, and HTTP routes are PLANNED (P8 continued).
- API has no authN/Z, pagination, rate limiting, audit events (KD-06 scope narrowed: lifecycle + approval/execution routes now real and tested).
- Emergency-stop is process-wide in-memory (extends KD-07): no cross-instance propagation, no worker kill-signal delivery, no credential revocation — the control-plane gate + audit trail are real; distributed enforcement is PLANNED.
- Evidence store is in-memory per instance (extends KD-07/KD-09): no immutable object storage, no encryption at rest, no signed export envelopes, no retention-lock enforcement. Chain + tenant isolation + manifest verification are real; the rest is PLANNED.
- Evidence `TenantId = Guid.Empty` unattributed marker remains in `IntelligenceService.GenerateEvidenceAsync` (KD-01) — the store itself requires a real tenant id; the legacy helper is the remaining/fabricating path.

## Reproduce

```powershell
cd C:\Users\d\pentest
dotnet build Ares.Solution.slnx --no-incremental --nologo
dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj --nologo
```
