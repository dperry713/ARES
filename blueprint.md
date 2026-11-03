ARES — Gold-Standard Engineering and Product Blueprint
Engineering strategy
 Production target specification


Project: ARES — Autonomous Reconnaissance & Exploitation System
Operating model: AI-assisted, policy-governed, human-authorized security assessment
Objective: Establish ARES as a trustworthy, measurable, extensible, and independently verifiable security-assessment platform.
This document defines a target standard. It does not assert that ARES already implements these capabilities. Every capability must be verified against the actual repository, runtime, and test evidence. MEC.md remains the authoritative project execution contract.


1. What makes ARES a gold-standard platform?
ARES should not compete on the number of scanners, the volume of AI-generated findings, or the appearance of autonomy.

It should compete on six properties:
1. Trustworthiness: Users understand exactly what ARES is authorized to do, what it did, and what it did not do.
2. Technical accuracy: Findings are reproducible, evidence-backed, deduplicated, and independently validated.
3. Safe autonomy: AI can reason and recommend, but deterministic controls govern execution.
4. Operational excellence: Assessments are observable, recoverable, auditable, and predictable under failure.
5. Extensibility: New tools, checks, providers, environments, and integrations can be added without weakening security.
6. Measurable superiority: Quality is demonstrated through transparent benchmarks and repeatable evaluations.
ARES product promise:
ARES turns authorized security assessments into governed, reproducible, evidence-backed decisions — from scope definition through verification and remediation validation.

Avoid unsupported claims such as:
- Fully autonomous.
- Zero false positives.
- Unhackable.
- Complete coverage.
- Industry-leading accuracy.
Such claims require precise definitions and independently reproducible evidence.
1.1 Core engineering principles
Principle	Required implementation
Authorization before action	No assessment operation executes without valid scope and policy authorization
Fail closed	Policy, identity, scope, or approval uncertainty prevents execution
Evidence before severity	Finding severity never substitutes for proof
AI is advisory	Model output cannot grant permissions or bypass controls
Least privilege	Workers receive only the resources and permissions they require
Reproducibility	Results can be recreated using versioned inputs and environment metadata
Honest capability reporting	Planned, simulated, partial, and verified features are distinguishable
Human control	Authorized operators can approve, deny, pause, cancel, and stop operations


2. Repository and MEC compliance
Before adding features, establish exactly what already exists.
2.1 Mandatory discovery sequence
1. Locate and completely read MEC.md.
2. Read all supporting documents referenced by the MEC.
3. Inspect the complete repository structure, projects, workflows, tests, policies, and deployment manifests.
4. Identify actual executable entry points and runtime paths.
5. Trace every GUI control to its API, application service, execution manager, and persisted outcome.
6. Trace documented features to implementation and test evidence.
7. Identify mocks, stubs, simulations, disabled modules, and incomplete integrations.
8. Establish build, test, security, performance, and documentation baselines.
9. Create a requirement-to-code-to-test-to-evidence matrix.
10. Produce a prioritized gap assessment.
2.2 Mandatory documentation inventory
Maintain:
Document	Purpose
MEC.md	Authoritative project execution contract
README.md	Accurate product overview and verified setup
ARCHITECTURE.md	Actual architecture and trust boundaries
THREAT_MODEL.md	Threats, attack surfaces, mitigations
SECURITY_DECISIONS.md	Security decisions and rationale
ARCHITECTURE_DECISIONS.md	Architectural decisions
IMPLEMENTATION_STATUS.md	Actual implementation status
MASTER_EXECUTION_LEDGER.md	Chronological engineering record
MEC_COMPLIANCE_MATRIX.md	Requirement traceability
CAPABILITY_CATALOG.md	Supported and verified capabilities
API_REFERENCE.md	Actual API behavior
CONFIGURATION_REFERENCE.md	Real configuration options
SKILL_INVENTORY.md	Skills discovered and applied
TEST_STRATEGY.md	Test architecture
TEST_RESULTS.md	Actual test executions
BENCHMARK_METHODOLOGY.md	Benchmark methodology
BENCHMARK_RESULTS.md	Actual benchmark results
KNOWN_DEFECTS.md	Outstanding defects
RISK_REGISTER.md	Technical and security risks
DEPLOYMENT.md	Verified deployment procedures
OPERATIONS_RUNBOOK.md	Operational procedures
RELEASE_READINESS.md	Release gates and evidence
CHANGELOG.md	User-visible changes

Documentation is part of the definition of done for every implementation task.
2.3 Capability status vocabulary
Use one controlled vocabulary:
- IMPLEMENTED_AND_VERIFIED
- IMPLEMENTED_NOT_VERIFIED
- PARTIALLY_IMPLEMENTED
- SIMULATED
- MOCKED
- PLANNED
- BLOCKED
- UNSUPPORTED
- DEPRECATED
A feature cannot be labeled verified merely because its source code exists or a unit test passes.

3. Target architecture
ARES should strictly separate planning, authorization, execution, and evidence.







airasecurity.ai


3.1 Reference architecture
             WEB UI / CLI / API
                     |
             IDENTITY / TENANT
                     |
            ENGAGEMENT & SCOPE
                     |
              AI INTELLIGENCE
                     |
            DETERMINISTIC POLICY
                     |
             HUMAN APPROVAL
                     |
             EXECUTION MANAGER
                     |
             ISOLATED WORKERS
                     |
          EVIDENCE INGESTION
                     |
             VERIFICATION
                     |
       +-------------+-------------+
       |             |             |
    FINDINGS      ATTACK GRAPH   REPORTS

Cross-cutting:
Audit events, telemetry, secrets, tenant isolation,
policy versioning, evidence storage, backups.


