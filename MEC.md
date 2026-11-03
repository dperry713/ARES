# ARES — Master Execution Contract (MEC)
## Production-Grade AI Penetration Testing Platform

**Document version:** 1.0  
**Status:** Approved implementation specification  
**Architecture:** Standalone modular platform  
**Execution autonomy:** AI-supervised, human-authorized  
**Target environments:** Web applications, APIs, network infrastructure, servers, cloud infrastructure  
**Primary objective:** Build an AI-driven penetration-testing platform capable of discovering, analyzing, prioritizing, safely validating, documenting, and continuously improving security assessments.

---

# 1. Executive Mission

Build ARES as a production-grade AI penetration-testing platform that automates the security assessment lifecycle.

ARES must combine:

- Intelligent reconnaissance.
- Asset discovery and fingerprinting.
- Vulnerability intelligence.
- Attack surface mapping.
- Attack-path analysis.
- AI-driven security hypothesis generation.
- Exploitability assessment.
- Controlled security test execution.
- Independent finding verification.
- Evidence collection.
- Human approval workflows.
- Continuous strategy evaluation.
- Professional security reporting.

The platform must increase the effectiveness of authorized security assessments by intelligently selecting which security tests to perform, when to perform them, what evidence to collect, and how to interpret their results.

ARES must not equate the number of executed tests with security effectiveness.

The primary success metric is the number of independently validated, reproducible, actionable security findings relative to testing time and operational risk.

## 1.1 Core engineering principle

AI reasoning is probabilistic.

Execution authorization, scope enforcement, policy evaluation, and security controls must be deterministic.

The AI must never directly control unrestricted operating-system processes, networks, cloud credentials, or arbitrary executable payloads.

All security testing must pass through the platform's authorization and execution-management layers.

## 1.2 Product objectives

1. Support multiple security assessment environments through a common execution framework.
2. Use AI to prioritize security testing based on observed evidence.
3. Improve testing efficiency through contextual vulnerability analysis.
4. Reduce false positives through independent verification.
5. Prevent unauthorized or out-of-scope testing.
6. Maintain reproducible, tamper-evident assessment records.
7. Support human approval at every security-sensitive execution boundary.
8. Learn from validated outcomes without allowing unreviewed model changes to affect production execution.
9. Provide an operational GUI for managing and monitoring assessments.
10. Support enterprise deployment, multi-tenancy, auditing, and extensibility.

# 2. Product Scope

ARES must support three primary assessment domains.

## 2.1 Web application security

Capabilities:

- HTTP/HTTPS endpoint discovery.
- Web application fingerprinting.
- Technology identification.
- TLS configuration assessment.
- Security header analysis.
- Cookie security analysis.
- Authentication configuration assessment.
- Authorization boundary testing using approved test identities.
- Session management analysis.
- Input validation testing.
- API schema discovery.
- API authorization assessment.
- Rate-limit configuration testing.
- CORS configuration assessment.
- Known vulnerable dependency detection.
- Safe vulnerability verification.

Security classifications:

- OWASP Top 10.
- OWASP API Security Top 10.
- CWE.
- CVE.
- CVSS.

Testing must use non-destructive verification techniques and authorized test identities.

## 2.2 Network and infrastructure security

Capabilities:

- Authorized host discovery.
- TCP service discovery.
- Service fingerprinting.
- Operating system fingerprinting where supported.
- TLS configuration inspection.
- SSH configuration assessment.
- DNS security analysis.
- Certificate validation.
- Exposed service identification.
- Network segmentation assessment.
- Firewall exposure analysis.
- Known vulnerable service identification.
- Configuration compliance assessment.
- Safe service validation.

The platform must distinguish between a discovered service, a suspected vulnerability, and a successfully verified security weakness.

## 2.3 Cloud infrastructure security

Support:

- AWS.
- Microsoft Azure.
- Google Cloud Platform.

Capabilities:

- Cloud asset inventory.
- IAM configuration analysis.
- Role and permission analysis.
- Public exposure detection.
- Storage security assessment.
- Network security configuration assessment.
- Security group and firewall analysis.
- Identity relationship mapping.
- Key and secret exposure metadata detection.
- Logging and monitoring configuration assessment.
- Encryption configuration assessment.
- Cloud workload inventory.
- Policy compliance assessment.
- Attack-path modeling based on authorized read-only configuration data.

Initial integrations should prioritize read-only cloud APIs.

Do not grant the AI unrestricted cloud administrative privileges.

## 2.4 Explicitly excluded autonomous capabilities

ARES must not autonomously perform:

- Credential theft.
- Credential harvesting.
- Password spraying.
- Persistence installation.
- Unauthorized lateral movement.
- Data exfiltration.
- Destructive exploitation.
- Ransomware-like actions.
- Unrestricted remote shell execution.
- Unrestricted exploit payload generation.
- Evasion of endpoint security.
- Security control disabling.
- Unauthorized privilege escalation.
- Production system modification.

Exploit research and validation should be conducted through safe, non-destructive checks or explicitly isolated, intentionally vulnerable laboratory environments.

# 3. System Architecture

Use a modular architecture with clear domain boundaries.

## 3.1 High-level architecture

```text
                         ARES PLATFORM
                               |
                 REACT OPERATIONS CONSOLE
                               |
                       API GATEWAY
                               |
                    IDENTITY / RBAC / ABAC
                               |
                    ASSESSMENT ORCHESTRATOR
                               |
          +--------------------+--------------------+
          |                    |                    |
     RECON ENGINE         AI PLANNER          INTELLIGENCE
          |                    |                    |
     ASSET INVENTORY      HYPOTHESIS          CVE / CWE
          |               GENERATION           CVSS / EPSS
     FINGERPRINTING       PRIORITIZATION       ADVISORIES
          |                    |                    |
          +--------------------+--------------------+
                               |
                        ATTACK GRAPH
                               |
                      POLICY ENGINE
                               |
                       APPROVAL SERVICE
                               |
                     EXECUTION MANAGER
                               |
                  ISOLATED WORKER POOL
                               |
                REGISTERED SECURITY MODULES
                               |
                   INDEPENDENT VERIFICATION
                               |
                      EVIDENCE ENGINE
                               |
                   FINDING CLASSIFICATION
                               |
                     LEARNING ENGINE
                               |
                    REPORTING ENGINE
                               |
                  POSTGRESQL / OBJECT STORE
```

## 3.2 Architectural boundaries

Implement these independently deployable or logically isolated components:

| Component | Responsibility |
|---|---|
| Ares.Api | External REST and WebSocket interfaces |
| Ares.Identity | Authentication, authorization, tenant management |
| Ares.Engagements | Assessment configuration and authorization |
| Ares.Recon | Asset discovery and fingerprinting |
| Ares.Intelligence | Vulnerability and security intelligence |
| Ares.AttackGraph | Relationship and attack-path analysis |
| Ares.AI | Model integration and planning |
| Ares.Policy | Deterministic authorization |
| Ares.Approvals | Human approval workflows |
| Ares.Execution | Job scheduling and execution management |
| Ares.Worker | Isolated security test execution |
| Ares.Modules | Security test module registry |
| Ares.Verification | Independent finding verification |
| Ares.Evidence | Evidence storage and integrity |
| Ares.Learning | Historical performance and strategy evaluation |
| Ares.Reporting | Technical and executive reporting |
| Ares.Notifications | Operational notifications |
| Ares.Web | React frontend |

Start with a modular monolith for the management plane if that simplifies initial implementation. Keep worker execution, credentials, policy enforcement, and AI-provider integrations isolated behind stable interfaces.

Do not prematurely distribute every domain into a microservice.

# 4. Technology Stack

Use the following baseline.

| Layer | Technology |
|---|---|
| Backend | .NET 10 / ASP.NET Core |
| Domain architecture | Clean Architecture |
| AI orchestration | Python 3.13+ |
| Execution runtime | Rust stable |
| Frontend | React + TypeScript |
| UI components | Accessible component system |
| Database | PostgreSQL |
| Database access | EF Core / Npgsql |
| Cache | Redis |
| Event processing | Transactional outbox + Redis Streams initially |
| Graph storage | Neo4j, optional initial deployment |
| Evidence storage | S3-compatible object storage |
| AI providers | OpenAI, Anthropic, Google, Azure AI, local models |
| Policy engine | Open Policy Agent / Rego |
| Containers | Docker |
| Orchestration | Kubernetes |
| Observability | OpenTelemetry |
| Logging | Serilog |
| Metrics | Prometheus |
| Visualization | React Flow / graph visualization |
| API documentation | OpenAPI |
| Authentication | OIDC / OAuth 2.0 |
| Testing | xUnit, pytest, cargo test, Vitest, Playwright |

Use currently supported stable dependency versions and commit dependency lockfiles.

.NET 10 is the primary backend baseline. Do not mix incompatible framework versions without a documented compatibility decision.

# 5. Repository Structure

Create the following repository structure.

```text
ares/
│
├── src/
│   ├── backend/
│   │   ├── Ares.Api/
│   │   ├── Ares.Domain/
│   │   ├── Ares.Application/
│   │   ├── Ares.Contracts/
│   │   ├── Ares.Infrastructure/
│   │   ├── Ares.Identity/
│   │   ├── Ares.Engagements/
│   │   ├── Ares.Recon/
│   │   ├── Ares.Intelligence/
│   │   ├── Ares.AttackGraph/
│   │   ├── Ares.Policy/
│   │   ├── Ares.Approvals/
│   │   ├── Ares.Execution/
│   │   ├── Ares.Verification/
│   │   ├── Ares.Evidence/
│   │   ├── Ares.Learning/
│   │   ├── Ares.Reporting/
│   │   ├── Ares.Notifications/
│   │   └── Ares.Hosting/
│   │
│   ├── ai/
│   │   ├── orchestrator/
│   │   ├── providers/
│   │   ├── planner/
│   │   ├── intelligence/
│   │   ├── evaluation/
│   │   ├── schemas/
│   │   └── tests/
│   │
│   ├── runtime/
│   │   ├── worker/
│   │   ├── sandbox/
│   │   ├── module-host/
│   │   ├── network-policy/
│   │   └── tests/
│   │
│   └── frontend/
│       ├── src/
│       │   ├── app/
│       │   ├── components/
│       │   ├── features/
│       │   ├── hooks/
│       │   ├── services/
│       │   ├── state/
│       │   ├── types/
│       │   └── tests/
│       └── e2e/
│
├── modules/
│   ├── web/
│   ├── network/
│   ├── cloud/
│   ├── configuration/
│   └── compliance/
│
├── policies/
│   ├── authorization/
│   ├── engagement/
│   ├── execution/
│   ├── tenant/
│   └── approval/
│
├── benchmarks/
│   ├── fixtures/
│   ├── scenarios/
│   ├── ground-truth/
│   ├── scoring/
│   └── reports/
│
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── contract/
│   ├── security/
│   ├── architecture/
│   ├── performance/
│   ├── resilience/
│   └── end-to-end/
│
├── deploy/
│   ├── docker/
│   ├── compose/
│   ├── kubernetes/
│   ├── helm/
│   ├── terraform/
│   └── monitoring/
│
├── docs/
│   ├── architecture/
│   ├── security/
│   ├── api/
│   ├── operations/
│   ├── development/
│   └── decisions/
│
├── scripts/
├── .github/
│   └── workflows/
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── docker-compose.yml
├── README.md
├── SECURITY.md
├── CONTRIBUTING.md
└── LICENSE
```

All components must contain real implementations. Do not create empty projects, fake integrations, placeholder implementations, or nonfunctional UI components.

# 6. Domain Model

Use strongly typed identifiers and explicit state transitions.

## 6.1 Core entities

### Tenant

Represents an isolated customer or organization.

Properties:

- TenantId.
- Name.
- Status.
- CreatedAt.
- Configuration.
- RetentionPolicy.

### User

Properties:

- UserId.
- TenantId.
- ExternalIdentity.
- RoleAssignments.
- Status.
- CreatedAt.

### Engagement

Represents an authorized security assessment.

Properties:

- EngagementId.
- TenantId.
- Name.
- Description.
- AuthorizationReference.
- StartAt.
- EndAt.
- ScopeDefinition.
- AllowedAssessmentTypes.
- ExcludedTargets.
- MaximumConcurrency.
- RateLimits.
- ApprovalRequirements.
- Status.

### Asset

Properties:

- AssetId.
- TenantId.
- EngagementId.
- AssetType.
- CanonicalIdentifier.
- NetworkLocation.
- TechnologyFingerprint.
- DiscoverySource.
- FirstObservedAt.
- LastObservedAt.
- Confidence.

### Vulnerability

Properties:

- VulnerabilityId.
- AssetId.
- CveId.
- CweId.
- CvssScore.
- EpssScore.
- Evidence.
- AffectedComponent.
- VersionEvidence.
- Status.

### TestHypothesis

Properties:

- HypothesisId.
- EngagementId.
- AssetId.
- VulnerabilityId.
- ProposedModule.
- Prerequisites.
- EvidenceReferences.
- Confidence.
- Priority.
- ProposedBy.
- Status.

### Execution

Properties:

- ExecutionId.
- EngagementId.
- HypothesisId.
- ModuleId.
- ModuleVersion.
- ApprovalId.
- PolicyDecisionId.
- Status.
- StartedAt.
- CompletedAt.
- WorkerId.
- ResultReference.