This is a target architecture, not a claim about the current repository.
3.2 Trust boundaries
Explicitly document and test:
1. Browser/CLI to API.
2. Tenant to tenant.
3. AI provider to ARES.
4. Untrusted target content to planner.
5. Policy engine to approval service.
6. Approval service to execution manager.
7. Control plane to worker.
8. Worker to target network.
9. Worker to evidence store.
10. External integrations to ARES.
11. Administrative plane to tenant plane.
Treat all target-derived content as untrusted. A web page, repository file, API response, or tool output must never be able to change ARES instructions, scope, permissions, or policy.
3.3 Recommended technology stack
Layer	Technology
Management API	.NET 10 / ASP.NET Core
Persistence	PostgreSQL / EF Core
AI orchestration	Python 3.13+ / FastAPI / Pydantic v2
Execution runtime	Rust stable / Tokio
Web console	React / TypeScript strict / Vite
Policy engine	OPA / Rego
Cache and queues	Redis or managed equivalent
Evidence storage	S3-compatible object storage
Telemetry	OpenTelemetry
Packaging	Docker
Orchestration	Kubernetes
Infrastructure	Terraform

These are candidate technologies. Preserve existing working components unless there is a documented reason to change them.

4. Authorization, scope, and safety
The most important security property is that no AI-generated plan can authorize its own execution.
4.1 Engagement authorization
Every engagement should include:
- Tenant and owner.
- Authorized assets.
- Explicit excluded assets.
- Permitted assessment categories.
- Prohibited operations.
- Approved test window and timezone.
- Rate and concurrency limits.
- Maximum duration.
- Approval requirements.
- Authorization evidence and expiration.
- Emergency contact.
- Emergency-stop configuration.
- Change history and policy version.
Scope must be validated at execution time, not only when the engagement is created.
4.2 Example authorization envelope
{
  "authorizationId": "auth_01JEXAMPLE",
  "engagementId": "eng_01JEXAMPLE",
  "tenantId": "tenant_acme",
  "scopeVersion": 12,
  "policyVersion": "ares-policy-3.4.1",
  "targets": [
    {
      "assetId": "asset_web_001",
      "canonicalHost": "staging.example.test",
      "allowedPorts": [443],
      "allowedSchemes": ["https"]
    }
  ],
  "excludedTargets": [
    "admin.staging.example.test",
    "192.0.2.44"
  ],
  "permittedCategories": [
    "passive_reconnaissance",
    "safe_http_configuration_checks",
    "authenticated_read_only_validation"
  ],
  "approvalRequiredFor": [
    "active_service_interaction",
    "authenticated_state_change"
  ],
  "rateLimits": {
    "requestsPerSecond": 2,
    "concurrentWorkers": 1
  },
  "validFrom": "2026-11-01T14:00:00Z",
  "expiresAt": "2026-11-01T18:00:00Z",
  "maxExecutionSeconds": 900,
  "emergencyStopEnabled": true
}


This is illustrative data, not an active authorization.

Production authorization must validate the issuer, signature, tenant, engagement, scope version, expiration, and operation parameters.
4.3 DNS and destination validation
Prevent scope bypass through:
- DNS rebinding.
- CNAME chains.
- Redirects to out-of-scope hosts.
- IPv4/IPv6 representation tricks.
- Alternate numeric IP encodings.
- Proxy environment variables.
- URL parser discrepancies.
- Cloud metadata endpoints.
- Loopback, link-local, multicast, and reserved ranges unless explicitly authorized.
- Time-of-check/time-of-use issues.
A robust connection workflow:
1. Parse and canonicalize the URL.
2. Validate scheme, hostname, port, and path.
3. Resolve A and AAAA records.
4. Canonicalize every IP.
5. Reject prohibited destinations.
6. Bind the validated destination to the connection.
7. Revalidate every redirect.
8. Record the resolved destination and policy decision.
9. Enforce network egress controls independently.
Do not perform a one-time DNS lookup followed by an unconstrained hostname connection.
4.4 Policy decision example
Illustrative Rego:
package ares.execution

default allow := false

allow if {
    input.identity.authenticated == true
    input.engagement.status == "ACTIVE"
    input.authorization.valid == true
    input.authorization.expired == false
    input.target.in_scope == true
    input.target.excluded == false
    input.operation.category in input.authorization.permitted_categories
    input.worker.isolation_verified == true
    input.rate_limit.allowed == true
    approval_satisfied
}

approval_satisfied if {
    input.operation.requires_approval == false
}

approval_satisfied if {
    input.operation.requires_approval == true
    input.approval.status == "APPROVED"
    input.approval.engagement_id == input.engagement.id
    input.approval.operation_hash == input.operation.hash
    input.approval.not_expired == true
}


The actual implementation must be versioned, tested, and fail closed.
4.5 Approval binding
Approval must be bound to:
- Engagement and scope version.
- Exact target.
- Operation type and canonical parameter digest.
- Tool and module version.
- Time window and expiry.
- Rate and concurrency limits.
- Requesting and approving identities.
- Policy version.
- One-time or bounded-use semantics.
Changing material operation parameters invalidates the approval.
4.6 Emergency stop
Implement:
- Immediate denial of new jobs.
- Cancellation of active jobs.
- Worker termination after a bounded grace period.
- Queue suspension.
- Credential revocation where feasible.
- Persistent audit event.
- UI confirmation of actual stop state.
- Authorized recovery.
Test during worker hangs, network partitions, queue backlog, and control-plane restarts.