### Finding

Properties:

- FindingId.
- EngagementId.
- AssetId.
- VulnerabilityId.
- Severity.
- Confidence.
- VerificationStatus.
- Description.
- Impact.
- Remediation.
- EvidenceReferences.
- FirstObservedAt.
- LastObservedAt.

### Approval

Properties:

- ApprovalId.
- ExecutionId.
- RequestedBy.
- ApprovedBy.
- ApprovalScope.
- ApprovedAt.
- ExpiresAt.
- Decision.
- Reason.

### Evidence

Properties:

- EvidenceId.
- TenantId.
- EngagementId.
- ExecutionId.
- EvidenceType.
- ObjectReference.
- SHA256.
- CollectedAt.
- Classification.
- Provenance.

## 6.2 State machines

Engagement:

```text
Draft
  |
PendingAuthorization
  |
Authorized
  |
Active
  |
Paused
  |
Completed
  |
Archived
```

Execution:

```text
Proposed
  |
AwaitingApproval
  |
Approved
  |
Queued
  |
Running
  |
Verifying
  |
Completed
```

Alternative terminal states:

```text
Rejected
Cancelled
Expired
PolicyDenied
Failed
TimedOut
VerificationFailed
```

Transitions must be validated by domain services.

Never permit arbitrary state updates from the frontend.

# 7. Security Module Contract

Create a common module interface.

Every module must be signed, versioned, registered, independently tested, and declared with an explicit capability manifest.

## 7.1 Module metadata

```csharp
public sealed record SecurityModuleManifest(
    string ModuleId,
    string Version,
    string Name,
    string Description,
    string Domain,
    string[] SupportedAssetTypes,
    string[] RequiredCapabilities,
    string[] RequiredPermissions,
    string SafetyClassification,
    string[] RequiredEvidenceTypes,
    bool RequiresHumanApproval);
```

The module registry must additionally retain:

- Artifact digest.
- Publisher identity.
- Signature.
- Build provenance.
- Dependency manifest.
- Compatibility metadata.
- Verification requirements.
- Supported runtime versions.

## 7.2 Module execution interface

```csharp
public interface ISecurityTestModule
{
    SecurityModuleManifest Manifest { get; }

    Task<ModuleValidationResult> ValidateAsync(
        SecurityTestContext context,
        CancellationToken cancellationToken);

    Task<SecurityTestResult> ExecuteAsync(
        SecurityTestContext context,
        CancellationToken cancellationToken);

    Task<VerificationResult> VerifyAsync(
        SecurityTestContext context,
        SecurityTestResult result,
        CancellationToken cancellationToken);
}
```

Module execution must not provide unrestricted access to the host environment.

`SecurityTestContext` must contain only the approved target, narrowly scoped credentials where applicable, execution limits, approved capabilities, and evidence collection configuration.

Do not pass unrestricted cloud credentials, environment variables, host filesystem access, or arbitrary shell capabilities into modules.

## 7.3 Initial module categories

Web:

- TLS assessment.
- HTTP security headers.
- Cookie configuration.
- CORS configuration.
- API schema validation.
- Authentication configuration checks.
- Authorization boundary checks.
- Dependency vulnerability assessment.
- Safe input-validation tests.

Network:

- Service inventory.
- TLS configuration assessment.
- SSH configuration assessment.
- DNS configuration assessment.
- Certificate assessment.
- Network exposure checks.
- Known vulnerable version correlation.

Cloud:

- IAM policy analysis.
- Public storage exposure.
- Security group configuration.
- Network ACL analysis.
- Logging configuration.
- Encryption configuration.
- Cloud asset inventory.
- Identity relationship analysis.

All modules must have explicit test limits and independent verification logic.

Exploit-oriented validation should initially be limited to harmless proof-of-condition checks and isolated, intentionally vulnerable test environments.

# 8. AI Orchestration Architecture

The AI subsystem must be independent of the execution subsystem.

## 8.1 AI responsibilities

AI can:

- Interpret reconnaissance results.
- Correlate vulnerability intelligence.
- Identify missing evidence.
- Generate security hypotheses.
- Rank test candidates.
- Explain likely attack paths.
- Recommend registered security modules.
- Interpret test outcomes.
- Suggest remediation.
- Identify inconsistencies.
- Recommend additional safe verification.

AI cannot:

- Directly execute arbitrary shell commands.
- Bypass policy decisions.
- Modify engagement scope.
- Approve its own actions.
- Access unrestricted credentials.
- Alter evidence records.
- Change production policies.
- Promote its own model or strategy versions.

## 8.2 Provider abstraction

```python
from typing import Protocol, Any


class AIProvider(Protocol):

    async def generate(
        self,
        system_prompt: str,
        input_data: dict[str, Any],
        output_schema: dict[str, Any],
    ) -> dict[str, Any]:
        ...

    async def health_check(self) -> bool:
        ...
```

Implement provider adapters for:

- OpenAI.
- Anthropic.
- Google.
- Azure AI.
- Local model providers.

All provider responses must pass schema validation.

Use structured outputs rather than free-form executable instructions.

## 8.3 Planning pipeline

```text
Asset Inventory
      |
Vulnerability Intelligence
      |
Evidence Correlation
      |
Context Construction
      |
AI Hypothesis Generation
      |
Schema Validation
      |
Prerequisite Evaluation
      |
Risk Ranking
      |
Deterministic Policy Evaluation
      |
Human Approval
      |
Execution Manager
```

Every hypothesis must include:

- Hypothesis identifier.
- Target asset.
- Suspected weakness.
- Supporting evidence.
- Evidence freshness.
- Required prerequisites.
- Confidence estimate.
- Potential impact.
- Proposed safe test.
- Expected verification result.
- Risk classification.
- Explanation.
- References to intelligence sources.

## 8.4 AI planning safeguards

Treat all scanned content, retrieved webpages, service banners, API responses, and discovered files as untrusted input.

Implement:

- Prompt-injection-resistant context handling.
- Tool allowlisting.
- Strict JSON schema validation.
- Model output size limits.
- Model timeout controls.
- Provider rate limiting.
- Input sanitization.
- Output provenance.
- Retrieval source attribution.
- Model version tracking.
- Human-readable planning explanations.

Never interpret instructions found on a target system as authorization to execute additional actions.

# 9. Exploitability and Prioritization Engine

Implement an evidence-based ranking system.

Use the following initial features:

- Environment compatibility.
- Reachability.
- Prerequisite satisfaction.
- Vulnerability evidence quality.
- Asset exposure.
- Vulnerability intelligence.
- Historical validation performance.
- Assessment risk.

Initial ranking model:

```text
Priority =
    0.20 * Compatibility
  + 0.20 * Reachability
  + 0.15 * PrerequisiteConfidence
  + 0.15 * EvidenceQuality
  + 0.10 * Exposure
  + 0.10 * IntelligenceConfidence
  + 0.10 * HistoricalEffectiveness
```