5. Secure execution runtime
Workers are security boundaries, not ordinary processes.
5.1 Worker lifecycle
REQUESTED
  -> POLICY_EVALUATED
  -> APPROVAL_PENDING
  -> AUTHORIZED
  -> QUEUED
  -> LEASED
  -> ISOLATION_PROVISIONED
  -> RUNNING
  -> OUTPUT_QUARANTINED
  -> EVIDENCE_VALIDATED
  -> COMPLETED

Failure paths:
  CANCEL_REQUESTED
  CANCELLED
  FAILED
  TIMED_OUT
  TERMINATED


Enforce valid state transitions. Use idempotency keys, leases, heartbeat monitoring, bounded retries, and dead-letter handling.
5.2 Linux isolation
- Dedicated non-root identity.
- User, PID, mount, network, and IPC namespaces where appropriate.
- Seccomp syscall allowlists.
- AppArmor or SELinux.
- Dropped capabilities.
- Read-only root filesystem.
- Private temporary directories.
- CPU, memory, process, and disk quotas.
- Restricted network namespace.
- No host Docker socket.
- No unrestricted host filesystem mounts.
- Short-lived credentials.
- Ephemeral worker destruction.
5.3 Windows isolation
- Dedicated low-privilege identities.
- Restricted tokens.
- Windows Job Objects.
- AppContainer where compatible.
- ACL-protected named pipes.
- Executable and DLL search-path controls.
- Restricted filesystem and registry access.
- Windows Firewall egress restrictions.
- Credential isolation.
- Process-tree termination and lifecycle auditing.
5.4 Kubernetes isolation
- Dedicated namespaces.
- Restricted Pod Security Standards.
- Non-root containers.
- Read-only root filesystem.
- Dropped capabilities.
- Seccomp profiles.
- Default-deny NetworkPolicies.
- Dedicated service accounts.
- Resource quotas.
- Ephemeral storage limits.
- Image signature verification.
- Admission policies.
- Worker TTL and cleanup.
Verify actual enforcement in each claimed environment.
5.5 Module registry
Do not expose arbitrary shell execution as the standard module interface.

Example module manifest:
{
  "moduleId": "http.security.headers",
  "moduleVersion": "1.2.0",
  "contractVersion": "ares.module.v1",
  "category": "safe_http_configuration_checks",
  "riskClass": "LOW",
  "requiredCapabilities": [
    "network.connect.authorized_target"
  ],
  "supportsCancellation": true,
  "supportsDeterministicReplay": true,
  "evidenceTypes": [
    "http_response_metadata",
    "header_observation"
  ]
}


A registry must validate provenance, compatibility, permissions, schema, and policy eligibility before activation.

6. Evidence-first findings
A finding is not trustworthy simply because a scanner emitted it or an LLM described it convincingly.
6.1 Finding lifecycle
OBSERVATION
  -> NORMALIZED
  -> CORRELATED
  -> HYPOTHESIS
  -> VALIDATION_PLANNED
  -> AUTHORIZED_VALIDATION
  -> VERIFIED | NOT_REPRODUCIBLE | INCONCLUSIVE
  -> TRIAGED
  -> REMEDIATION_RECOMMENDED
  -> RETESTED
  -> CLOSED


Preserve the distinction between observation, inference, and verified vulnerability.
6.2 Finding data model
Capture:
- Stable finding ID.
- Tenant and engagement.
- Scope version.
- Asset.
- Finding type and taxonomy mapping.
- Technical description.
- Affected component and version.
- Observation timestamp.
- Discovery module and version.
- Evidence references and hashes.
- Reproduction preconditions.
- Verification status.
- Confidence and uncertainty.
- Severity with rationale.
- Exploitability context.
- Business impact.
- Reviewer and disposition.
- Remediation recommendation.
- Retest evidence.
- Provenance and chain of custody.
Never equate model confidence with vulnerability verification.
6.3 Evidence envelope example
{
  "evidenceId": "ev_01JEXAMPLE",
  "findingId": "finding_01JEXAMPLE",
  "engagementId": "eng_01JEXAMPLE",
  "assetId": "asset_web_001",
  "source": {
    "moduleId": "http.security.headers",
    "moduleVersion": "1.2.0",
    "workerImageDigest": "sha256:..."
  },
  "observedAt": "2026-11-01T15:12:31Z",
  "target": {
    "canonicalHost": "staging.example.test",
    "resolvedAddress": "192.0.2.10",
    "scheme": "https",
    "port": 443
  },
  "observation": {
    "type": "missing_security_header",
    "header": "Content-Security-Policy"
  },
  "integrity": {
    "sha256": "...",
    "storageObjectVersion": "..."
  },
  "verification": {
    "status": "VERIFIED",
    "method": "repeatable_read_only_http_observation",
    "attempts": 2
  }
}


6.4 Evidence integrity
- Hash evidence artifacts at ingestion.
- Use immutable or retention-locked storage where appropriate.
- Preserve original observations.
- Record transformations and redactions.
- Encrypt in transit and at rest.
- Restrict tenant and engagement access.
- Audit evidence reads, exports, modifications, and deletion.
- Use signed manifests for exported evidence packages.
Do not store secrets, session tokens, or unnecessary personal data in evidence.

7. AI architecture
AI should improve analysis quality and analyst productivity. It must not become the security authority.
7.1 Appropriate AI responsibilities
- Summarize authorized asset inventory.
- Cluster and deduplicate observations.
- Generate test hypotheses within permitted categories.
- Explain evidence and uncertainty.
- Suggest safe next steps.
- Map observations to established taxonomies.
- Prioritize findings.
- Draft remediation guidance.
- Explain attack-path relationships.
- Summarize assessment differences.
AI must not:
- Grant scope or permissions.
- Approve its own operations.
- Change policy enforcement.
- Convert target content into instructions.
- Execute arbitrary model-generated commands.
- Suppress contradictory evidence.
- Mark a finding verified without deterministic evidence checks.
- Change production policies through online learning.
7.2 Structured model output
Example hypothesis contract:
{
  "schemaVersion": "ares.hypothesis.v1",
  "hypothesisId": "hyp_01JEXAMPLE",
  "assetId": "asset_web_001",
  "observationIds": ["obs_001", "obs_002"],
  "hypothesisType": "security_configuration",
  "statement": "The application may lack a required browser security header.",
  "supportingEvidence": ["obs_001"],
  "contradictingEvidence": [],
  "confidence": 0.74,
  "proposedValidation": {
    "moduleId": "http.security.headers",
    "category": "safe_http_configuration_checks"
  },
  "limitations": [
    "Observation reflects one endpoint and one response."
  ]
}


The API must validate the schema, referenced observation IDs, tenant, engagement, and module permissions independently.
7.3 Prompt injection resistance
- Delimit target-derived content as untrusted.
- Never place target content in privileged instruction channels.
- Prevent model output from invoking tools directly.
- Require typed proposals.
- Validate all parameters against engagement scope.
- Maintain adversarial fixtures.
- Redact sensitive data before external model transmission.
- Track model and prompt-template versions.
- Provide a no-external-model operating mode where feasible.
7.4 AI evaluation metrics
Measure:
- Schema-valid output rate.
- Unsupported claim rate.
- Hypothesis precision and recall.
- Calibration error.
- Evidence citation correctness.
- Prompt injection resistance.
- Unsafe proposal rejection.
- Analyst acceptance and override rates.
- Cost per useful validated finding.
- Latency by model and task.
Do not optimize solely for the number of generated findings.

8. Attack graph and risk prioritization
A useful attack graph represents evidence-backed relationships, not speculative narratives.

Potential nodes:
- Assets.
- Services.
- Software components.
- Identities.
- Permissions.
- Vulnerabilities.
- Exposures.
- Trust relationships.
- Network boundaries.
- Findings.
- Evidence.
- Business services.
Potential edges:
- RUNS
- EXPOSES
- DEPENDS_ON
- AUTHENTICATES_TO
- CAN_ACCESS
- TRUSTS
- AFFECTED_BY
- SUPPORTED_BY
- MITIGATED_BY
Every edge should include provenance, observation time, confidence, and source.
8.1 Risk model
Consider:
- Verified severity.
- Exploitability evidence.
- Known exploitation status from trusted sources.
- Asset exposure.
- Identity privilege.
- Reachability.
- Business criticality.
- Compensating controls.
- Evidence freshness.
- Confidence and uncertainty.
Keep established severity taxonomies intact. Make contextual prioritization explainable.

Example:
{
  "findingId": "finding_01JEXAMPLE",
  "priority": "HIGH",
  "priorityScore": 82,
  "scoreVersion": "risk-model-2.1",
  "contributions": [
    {"factor": "verified_severity", "value": 30},
    {"factor": "external_exposure", "value": 20},
    {"factor": "asset_criticality", "value": 18},
    {"factor": "exploitability_evidence", "value": 14}
  ],
  "uncertainty": [
    "Asset ownership metadata is 45 days old."
  ],
  "recommendedAction": "Validate ownership metadata and prioritize remediation."
}


This is illustrative output, not an actual measured ARES score.

9. Benchmarking: prove quality
A gold-standard tool requires a transparent evaluation methodology.
9.1 Benchmark corpus
Include:
- Known vulnerabilities and secure controls.
- Multiple technology stacks and versions.
- False-positive traps.
- Misconfigurations.
- Authentication and authorization boundaries.
- API authorization cases.
- Cloud configuration scenarios.
- Network segmentation cases.
- Prompt injection content.
- Stale and conflicting observations.
- Duplicate findings.
- Partial outages and timeouts.
- Scope and DNS edge cases.
Use controlled labs, synthetic assets, and explicitly authorized systems.
9.2 Benchmark metrics
Dimension	Metric
Detection	Precision, recall, F1
Verification	Verified finding rate
False positives	False discovery rate
False negatives	Miss rate
Reproducibility	Repeated result consistency
Scope safety	Out-of-scope action count
Policy	Unauthorized action rejection
Resilience	Recovery and completion rates
Performance	Time to useful result
Cost	Cost per validated finding
Usability	Analyst task completion
Reporting	Evidence completeness

For a labeled benchmark:
- Precision = TP / (TP + FP)
- Recall = TP / (TP + FN)
- F1 = 2 × Precision × Recall / (Precision + Recall)
- False positive rate = FP / (FP + TN)
Define the unit of analysis. Host-level findings and endpoint-level findings are not interchangeable.
9.3 Example benchmark gate
Illustrative initial thresholds, not achieved ARES results:
benchmark:
  required_suites:
    - policy_authorization
    - scope_enforcement
    - finding_verification
    - prompt_injection
    - worker_isolation
    - evidence_integrity
    - regression_corpus

  hard_invariants:
    unauthorized_execution_count: 0
    out_of_scope_connection_count: 0
    unapproved_high_risk_operation_count: 0
    evidence_integrity_failures: 0

  quality_thresholds:
    minimum_precision: 0.90
    minimum_recall: 0.85
    minimum_reproducibility: 0.98

  release_behavior:
    fail_on_hard_invariant_violation: true
    fail_on_required_suite_failure: true
    require_benchmark_artifact: true


Calibrate thresholds using actual data and statistical confidence.