Normalize features to a documented scale.

These are prioritization scores, not probabilities of successful exploitation.

Maintain separate fields for:

- Vulnerability existence confidence.
- Exploitability confidence.
- Test execution success.
- Verification confidence.
- Business impact.

Never merge these into one ambiguous confidence score.

The model must be evaluated against ground-truth benchmark datasets before its rankings can influence production prioritization.

# 10. Deterministic Policy Engine

Implement OPA/Rego as an independent policy decision service.

The policy engine must evaluate every proposed execution.

## 10.1 Required checks

1. Engagement authorization is valid.
2. Engagement is active.
3. Target belongs to authorized scope.
4. Target does not match an exclusion.
5. Requested test category is permitted.
6. Module signature is valid.
7. Module version is approved.
8. Required capabilities are available.
9. Human approval exists where required.
10. Approval has not expired.
11. Execution limits have not been exceeded.
12. Tenant is active.
13. Worker environment satisfies the security profile.
14. Global emergency-stop state is inactive.

## 10.2 Policy result

```json
{
  "decision": "deny",
  "policy_version": "1.0.0",
  "reason_code": "APPROVAL_REQUIRED",
  "explanations": [
    "The requested test requires human approval."
  ],
  "evaluated_at": "2026-11-01T12:00:00Z"
}
```

Use three decisions:

- Permit.
- Deny.
- RequireApproval.

A missing policy response, policy-engine timeout, invalid signature, or malformed decision must fail closed.

## 10.3 Human approval

Every invasive or potentially disruptive test requires explicit human approval.

The approval must be bound to:

- Exact engagement.
- Exact asset.
- Exact module identifier.
- Exact module version.
- Exact test parameters.
- Execution limits.
- Approval expiration.

Any material change invalidates the approval.

Approval must be recorded with the approving user's identity and an immutable audit event.

AI-generated approval is prohibited.

# 11. Isolated Execution Runtime

Implement the worker runtime in Rust.

The execution manager must never execute module code inside the API process.

## 11.1 Worker architecture

```text
Execution Manager
       |
Policy Decision
       |
Approval Verification
       |
Worker Provisioning
       |
Capability Assignment
       |
Isolated Worker
       |
Registered Module
       |
Result Collection
       |
Independent Verification
       |
Worker Destruction
```

## 11.2 Isolation requirements

Linux:

- Rootless containers where possible.
- Non-root user.
- Read-only root filesystem.
- Dropped Linux capabilities.
- Seccomp.
- AppArmor or SELinux.
- Resource quotas.
- Network egress allowlists.
- No Docker socket mounting.
- No privileged containers.
- Ephemeral worker filesystem.

Windows:

- Windows Job Objects.
- Restricted tokens.
- Separate worker identities.
- Named-pipe IPC with ACLs.
- Explicit process creation permissions.
- Resource limits.
- Controlled filesystem access.
- Network restrictions enforced outside the worker process.

Kubernetes:

- Dedicated worker namespace.
- NetworkPolicies.
- Pod Security Standards.
- ResourceQuota.
- LimitRange.
- ServiceAccount restrictions.
- Short-lived workload identities.

## 11.3 Execution controls

Every worker must enforce:

- Execution timeout.
- CPU limit.
- Memory limit.
- Process count limit.
- Output size limit.
- Request rate limit.
- Network destination restrictions.
- Maximum concurrent executions.
- Cancellation support.
- Automatic destruction after completion.

The worker must receive only the credentials and capabilities required for the specific authorized assessment.

## 11.4 Kill switch

Implement an emergency stop that:

1. Prevents new executions.
2. Cancels queued executions.
3. Terminates active workers.
4. Revokes worker credentials.
5. Records the emergency event.
6. Preserves collected evidence.
7. Notifies authorized operators.

The kill switch must work independently of the AI subsystem.

# 12. Engagement and Authorization Management

This is a foundational subsystem.

Before testing begins, the customer must establish an engagement.

Required information:

- Organization.
- Authorized operator.
- Authorization reference.
- Assessment start and expiration.
- Authorized assets.
- Excluded assets.
- Permitted assessment types.
- Maximum request rates.
- Maximum concurrent workers.
- Permitted testing windows.
- Human approval requirements.
- Emergency contacts.

## 12.1 Scope enforcement

Implement canonical target validation.

For network assets:

- Validate IP addresses and CIDRs.
- Normalize IPv4 and IPv6.
- Resolve DNS under controlled conditions.
- Validate resolved addresses against scope.
- Prevent DNS rebinding and resolution changes from bypassing scope.
- Revalidate immediately before connection.

For web assets:

- Normalize schemes, hosts, ports and paths.
- Prevent redirects outside authorized scope.
- Validate every redirect destination.
- Prevent IP literal and hostname canonicalization bypasses.
- Restrict DNS resolution to authorized destinations.

For cloud:

- Bind assessments to verified cloud account and subscription identifiers.
- Validate resource ownership.
- Require explicit account and region scope.
- Use read-only cloud identities by default.

An engagement scope must not be inferred from AI reasoning.

# 13. Evidence and Verification

Evidence is a first-class product capability.

A finding is not considered verified merely because an AI model says it is exploitable.

## 13.1 Evidence requirements

Each evidence item must include:

- Evidence identifier.
- Assessment identifier.
- Asset identifier.
- Execution identifier.
- Module version.
- Collection timestamp.
- Evidence type.
- Cryptographic digest.
- Collection provenance.
- Verification relationship.
- Data classification.

Evidence storage must use encryption at rest.

Use content hashing and immutable audit references to detect modification.

Sensitive response content must be redacted or minimized before storage.

## 13.2 Verification levels

```text
Unverified
    |
Suspected
    |
EvidenceCollected
    |
VerificationInProgress
    |
Verified
```

Alternative states:

- FalsePositive.
- Inconclusive.
- NotReproducible.
- Remediated.
- AcceptedRisk.

Verification should be performed independently from AI hypothesis generation wherever practical.

## 13.3 Finding severity

Support:

- Critical.
- High.
- Medium.
- Low.
- Informational.

Use CVSS as a vulnerability severity reference, not as a replacement for business-risk analysis.

Incorporate:

- Asset criticality.
- Exposure.
- Exploitability evidence.
- Business impact.
- Existing compensating controls.

# 14. Learning and Continuous Improvement

Implement a separate learning subsystem.

The system should improve testing strategy selection based on independently verified historical outcomes.

## 14.1 Learning data

Capture:

- Proposed test.
- Supporting evidence.
- Predicted confidence.
- Actual outcome.
- Verification outcome.
- Execution duration.
- Resource consumption.
- False-positive classification.
- Target compatibility.
- Module version.
- AI model version.
- Strategy version.