10. Testing strategy
Required layers:
1. Unit tests.
2. Application service tests.
3. API contract tests.
4. Database integration tests.
5. Event contract tests.
6. Policy regression tests.
7. Architecture tests.
8. Worker isolation tests.
9. Security regression tests.
10. Adversarial tests.
11. Property-based tests.
12. Fuzz tests.
13. End-to-end tests.
14. UI and accessibility tests.
15. Performance tests.
16. Resilience tests.
17. Backup and restore tests.
18. Migration tests.
19. Cross-platform tests.
10.1 Critical negative tests
Prove that:
- Missing authorization is rejected.
- Expired authorization is rejected.
- Revoked authorization is rejected.
- Out-of-scope targets are rejected.
- Excluded targets are rejected.
- Redirects cannot escape scope.
- DNS rebinding cannot bypass destination validation.
- IPv4/IPv6 normalization cannot bypass scope.
- Approval cannot authorize modified operation parameters.
- Cross-tenant access is denied.
- Workers cannot access another worker's secrets.
- Model output cannot change policy.
- Prompt injection cannot grant tool access.
- Cancellation terminates work within a defined bound.
- Evidence tampering is detected.
- Duplicate events do not create duplicate execution.
- Restarts do not silently lose authorization state.
10.2 Coverage policy
Suggested starting targets:
Area	Target
Domain logic	90%
Critical policy rules	100% decision-case coverage
Application services	85%
Frontend business logic	80%
Critical authorization paths	Every decision branch tested

Coverage is not a security guarantee. Never exclude difficult code simply to improve a percentage.

11. API and integration quality
API requirements:
- Versioned endpoints.
- OpenAPI generated from actual contracts.
- Authentication and tenant authorization.
- Resource-level authorization.
- Input validation and size limits.
- Pagination and stable sorting.
- Idempotency for job creation.
- Optimistic concurrency.
- Rate limiting and quotas.
- Standardized error envelopes.
- Correlation and trace IDs.
- Audit events.
- Compatibility tests.
Example error:
{
  "type": "https://docs.example.test/errors/scope-violation",
  "title": "Operation rejected by scope policy",
  "status": 403,
  "code": "ARES_SCOPE_DENIED",
  "detail": "The requested target is not included in the active engagement scope.",
  "traceId": "00-...",
  "correlationId": "corr_01JEXAMPLE"
}


Potential integrations:
- Microsoft Sentinel.
- Splunk.
- Elastic.
- Jira.
- ServiceNow.
- GitHub Issues.
- OIDC and OAuth 2.0.
- SAML where needed.
- SCIM.
- AWS, Azure, GCP inventory.
- SIEM and CMDB systems.
- Slack and Microsoft Teams.
Support standards where useful:
- CVE.
- CWE.
- CVSS.
- CPE.
- EPSS.
- KEV.
- STIX/TAXII.
- CycloneDX.
- SPDX.
- SARIF.
- OWASP taxonomies.
Document supported versions and limitations. Partial format parsing must not be represented as full standards support.

12. GUI: operational truth
The interface must expose real workflows and actual backend state.
12.1 Core workspaces









Operations dashboard

System health, execution activity, and assessment status.










Attack graph

Evidence-backed asset and vulnerability relationships.










Findings and triage

Verification status, evidence, severity, and remediation.










Approval inbox

Operation details, authorization, and human decisions.


Additional workspaces:
- Tenants and users.
- Engagements and authorization.
- Asset inventory and scope.
- Reconnaissance observations.
- Intelligence.
- AI hypotheses.
- Execution manager.
- Evidence explorer.
- Remediation and retesting.
- Benchmark center.
- Learning and model evaluation.
- Reports.
- Audit log.
- Integrations.
- Policy administration.
- System health.
12.2 Every control needs an execution contract
For each button, toggle, form, chart action, and menu item, document:
- User intent.
- Required permission.
- API endpoint or command.
- Application service.
- Domain operation.
- State transition.
- Audit event.
- Success condition.
- Failure behavior.
- Cancellation behavior.
- UI feedback.
- Test coverage.
Example:
control:
  id: execution.cancel
  label: Cancel execution
  permission: execution.cancel
  command: CancelExecution
  api: POST /api/v1/executions/{executionId}/cancel
  preconditions:
    - execution belongs to caller tenant
    - execution is cancellable
    - caller has permission
  success:
    - cancellation request persisted
    - worker receives cancellation signal
    - final state confirmed by execution manager
  failure:
    - show standardized error
    - preserve current execution state
  audit:
    event: execution.cancel_requested


Do not display Cancelled just because an HTTP request was accepted. Display Cancellation requested until the execution manager confirms termination.
12.3 Status vocabulary
- Pending.
- Awaiting approval.
- Authorized.
- Queued.
- Starting.
- Running.
- Cancellation requested.
- Cancelling.
- Completed.
- Failed.
- Timed out.
- Cancelled.
- Terminated.
- Verification pending.
- Verification inconclusive.
Every status must originate from authoritative backend state.

13. Observability and reliability
Use OpenTelemetry to instrument:
- API requests.
- Authentication and authorization.
- Policy evaluations.
- Approval lifecycle.
- Queue latency.
- Worker provisioning and execution.
- Module duration.
- Evidence ingestion.
- Verification.
- Database and storage operations.
- AI provider calls.
- Cancellation propagation.
- Report generation.
Example metrics:
ares_jobs_submitted_total
ares_jobs_completed_total
ares_jobs_failed_total
ares_jobs_cancelled_total
ares_job_queue_wait_seconds
ares_worker_startup_seconds
ares_policy_denials_total
ares_approval_wait_seconds
ares_evidence_verification_failures_total
ares_ai_provider_latency_seconds
ares_ai_provider_cost_total
ares_out_of_scope_attempts_total