## 14.2 Learning pipeline

```text
Assessment Outcomes
       |
Outcome Validation
       |
Historical Dataset
       |
Performance Analysis
       |
Strategy Candidate
       |
Offline Benchmark
       |
Regression Evaluation
       |
Human Review
       |
Approved Promotion
       |
Production Strategy
```

Do not permit automatic changes to security policy or execution permissions.

Learning can optimize prioritization, evidence selection, and safe test recommendations.

It cannot expand the platform's permitted capabilities.

## 14.3 Required metrics

- Finding precision.
- Finding recall.
- False-positive rate.
- Verification success rate.
- Mean time to verified finding.
- Assessment coverage.
- Hypothesis ranking quality.
- AI provider latency.
- Execution failure rate.
- Policy denial rate.
- Approval turnaround.
- Cost per verified finding.

Measure these against a reproducible baseline.

# 15. API Contract

Use versioned REST endpoints under `/api/v1`.

## 15.1 Authentication

Use OIDC/OAuth 2.0.

Support:

- User authentication.
- Service-to-service authentication.
- Tenant isolation.
- Role-based access control.
- Attribute-based authorization.
- Short-lived access tokens.
- Secure refresh-token handling.

Suggested roles:

- PlatformAdministrator.
- TenantAdministrator.
- SecurityLead.
- SecurityOperator.
- Approver.
- Auditor.
- ReadOnlyViewer.

## 15.2 Engagement endpoints

```http
POST   /api/v1/engagements
GET    /api/v1/engagements
GET    /api/v1/engagements/{id}
PATCH  /api/v1/engagements/{id}
POST   /api/v1/engagements/{id}/authorize
POST   /api/v1/engagements/{id}/start
POST   /api/v1/engagements/{id}/pause
POST   /api/v1/engagements/{id}/resume
POST   /api/v1/engagements/{id}/cancel
```

## 15.3 Asset endpoints

```http
GET    /api/v1/assets
GET    /api/v1/assets/{id}
GET    /api/v1/assets/{id}/vulnerabilities
GET    /api/v1/assets/{id}/history
```

## 15.4 Reconnaissance endpoints

```http
POST   /api/v1/engagements/{id}/recon
GET    /api/v1/engagements/{id}/recon
GET    /api/v1/recon/{id}
```

## 15.5 AI planning endpoints

```http
POST   /api/v1/engagements/{id}/plans
GET    /api/v1/plans/{id}
GET    /api/v1/plans/{id}/hypotheses
POST   /api/v1/hypotheses/{id}/review
```

Planning endpoints generate recommendations, not execution authority.

## 15.6 Approval endpoints

```http
GET    /api/v1/approvals
GET    /api/v1/approvals/{id}
POST   /api/v1/approvals/{id}/approve
POST   /api/v1/approvals/{id}/reject
```

## 15.7 Execution endpoints

```http
POST   /api/v1/executions
GET    /api/v1/executions
GET    /api/v1/executions/{id}
POST   /api/v1/executions/{id}/cancel
GET    /api/v1/executions/{id}/events
```

Execution creation must require a valid policy decision and applicable approval.

## 15.8 Finding endpoints

```http
GET    /api/v1/findings
GET    /api/v1/findings/{id}
GET    /api/v1/findings/{id}/evidence
POST   /api/v1/findings/{id}/review
POST   /api/v1/findings/{id}/resolve
```

## 15.9 Reporting endpoints

```http
POST   /api/v1/reports
GET    /api/v1/reports
GET    /api/v1/reports/{id}
GET    /api/v1/reports/{id}/download
```

Support PDF, JSON, SARIF and CSV exports.

## 15.10 API requirements

Every API must support:

- Input validation.
- Pagination.
- Filtering.
- Sorting.
- Standardized errors.
- Correlation identifiers.
- Idempotency for appropriate POST operations.
- Rate limiting.
- Authorization.
- Audit events.
- OpenAPI documentation.

Use RFC 9457 Problem Details for standardized errors.

WebSocket events should communicate execution progress, approval changes, findings, and worker health.

# 16. Frontend and Operations Console

Build a complete React + TypeScript application.

The GUI must be operational, not a static visualization.

## 16.1 Dashboard

Display:

- Active engagements.
- Authorized assets.
- Discovered assets.
- Identified vulnerabilities.
- Verified findings.
- Pending approvals.
- Active executions.
- Worker availability.
- Recent security events.
- Assessment coverage.
- AI planning activity.

## 16.2 Engagement management

Features:

- Create engagement.
- Define target scope.
- Configure excluded assets.
- Upload authorization documentation.
- Set assessment windows.
- Configure execution limits.
- Configure approval requirements.
- View engagement history.
- Pause and terminate assessments.

## 16.3 Asset explorer

Display:

- Host inventory.
- Applications.
- API endpoints.
- Cloud resources.
- Service fingerprints.
- Vulnerability associations.
- Asset relationships.
- Exposure information.
- Discovery history.

## 16.4 Attack graph visualization

Implement an interactive graph.

Nodes:

- Assets.
- Services.
- Identities.
- Vulnerabilities.
- Network boundaries.
- Cloud resources.
- Findings.

Edges:

- Hosts.
- Exposes.
- DependsOn.
- AccessibleThrough.
- Runs.
- AssociatedWith.
- PotentiallyReachable.

Distinguish observed relationships from inferred relationships.

AI-generated relationships must be visibly labeled as hypotheses.

## 16.5 AI planning interface

Display:

- Proposed testing hypotheses.
- Supporting evidence.
- Confidence.
- Priority.
- Prerequisites.
- Potential impact.
- Proposed module.
- Estimated risk.
- Explanation.
- Approval requirement.

Allow operators to accept, reject, defer, or request further analysis.

## 16.6 Approval center

Display:

- Requested operation.
- Target.
- Module.
- Parameters.
- Risk classification.
- Authorization.
- Policy result.
- Resource limits.
- Expiration.
- Approving identity.

Provide approve/reject actions with confirmation and audit recording.

## 16.7 Execution center

Real-time status:

- Queued.
- Awaiting approval.
- Provisioning.
- Running.
- Verifying.
- Completed.
- Failed.
- Cancelled.

Display:

- Worker identifier.
- Current module.
- Progress events.
- Execution duration.
- Resource utilization.
- Evidence collection status.
- Errors.
- Cancellation control.

Never show fabricated progress or synthetic execution status.

All visualization data must originate from actual backend state or be explicitly identified as simulated.

## 16.8 Findings center

Provide:

- Severity filters.
- Asset filters.
- Confidence filters.
- Verification status.
- Evidence viewer.
- Reproduction instructions limited to safe verification.
- Remediation guidance.
- Finding history.
- Export.