Avoid raw hostnames, tenant IDs, finding IDs, or user IDs as unbounded metric labels.

Reliability requirements:
- Timeouts on external operations.
- Retries only for safe, idempotent operations.
- Exponential backoff with jitter.
- Circuit breakers.
- Bulkheads.
- Bounded queues.
- Idempotent consumers.
- Lease expiry.
- Dead-letter queues.
- Graceful shutdown.
- Health and readiness endpoints.
- Backup and restore validation.
An unavailable authorization service must never become an allow decision.

14. Data protection and tenant isolation
- Enforce tenant ID at every data access boundary.
- Use database row-level security where appropriate.
- Apply tenant-aware object storage authorization.
- Encrypt data in transit and at rest.
- Centralize secrets management.
- Use key rotation and revocation.
- Keep long-lived secrets out of worker processes.
- Redact sensitive evidence.
- Define retention and deletion policies.
- Audit privileged access.
- Protect backups.
- Document model-provider data handling.
- Do not transmit customer data to external AI providers without explicit authorization.

15. Software supply-chain security
Required controls:
- Pinned dependencies and lockfiles.
- Dependency reviews.
- SBOM generation.
- Dependency scanning.
- Container scanning.
- Secret scanning.
- SAST and applicable DAST.
- Build provenance.
- Signed artifacts where supported.
- Least-privilege CI identities.
- Protected branches and release tags.
- Security-sensitive CODEOWNERS.
- Toolchain version recording.
- Vulnerability disclosure and patch SLAs.
A green dependency scan is not proof that the product is secure.

16. Production implementation examples
These examples illustrate expected engineering rigor. They are not verified ARES source code.
16.1 .NET policy decision contract
public enum PolicyDecisionKind
{
    Allow,
    Deny,
    RequireApproval
}

public sealed record PolicyDecision(
    PolicyDecisionKind Kind,
    string PolicyVersion,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EvaluatedAt);

public sealed record ExecutionAuthorizationRequest(
    Guid TenantId,
    Guid EngagementId,
    Guid ScopeVersion,
    string TargetId,
    string OperationHash,
    string OperationCategory,
    DateTimeOffset RequestedAt);

public interface IExecutionPolicy
{
    Task<PolicyDecision> EvaluateAsync(
        ExecutionAuthorizationRequest request,
        CancellationToken cancellationToken);
}


Requirements:
- Deterministic evaluation for identical inputs and policy version.
- Unknown policy state denies execution.
- Tenant and engagement ownership verified.
- Operation hash covers canonical parameters.
- Decision and reason codes persisted.
- Callers cannot forge trusted allow decisions.
- Only a trusted policy adapter can produce a verifiable authorization artifact.
16.2 .NET cancellation contract
public enum ExecutionStatus
{
    Queued,
    Starting,
    Running,
    CancellationRequested,
    Cancelling,
    Completed,
    Failed,
    TimedOut,
    Cancelled,
    Terminated
}

public sealed record ExecutionSnapshot(
    Guid Id,
    ExecutionStatus Status,
    DateTimeOffset UpdatedAt,
    string? FailureCode);

public interface IExecutionCoordinator
{
    Task<ExecutionSnapshot> RequestCancellationAsync(
        Guid executionId,
        Guid tenantId,
        string actorId,
        CancellationToken cancellationToken);
}


Do not report terminal cancellation until the worker or supervisor confirms termination.
16.3 Python structured proposal validation
from typing import Literalfrom pydantic import BaseModel, ConfigDict, Fieldclass ValidationProposal(BaseModel):    model_config = ConfigDict(        extra="forbid",        strict=True,        frozen=True,    )    module_id: str = Field(min_length=1, max_length=128)    category: Literal[        "passive_reconnaissance",        "safe_http_configuration_checks",        "authenticated_read_only_validation",    ]    observation_ids: tuple[str, ...] = Field(        min_length=1,        max_length=50,    )    rationale: str = Field(min_length=1, max_length=2000)



This validates structure, not authorization. The server must independently verify engagement ownership and module permissions.
16.4 Rust fail-closed decision component
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Decision {
    Allow,
    Deny { reason_code: &'static str },
    RequireApproval { reason_code: &'static str },
}

pub fn decide(
    authenticated: bool,
    engagement_active: bool,
    authorization_valid: bool,
    target_in_scope: bool,
    target_excluded: bool,
    isolation_verified: bool,
    rate_limit_allowed: bool,
) -> Decision {
    if !authenticated {
        return Decision::Deny {
            reason_code: "IDENTITY_REQUIRED",
        };
    }

    if !engagement_active || !authorization_valid {
        return Decision::Deny {
            reason_code: "AUTHORIZATION_INVALID",
        };
    }

    if !target_in_scope || target_excluded {
        return Decision::Deny {
            reason_code: "SCOPE_DENIED",
        };
    }

    if !isolation_verified {
        return Decision::Deny {
            reason_code: "ISOLATION_UNVERIFIED",
        };
    }

    if !rate_limit_allowed {
        return Decision::Deny {
            reason_code: "RATE_LIMIT_DENIED",
        };
    }

    Decision::Allow
}


This is a small decision component, not a complete authorization system. Production policy must additionally validate approvals, operation parameters, destination canonicalization, tenant ownership, and time windows.
16.5 TypeScript capability registry
export type CapabilityStatus =
  | "IMPLEMENTED_AND_VERIFIED"
  | "IMPLEMENTED_NOT_VERIFIED"
  | "PARTIALLY_IMPLEMENTED"
  | "SIMULATED"
  | "MOCKED"
  | "PLANNED"
  | "BLOCKED"
  | "UNSUPPORTED"
  | "DEPRECATED";

export interface CapabilityRecord {
  id: string;
  name: string;
  status: CapabilityStatus;
  evidenceReferences: readonly string[];
  limitations: readonly string[];
  lastVerifiedAt?: string;
  version: string;
}


The UI must render the status from the authoritative capability registry, not infer verification from a feature flag.

17. Learning system
ARES may learn from prior assessments, analyst dispositions, and benchmark outcomes. Learning must never alter execution authority.
17.1 Permitted inputs
- Analyst-confirmed true positives and false positives.
- Reproducibility outcomes.
- Remediation verification.
- Benchmark labels.
- Module reliability measurements.
- Model evaluation results.
- Customer-specific tuning with consent and tenant isolation.
17.2 Promotion workflow
Candidate improvement
  -> Dataset and provenance review
  -> Offline evaluation
  -> Security and regression evaluation
  -> Human review
  -> Staged release
  -> Canary monitoring
  -> Promotion or rollback


Never allow online learning to modify:
- Authorization policy.
- Scope enforcement.
- Approval requirements.
- Worker isolation.
- Secret access.
- Tenant boundaries.
- Emergency-stop behavior.

18. Deployment and release engineering
18.1 CI pull-request gates
- Formatting.
- Compiler and type checks.
- Static analysis.
- Unit tests.
- Integration tests.
- API and event contract tests.
- Architecture tests.
- Policy regression tests.
- Security tests.
- Secret scanning.
- Dependency scanning.
- Container scanning.
- Documentation checks.
- OpenAPI drift checks.
- Capability registry validation.
- Benchmark regression suites.
18.2 Release pipeline
Commit
  -> Build
  -> Unit / Integration / Contract Tests
  -> Security Scans
  -> Policy and Adversarial Suites
  -> Benchmark Regression
  -> SBOM and Provenance
  -> Artifact Signing
  -> Staging Deployment
  -> Smoke / E2E / Isolation Tests
  -> Human Release Approval
  -> Production Deployment
  -> Health Verification
  -> Monitor / Rollback Decision


18.3 Deployment environments
Maintain isolated:
- Local development.
- Automated test.
- Security test.
- Staging.
- Production.
Potential deployment options:
- Docker Compose for local development.
- Kubernetes for production where justified.
- Managed PostgreSQL.
- Managed object storage.
- Terraform for repeatable infrastructure.
Do not claim cloud-provider support until the deployment path has been exercised.
18.4 Operational runbooks
Create and test runbooks for:
- Initial installation.
- Upgrade and migration.
- Rollback.
- Worker outage.
- AI provider outage.
- Database outage.
- Queue backlog.
- Storage outage.
- Secret rotation.
- Tenant compromise.
- Evidence integrity incident.
- Emergency stop.
- Backup restoration.
- Disaster recovery.
A backup job reporting success does not prove recoverability. Only a tested restore provides that evidence.

19. Governance and standards mapping
Map ARES controls to relevant frameworks where appropriate:
- NIST Cybersecurity Framework.
- NIST SP 800-53.
- NIST SP 800-115.
- NIST SP 800-218 (SSDF).
- OWASP ASVS.
- OWASP Web Security Testing Guide.
- OWASP API Security Top 10.
- MITRE ATT&CK.
- CWE.
- CVE.
- CVSS.
- EPSS.
- CISA KEV.
- SOC 2 Trust Services Criteria.
- ISO/IEC 27001.
Distinguish:
1. MAPPED: Control mapped to a framework.
2. IMPLEMENTED_AND_EVIDENCED: Control implemented and supported by evidence.
3. INDEPENDENTLY_ASSESSED: Qualified independent assessment performed.
A framework mapping is not certification or compliance.

20. Implementation roadmap
1. Phase 0 — Reality and contract baseline
   
   Read MEC, inspect repository, map architecture, inventory capabilities, establish baseline tests, create threat model and compliance matrix.
   
   Exit: implemented behavior is distinguishable from plans and mocks.
2. Phase 1 — Identity, tenant, engagement, and scope
   
   Authentication, authorization, tenant isolation, engagement lifecycle, asset scope, exclusions, canonicalization, audit events.
   
   Exit: unauthorized and out-of-scope cases are rejected.
3. Phase 2 — Approval and execution control plane
   
   Approval workflow, operation-bound approval, durable queue, leases, cancellation, emergency stop, quotas.
   
   Exit: no operation executes without valid authorization.
4. Phase 3 — Isolated worker runtime
   
   Linux and Windows isolation, module registry, egress restrictions, secret delivery, worker cleanup.
   
   Exit: isolation is verified for every claimed environment.
5. Phase 4 — Evidence and finding quality
   
   Evidence ingestion, integrity, provenance, finding lifecycle, verification, retesting.
   
   Exit: findings have traceable evidence and reproducible verification.
6. Phase 5 — AI intelligence
   
   Provider abstraction, structured hypotheses, prompt injection defense, risk prioritization, cost tracking, offline evaluation.
   
   Exit: AI cannot bypass policy and provides measurable benefit over a non-AI baseline.
7. Phase 6 — User experience and integrations
   
   Operations console, approval inbox, evidence explorer, attack graph, reporting, integrations, accessibility.
   
   Exit: every control maps to a real operation.
8. Phase 7 — Benchmarking and hardening
   
   Versioned corpus, holdout scenarios, adversarial tests, resilience, performance, security review.
   
   Exit: no critical invariant failures.
9. Phase 8 — Production readiness
   
   Production deployment, backup restoration, observability, SBOM, release approval, documentation audit.
   
   Exit: all readiness gates have supporting evidence.

21. Prioritization
Priority	Workstream	Reason
P0	MEC compliance, identity, scope, policy, approval	Prevent unauthorized activity
P0	Worker isolation, egress, cancellation, emergency stop	Limit execution impact
P0	Audit, evidence integrity, tenant isolation	Establish trust
P1	Finding verification and reproducibility	Improve technical accuracy
P1	Test and benchmark corpus	Make quality measurable
P1	Documentation and capability registry	Prevent false claims
P2	AI hypothesis and analyst assistance	Improve productivity
P2	Attack graph and risk prioritization	Improve decision support
P2	GUI and integrations	Improve adoption
P3	Advanced learning and optimization	Requires validated data
P3	Broad provider/tool ecosystem	Expand through secure governance


22. Definition of done
A feature is incomplete until all applicable criteria are satisfied.
- [ ] MEC requirements identified and mapped.
- [ ] Architecture and threat implications reviewed.
- [ ] Domain and API contracts defined.
- [ ] Authentication and authorization enforced.
- [ ] Tenant boundaries enforced.
- [ ] Input validation and error handling implemented.
- [ ] Audit events implemented.
- [ ] Cancellation and timeout behavior defined.
- [ ] Unit tests added.
- [ ] Integration tests added where applicable.
- [ ] Security and negative tests added.
- [ ] UI tests added where applicable.
- [ ] Telemetry added without sensitive-data leakage.
- [ ] Documentation updated.
- [ ] Capability status accurately classified.
- [ ] Evidence references recorded.
- [ ] Known limitations documented.
- [ ] CI gates pass.
- [ ] No unresolved critical defects introduced.

23. Gold-standard release scorecard
A high average score must never conceal a critical security failure.
23.1 Non-negotiable release gates
Gate	Required state
MEC compliance	All mandatory requirements satisfied
Authorization	No unauthorized execution in required test corpus
Scope	No out-of-scope connection in required test corpus
Approval	Required operations blocked until approval
Tenant isolation	No cross-tenant access
Worker isolation	Verified in every claimed environment
Emergency stop	Proven end to end
Evidence integrity	Tampering detected
Auditability	Sensitive actions durably recorded
Critical defects	No unresolved release-blocking critical defects
Documentation	No knowingly false capability claims
Recovery	Backup restoration exercised
Release provenance	Required artifact evidence available

23.2 Maturity scale
Score each dimension from 0 to 5 with written evidence:
- Security architecture.
- Policy quality.
- Worker isolation.
- Detection accuracy.
- Verification accuracy.
- Evidence integrity.
- AI reliability.
- Operational resilience.
- Developer experience.
- Analyst usability.
- Integration quality.
- Documentation quality.
- Benchmark reproducibility.
- Release engineering.
Score	Definition
0	Absent
1	Designed
2	Implemented, not adequately verified
3	Tested in controlled environments
4	Operationally validated with monitoring and evidence
5	Independently assessed, continuously measured, maintained

A score of 5 requires independent assessment appropriate to the dimension.

24. Competitive differentiation
ARES should differentiate through verifiable outcomes:
1. Authorization transparency.
2. Evidence lineage.
3. Reproducible assessments.
4. Safe AI constrained by deterministic policy.
5. Verified cross-platform isolation.
6. Measured analyst efficiency.
7. Open integration contracts.
8. Honest capability catalog.
9. Explicit customer control.
10. Independent assurance.
Avoid competing on being more autonomous than everyone else.

Compete on more dependable, explainable, reproducible, and safer outcomes.

25. Mandatory engineering-agent instructions
The agent executing ARES MUST:
1. Read all of MEC.md before implementation.
2. Read all mandatory supporting specifications.
3. Discover and activate relevant installed engineering and security skills.
4. Inspect the actual repository before choosing implementation tasks.
5. Preserve existing architecture and security boundaries unless an authorized decision changes them.
6. Build requirement-to-code-to-test-to-evidence traceability.
7. Implement in dependency order.
8. Use real implementations rather than production mocks.
9. Keep all documentation synchronized.
10. Run actual builds, tests, simulations, and benchmarks.
11. Record exact commands, environments, and results.
12. Never invent results or claim unverified functionality.
13. Never weaken security controls to make tests pass.
14. Obtain human approval for external, intrusive, state-changing, and production operations.
15. Stop and report unclear authorization or security assumptions.
16. Maintain a durable execution ledger.
17. Verify every GUI control against its real execution path.
18. Complete the documentation-integrity audit.
19. Report incomplete work honestly.
20. Never declare production readiness without evidence for every mandatory gate.
The agent may autonomously inspect the repository, modify local code, run local tests, create documentation, and perform authorized lab simulations.

It must not autonomously scan third-party systems, change production infrastructure, access production secrets, or perform intrusive operations without explicit authorization.

26. Final governing standard
ARES becomes a gold-standard candidate only when its quality is demonstrated, not asserted.

The essential proof is:
- A valid engagement is required before action.
- Scope and policy are enforced independently of AI.
- Sensitive operations are authorized and auditable.
- Workers are isolated and constrained.
- Findings are backed by reproducible evidence.
- AI improves measurable analyst outcomes without gaining authority.
- Tests exercise expected and adversarial behavior.
- Benchmarks are versioned and reproducible.
- The UI reports actual system state.
- Documentation describes only real capabilities.
- Operations, recovery, and upgrades are tested.
- Independent assessment validates important claims.
The governing principle: ARES must never claim more capability, security, accuracy, or maturity than its implementation and evidence can prove.