## 16.9 Reporting center

Generate:

- Executive summaries.
- Technical reports.
- Vulnerability reports.
- Asset inventory reports.
- Attack-path reports.
- Compliance reports.
- Remediation tracking.
- Assessment comparison reports.

# 17. Security Architecture

Implement defense in depth.

## 17.1 Identity security

- OIDC authentication.
- MFA support through the identity provider.
- RBAC.
- ABAC.
- Tenant isolation.
- Short-lived tokens.
- Service identity.
- Credential rotation.

## 17.2 Secret management

Never store raw credentials in ordinary application records.

Use a dedicated secret manager.

Support:

- Azure Key Vault.
- AWS Secrets Manager.
- HashiCorp Vault.
- Kubernetes Secrets through an appropriate external secrets integration.

Prefer workload identity and short-lived credentials.

AI prompts must never contain raw credentials.

## 17.3 Data protection

- TLS for all network communications.
- Encryption at rest.
- Tenant-scoped storage.
- Evidence access controls.
- Sensitive-data redaction.
- Configurable retention.
- Secure deletion workflows.
- Audit retention.

## 17.4 Application security

Implement:

- SSRF protections.
- DNS rebinding defenses.
- Redirect validation.
- SQL parameterization.
- XSS protection.
- CSRF protection where applicable.
- Secure CORS.
- Request size limits.
- Rate limiting.
- Dependency scanning.
- Container image scanning.
- SBOM generation.

## 17.5 Audit integrity

Create append-only audit records.

Record:

- User actions.
- Engagement changes.
- Authorization decisions.
- Approval decisions.
- Policy decisions.
- Module registration.
- Execution lifecycle.
- Evidence collection.
- Finding modifications.
- Learning promotions.

Use cryptographic chaining or signed audit checkpoints to make tampering detectable.

# 18. Observability and Reliability

Implement OpenTelemetry across backend, AI services, and workers.

Required signals:

### Logs

Structured JSON logs with:

- Timestamp.
- Service.
- Tenant identifier.
- Engagement identifier.
- Correlation identifier.
- Execution identifier.
- Severity.
- Event type.

Never log secrets or unredacted sensitive response bodies.

### Metrics

```text
ares_engagements_active
ares_assets_discovered_total
ares_hypotheses_generated_total
ares_findings_verified_total
ares_execution_started_total
ares_execution_failed_total
ares_policy_denials_total
ares_approvals_pending
ares_workers_active
ares_worker_crashes_total
ares_ai_provider_latency_seconds
ares_ai_token_usage_total
ares_verification_duration_seconds
```

### Health checks

Implement:

- Liveness.
- Readiness.
- PostgreSQL connectivity.
- Redis connectivity.
- AI provider health.
- Policy engine health.
- Worker availability.
- Object storage health.

A failed policy engine must prevent new executions, not silently bypass policy evaluation.

### Resilience

Implement:

- Bounded retries.
- Exponential backoff.
- Circuit breakers.
- Dead-letter queues.
- Idempotent event processing.
- Transactional outbox.
- Graceful shutdown.
- Worker lease expiration.
- Execution recovery.

Do not retry potentially disruptive tests blindly.

# 19. Testing Strategy

Testing must be comprehensive and enforceable.

## 19.1 Unit tests

Test every domain service and critical business rule.

Examples:

- Scope validation.
- Approval expiration.
- Engagement authorization.
- Tenant isolation.
- Policy decision handling.
- Module compatibility.
- Finding classification.
- Evidence integrity.
- State transitions.

## 19.2 Integration tests

Validate:

- PostgreSQL persistence.
- Redis event processing.
- OPA integration.
- AI provider adapters.
- Object storage.
- Worker communication.
- Authentication.
- API authorization.

Use real service containers where practical.

## 19.3 Security tests

Required scenarios:

- Unauthorized target rejection.
- Out-of-scope DNS resolution.
- DNS rebinding.
- Redirect-to-out-of-scope.
- IPv4 and IPv6 normalization.
- Tenant isolation violations.
- Approval replay.
- Approval expiration.
- Module signature failure.
- Malformed AI responses.
- Prompt injection through discovered content.
- Malicious module metadata.
- Evidence tampering.
- Secret leakage.
- Worker escape attempts.
- Resource exhaustion.
- Emergency-stop behavior.

## 19.4 End-to-end tests

Build complete workflows:

1. Create tenant.
2. Create user.
3. Create engagement.
4. Configure authorized laboratory assets.
5. Validate authorization.
6. Discover assets.
7. Generate vulnerability intelligence.
8. Generate AI hypotheses.
9. Review recommendations.
10. Approve a safe security test.
11. Execute the test.
12. Verify the result.
13. Store evidence.
14. Generate a report.
15. Audit the entire workflow.

## 19.5 Coverage requirements

Establish the following initial gates:

| Area | Required threshold |
|---|---:|
| Domain unit coverage | 90% |
| Application unit coverage | 85% |
| Policy rule tests | 100% of defined rules |
| Security regression tests | 100% pass |
| API contract tests | 100% pass |
| Critical execution paths | 100% covered by tests |
| Frontend component coverage | 80% |
| End-to-end critical workflows | 100% pass |

Coverage percentages are necessary but insufficient. Critical security properties require explicit tests even when line coverage is already high.

# 20. Benchmark and Simulation Framework

This is a major differentiator.

Build a dedicated benchmark system that measures whether ARES actually improves penetration-testing effectiveness.

## 20.1 Benchmark categories

### Web

Use isolated, intentionally vulnerable applications with documented ground truth.

Evaluate:

- Vulnerability identification.
- API authorization analysis.
- Misconfiguration detection.
- Evidence quality.
- False-positive resistance.

### Network

Use isolated virtual networks.

Evaluate:

- Service discovery.
- Fingerprinting accuracy.
- Exposure classification.
- Configuration detection.
- Scope enforcement.

### Cloud

Use dedicated cloud test accounts or simulated configuration fixtures.

Evaluate:

- IAM analysis.
- Public exposure detection.
- Misconfiguration detection.
- Identity relationship analysis.
- Resource attribution.

### AI adversarial benchmarks

Test:

- Prompt injection.
- Malicious tool output.
- Poisoned vulnerability metadata.
- Conflicting intelligence.
- Fabricated CVE references.
- Unsupported AI conclusions.
- Malformed structured output.
- Excessive model confidence.

## 20.2 Ground truth

Every benchmark scenario must define:

- Scenario ID.
- Environment.
- Known assets.
- Known vulnerabilities.
- Known secure configurations.
- Expected findings.
- Expected non-findings.
- Required evidence.
- Permitted operations.
- Expected policy decisions.

Do not train or evaluate against datasets where the ground truth is unknown.

## 20.3 Required benchmark metrics

| Metric | Purpose |
|---|---|
| Precision | Percentage of reported findings that are correct |
| Recall | Percentage of known findings detected |
| F1 | Balance between precision and recall |
| False-positive rate | Incorrect vulnerability identification |
| Verification accuracy | Correctness of finding validation |
| Time to finding | Assessment efficiency |
| Coverage | Percentage of in-scope assets assessed |
| Ranking quality | Whether high-value findings appear earlier |
| Cost per verified finding | Economic efficiency |
| Policy violation rate | Authorization and scope enforcement |

## 20.4 AI strategy comparisons

Compare:

- Baseline deterministic scanner.
- AI-assisted prioritization.
- AI-assisted evidence correlation.
- Combined AI planning.

Use identical scenarios and fixed test budgets.

The AI must demonstrate measurable improvement over the baseline without reducing security-policy compliance or increasing false-positive rates beyond defined tolerances.

# 21. CI/CD and Repository Guardrails

Use GitHub Actions.

Every pull request must execute the applicable quality gates.

## 21.1 CI pipeline

```text
Pull Request
     |
Formatting
     |
Static Analysis
     |
Dependency Audit
     |
Secret Scanning
     |
Backend Build
     |
Backend Tests
     |
Python Tests
     |
Rust Tests
     |
Frontend Build
     |
Frontend Tests
     |
Architecture Tests
     |
Security Tests
     |
Container Build
     |
SBOM Generation
     |
Image Scanning
     |
Integration Tests
     |
Benchmark Regression
     |
Merge Eligibility
```

## 21.2 Required tools

.NET:

- dotnet format.
- Roslyn analyzers.
- xUnit.
- Coverlet.

Python:

- Ruff.
- mypy or Pyright.
- pytest.
- pip-audit.

Rust:

- cargo fmt.
- cargo clippy.
- cargo test.
- cargo audit.

Frontend:

- ESLint.
- TypeScript strict mode.
- Vitest.
- Playwright.

Security:

- CodeQL.
- Gitleaks.
- Trivy.
- Semgrep.
- Dependency review.

Infrastructure:

- Checkov.
- Conftest.
- Container image scanning.

## 21.3 Branch protection

Require:

- Pull requests.
- Successful CI.
- Required reviewers.
- No direct pushes to protected branches.
- No bypass of mandatory security checks.
- Signed release artifacts.
- Versioned database migrations.

Never mark a CI job successful when the actual test process failed.

## 21.4 Release pipeline

```text
Main Branch
    |
Versioning
    |
Build
    |
Test
    |
Security Scan
    |
SBOM
    |
Artifact Signing
    |
Container Registry
    |
Staging Deployment
    |
Smoke Tests
    |
Approval
    |
Production Deployment
    |
Health Verification
```

# 22. Deployment Architecture

Support two initial deployment profiles.

## 22.1 Developer deployment

Docker Compose:

- API.
- Frontend.
- PostgreSQL.
- Redis.
- OPA.
- AI orchestrator.
- Worker runtime.
- Object storage.
- OpenTelemetry collector.

Include seeded development data and intentionally vulnerable local benchmark targets.

Development environments must not automatically authorize external targets.

## 22.2 Production deployment

Kubernetes:

```text
             INGRESS
                |
           API GATEWAY
                |
        MANAGEMENT CLUSTER
                |
    +-----------+-----------+
    |           |           |
  API       AI SERVICE    POLICY
    |           |           |
    +-----------+-----------+
                |
          EXECUTION QUEUE
                |
          WORKER CLUSTER
                |
       AUTHORIZED TEST TARGETS
                |
          EVIDENCE STORE
                |
        POSTGRESQL / REDIS
                |
          OBSERVABILITY
```

Deploy separate worker pools by security profile.

Management-plane services must never share unrestricted network access with execution workers.

## 22.3 Infrastructure as code

Implement Terraform modules for:

- Network.
- Kubernetes.
- PostgreSQL.
- Redis.
- Object storage.
- Secret management.
- Monitoring.
- IAM.
- Container registry.

Support AWS, Azure, and GCP through separate provider modules.

Do not attempt to abstract away cloud-specific security controls that differ materially between providers.

# 23. Performance and Scalability

Initial engineering targets:

- Support multiple concurrent engagements.
- Horizontal scaling of stateless API services.
- Independently scalable workers.
- Configurable per-tenant concurrency.
- Durable execution queues.
- Bounded AI request concurrency.
- Evidence storage outside the relational database.
- Efficient asset and finding queries.
- Paginated API responses.
- WebSocket event fan-out.

Initial benchmark targets:

| Metric | Target |
|---|---:|
| API p95 latency for ordinary reads | Under 300 ms |
| Policy decision p95 | Under 100 ms |
| Dashboard initial data load | Under 2 seconds |
| Worker cancellation initiation | Under 5 seconds |
| Event delivery p95 | Under 2 seconds |
| Management API availability | 99.9% target |

These are engineering targets to validate under defined workloads, not guaranteed performance claims.

# 24. Implementation Phases

Implement in the following order.

## Phase 01 — Foundation

Deliver:

- Repository structure.
- Solution and project configuration.
- Clean Architecture boundaries.
- PostgreSQL.
- EF Core migrations.
- Redis.
- Authentication.
- Tenant isolation.
- Logging.
- OpenTelemetry.
- Health checks.
- Docker Compose.
- Initial CI.

Acceptance:

All projects build, all foundational tests pass, and tenant isolation is verified.

## Phase 02 — Engagement Management

Deliver:

- Tenant management.
- User management.
- Roles.
- Engagement entities.
- Authorization references.
- Target scope.
- Excluded target handling.
- Engagement lifecycle.
- Audit trail.

Acceptance:

No assessment can start without a valid authorized engagement.

## Phase 03 — Reconnaissance

Deliver:

- Asset discovery framework.
- Web asset discovery.
- Network asset inventory.
- Cloud inventory integrations.
- Fingerprinting.
- Asset relationships.
- Scope enforcement.

Acceptance:

Every discovered asset has provenance, and every external interaction is checked against current scope.

## Phase 04 — Vulnerability Intelligence

Deliver:

- CVE integration.
- CWE mapping.
- CVSS scoring.
- EPSS integration.
- CISA KEV integration.
- Vendor advisory ingestion.
- Dependency correlation.
- Intelligence freshness tracking.

Acceptance:

Every intelligence-derived finding references its source and retrieval timestamp.

## Phase 05 — Attack Graph

Deliver:

- Graph domain model.
- Asset relationships.
- Vulnerability relationships.
- Identity relationships.
- Network relationships.
- Cloud resource relationships.
- Attack-path hypotheses.
- Graph visualization.

Acceptance:

Observed facts and inferred relationships are distinguishable.

## Phase 06 — AI Planning

Deliver:

- Provider abstraction.
- Structured AI outputs.
- Context construction.
- Hypothesis generation.
- Ranking.
- Explanation.
- Confidence handling.
- Prompt-injection defenses.
- Provider health monitoring.

Acceptance:

AI can generate validated testing plans but cannot directly execute them.

## Phase 07 — Policy and Approval

Deliver:

- OPA integration.
- Policy bundles.
- Approval workflow.
- Approval expiration.
- Policy decision records.
- Execution capability validation.
- Emergency stop.

Acceptance:

No execution can bypass policy or required human approval.

## Phase 08 — Execution Runtime

Deliver:

- Rust worker.
- Worker lifecycle.
- Module registry.
- Signed module artifacts.
- Capability manifests.
- Linux isolation.
- Windows isolation.
- Resource limits.
- Network restrictions.
- Cancellation.
- Worker cleanup.

Acceptance:

All execution occurs in controlled workers, and all authorization checks are independently enforced.

## Phase 09 — Verification and Evidence

Deliver:

- Evidence collection.
- Evidence hashing.
- Independent verification.
- Finding classification.
- Finding lifecycle.
- Remediation guidance.
- Evidence viewer.

Acceptance:

No finding is labeled verified without sufficient verification evidence.

## Phase 10 — Learning Engine

Deliver:

- Outcome collection.
- Historical performance analysis.
- Strategy evaluation.
- Benchmark integration.
- Model evaluation.
- Strategy versioning.
- Promotion approval.

Acceptance:

A new strategy cannot become active without passing regression benchmarks and required review.

## Phase 11 — Frontend

Deliver:

- Dashboard.
- Engagement manager.
- Asset explorer.
- Attack graph.
- AI planning center.
- Approval center.
- Execution monitor.
- Findings center.
- Evidence viewer.
- Reporting center.
- Settings and administration.

Acceptance:

All interactive controls perform real backend operations and display actual execution state.

## Phase 12 — Enterprise Readiness

Deliver:

- Multi-tenant hardening.
- Cloud deployment.
- Terraform.
- Kubernetes.
- Backup and restore.
- Disaster recovery.
- Security assessment.
- Load testing.
- Full benchmark suite.
- Release signing.
- Documentation.
- Operational runbooks.

Acceptance:

The platform passes all production release gates.

# 25. Definition of Done

ARES is not complete until all of the following are true.

### Functional

- [ ] All three assessment domains work.
- [ ] Engagement authorization works.
- [ ] Scope enforcement works.
- [ ] Asset discovery works.
- [ ] Vulnerability intelligence works.
- [ ] AI planning works.
- [ ] Attack graph works.
- [ ] Approval workflow works.
- [ ] Worker execution works.
- [ ] Independent verification works.
- [ ] Evidence storage works.
- [ ] Learning evaluation works.
- [ ] Reporting works.
- [ ] Frontend is connected to real backend functionality.

### Security

- [ ] Tenant isolation verified.
- [ ] Scope bypass tests pass.
- [ ] Policy fail-closed behavior verified.
- [ ] Worker isolation verified.
- [ ] Secret isolation verified.
- [ ] Prompt injection tests pass.
- [ ] Module signature verification works.
- [ ] Emergency stop works.
- [ ] Audit integrity verified.
- [ ] No critical or high unresolved release-blocking vulnerabilities.

### Quality

- [ ] All builds pass.
- [ ] All unit tests pass.
- [ ] All integration tests pass.
- [ ] All contract tests pass.
- [ ] All security tests pass.
- [ ] All benchmark gates pass.
- [ ] All end-to-end tests pass.
- [ ] No unexplained test skips.
- [ ] No fake implementations.
- [ ] No TODO or FIXME markers in production paths.
- [ ] No hardcoded secrets.

### Operations

- [ ] Docker deployment works.
- [ ] Kubernetes deployment works.
- [ ] Monitoring works.
- [ ] Logging works.
- [ ] Health checks work.
- [ ] Backup and restore verified.
- [ ] Graceful shutdown verified.
- [ ] Documentation complete.
- [ ] Release artifacts signed.

# 26. Mandatory Engineering Rules

1. Use Clean Architecture and SOLID principles.
2. Use dependency injection.
3. Use asynchronous I/O.
4. Use strongly typed contracts.
5. Keep domain logic independent of infrastructure.
6. Use structured logging.
7. Use centralized exception handling.
8. Use database transactions where required.
9. Use parameterized queries.
10. Use explicit validation.
11. Use cancellation tokens throughout asynchronous operations.
12. Use idempotent execution and event processing.
13. Use secure defaults.
14. Fail closed on authorization errors.
15. Never expose secrets to AI models.
16. Never allow AI to authorize its own actions.
17. Never permit unrestricted worker execution.
18. Never treat an unverified AI claim as a security finding.
19. Never use synthetic data as evidence of actual execution.
20. Document architecture decisions and security assumptions.

Every production implementation must include meaningful tests and operational observability.

# 27. Final Delivery Requirements

The implementation agent must produce:

1. Complete repository structure.
2. Fully implemented backend.
3. Fully implemented AI orchestration service.
4. Fully implemented isolated worker runtime.
5. Working policy engine.
6. Working approval service.
7. Security module registry.
8. Initial security assessment modules.
9. Evidence and verification services.
10. Learning and benchmark framework.
11. Complete React dashboard.
12. Database migrations.
13. API documentation.
14. Docker Compose configuration.
15. Kubernetes deployment manifests.
16. Terraform infrastructure.
17. GitHub Actions CI/CD.
18. Automated test suites.
19. Security testing documentation.
20. Production deployment documentation.
21. Operational runbooks.
22. Threat model.
23. SBOM and release signing configuration.

Do not stop at architecture diagrams or generated scaffolding.

Implement each phase, build it, test it, resolve failures, and verify the result before advancing.

Maintain a persistent implementation ledger documenting:

- Completed work.
- Files changed.
- Architecture decisions.
- Tests executed.
- Test results.
- Known defects.
- Security findings.
- Remaining acceptance criteria.

At the end of every phase, provide independently verifiable evidence of completion.

The final release must be reproducible from a clean checkout using documented commands.

---

# FINAL PRODUCT PRINCIPLE

ARES is an AI-driven security assessment platform, not an unrestricted exploit automation agent.

Its competitive advantage must come from combining:

- Intelligent reconnaissance.
- Context-aware vulnerability analysis.
- AI-assisted attack-path reasoning.
- Deterministic authorization.
- Controlled security test execution.
- Independent verification.
- High-quality evidence.
- Benchmark-driven learning.

**The AI determines what is worth investigating. The policy engine determines what is permitted. The human authorizes sensitive actions. The execution runtime performs controlled tests. Independent verification determines what is true.**