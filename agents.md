## MANDATORY DOCUMENTATION INTEGRITY AND CAPABILITY ACCURACY STANDARD

**All project documentation MUST remain accurate, current, complete, and directly supported by implemented and independently verifiable system behavior. False, misleading, exaggerated, speculative, or unsupported claims are strictly prohibited.**

### 1. Documentation Maintenance Is Continuous

The agent MUST maintain documentation throughout the entire development lifecycle, not postpone documentation until the end.

Whenever implementation changes affect architecture, functionality, APIs, configuration, security, deployment, operations, testing, or user experience, the corresponding documentation MUST be updated in the same implementation task.

Documentation updates are part of the definition of done for every feature, fix, refactor, and architectural change.

### 2. Mandatory Documentation Inventory

Maintain and continuously update the following project documentation, creating files where appropriate:

| Document | Required contents |
|---|---|
| `MEC.md` | Authoritative master execution contract and requirements |
| `README.md` | Accurate product overview, capabilities, prerequisites, setup, and usage |
| `ARCHITECTURE.md` | Actual architecture, components, boundaries, and dependencies |
| `IMPLEMENTATION_STATUS.md` | Implemented, partial, planned, blocked, and unimplemented capabilities |
| `MASTER_EXECUTION_LEDGER.md` | Chronological engineering execution and decisions |
| `ARCHITECTURE_DECISIONS.md` | Architectural decisions, rationale, and consequences |
| `SECURITY_DECISIONS.md` | Security controls, enforcement boundaries, and decisions |
| `THREAT_MODEL.md` | Threats, attack surfaces, mitigations, and residual risks |
| `API_REFERENCE.md` | Actual API endpoints, schemas, authentication, and responses |
| `SKILL_INVENTORY.md` | Skills discovered, enabled, applied, and verified |
| `TEST_STRATEGY.md` | Test architecture, test categories, and quality gates |
| `TEST_RESULTS.md` | Actual test executions, results, failures, and coverage |
| `BENCHMARK_RESULTS.md` | Reproducible benchmark methodology and measured results |
| `KNOWN_DEFECTS.md` | Outstanding defects, severity, impact, and remediation |
| `RISK_REGISTER.md` | Technical, operational, security, and delivery risks |
| `DEPLOYMENT.md` | Verified deployment procedures and supported environments |
| `OPERATIONS_RUNBOOK.md` | Actual operational procedures, monitoring, recovery, and troubleshooting |
| `CONFIGURATION_REFERENCE.md` | Supported configuration options, defaults, and validation |
| `RELEASE_READINESS.md` | Acceptance criteria, evidence, blockers, and release disposition |
| `CHANGELOG.md` | User-visible changes, fixes, and compatibility changes |

Add additional documentation when a subsystem requires its own specification, operating procedure, or security guidance.

Do not create documentation that merely repeats other documents without adding value.

### 3. Absolute Truthfulness Requirements

The agent MUST NEVER:

- Claim a feature exists when it has not been implemented.
- Claim a feature works without executing and verifying it.
- Describe a prototype, mock, stub, simulation, or placeholder as production functionality.
- Present planned functionality as implemented functionality.
- Report tests as passing when they were not executed successfully.
- Invent test results, benchmark measurements, coverage percentages, security findings, or performance metrics.
- Claim an integration works without successfully exercising the actual integration.
- Claim a security control is enforced when it is merely documented or configured without verification.
- Claim authentication, authorization, isolation, encryption, auditability, or policy enforcement is effective without appropriate evidence.
- Claim support for an operating system, cloud provider, model provider, protocol, or deployment environment without testing that support.
- Present mocked API responses as real service responses.
- Use screenshots, dashboard values, or status indicators that imply execution or success when no corresponding operation occurred.
- Hide failed tests, incomplete work, known defects, unsupported features, or operational limitations.
- Remove or weaken documented requirements to make a feature appear complete.
- Fabricate citations, external validation, certifications, compliance status, or third-party endorsements.

### 4. Mandatory Capability Classification

Every user-facing feature, API capability, integration, execution module, and operational function MUST have an explicit implementation status.

Use these classifications consistently:

- **IMPLEMENTED_AND_VERIFIED:** Implemented and successfully verified through an appropriate reproducible test or execution.
- **IMPLEMENTED_NOT_VERIFIED:** Implementation exists, but sufficient verification has not been completed.
- **PARTIALLY_IMPLEMENTED:** Some functionality exists, but documented requirements remain incomplete.
- **SIMULATED:** Behavior is simulated and does not perform the corresponding real operation.
- **MOCKED:** Uses mock implementations, fixtures, or synthetic responses.
- **PLANNED:** Designed or specified but not implemented.
- **BLOCKED:** Cannot be completed because of a documented dependency, permission, environment, or technical constraint.
- **UNSUPPORTED:** Intentionally not supported.
- **DEPRECATED:** Previously supported but no longer recommended or maintained.

Never use IMPLEMENTED_AND_VERIFIED without evidence.

### 5. Capability-to-Evidence Traceability

Every significant capability MUST have a traceable evidence record containing:

1. Capability identifier and description.
2. Relevant MEC requirement identifiers.
3. Implemented source modules and entry points.
4. Required configuration and dependencies.
5. Actual execution path.
6. Relevant unit, integration, security, and end-to-end tests.
7. Most recent execution result and timestamp.
8. Known limitations and failure conditions.
9. Applicable permissions and authorization requirements.
10. Documentation references.

Evidence must be reproducible wherever practical.

A passing unit test alone does not prove that an end-to-end feature works. Use integration or end-to-end evidence appropriate to the capability.

### 6. Documentation and Implementation Synchronization

For every implementation task, follow this sequence:

1. Identify affected documentation.
2. Implement the change.
3. Execute relevant tests.
4. Verify actual runtime behavior where applicable.
5. Update documentation to match the verified implementation.
6. Update the capability classification.
7. Record test results and evidence.
8. Update the implementation ledger and release-readiness records.

Documentation MUST NOT get ahead of implementation.

If implementation changes invalidate existing documentation, correct the documentation immediately.

### 7. GUI and Dashboard Truthfulness

ARES dashboards and visualizations MUST represent actual backend state and real execution records.

- Execution status must originate from the execution manager.
- Approval status must originate from the approval service.
- Findings must originate from recorded analysis and verification results.
- Evidence must reference persisted evidence artifacts.
- Metrics must derive from actual measurements.
- Worker status must reflect actual worker health and lifecycle.
- Benchmark results must come from executed benchmark runs.
- Learning metrics must reflect actual recorded evaluations.
- Security status must reflect verified security controls.

Do not implement decorative controls that appear functional but do nothing.

Every interactive control MUST invoke a real, authorized application operation or be explicitly presented as unavailable.

Do not fabricate live activity, progress, completion notifications, telemetry, or success indicators.

### 8. Testing and Verification Integrity

- Record exact test commands and execution environments.
- Preserve actual test output and relevant CI evidence.
- Distinguish skipped tests from passing tests.
- Distinguish unavailable tests from failed tests.
- Document test limitations and coverage exclusions.
- Never manipulate coverage calculations to satisfy quality gates.
- Never disable a failing test merely to obtain a green build.
- Investigate and resolve failures rather than suppressing them.
- Verify security-critical behavior independently of ordinary functional tests.
- Re-run affected regression suites after repairs.

### 9. Release and Production-Readiness Gate

ARES MUST NOT be described as production-ready unless:

- All applicable MEC requirements have been satisfied.
- Required functionality is implemented and verified.
- Security controls have been tested.
- Authorization boundaries are enforced.
- Integration and end-to-end tests have passed.
- Deployment and recovery procedures have been exercised.
- Documentation accurately describes the released system.
- No unresolved release-blocking defects remain.
- Known limitations and residual risks are disclosed.
- Release artifacts and verification evidence are available.

If any condition is unmet, report the actual readiness status and explicitly identify the remaining blockers.

### 10. Final Documentation Audit

Before declaring the project complete, conduct an independent documentation-integrity audit.

Verify that:

- Every documented feature exists or has an accurate status classification.
- Every implemented feature has corresponding documentation.
- Every documented API matches the actual implementation.
- Every configuration option exists and behaves as documented.
- Installation and deployment instructions have been executed successfully.
- Test and benchmark results are genuine.
- Security claims are supported by verification evidence.
- All known defects and limitations are disclosed.
- README, implementation status, architecture, MEC compliance, and release-readiness documentation agree.
- No stale instructions, broken references, misleading screenshots, or unsupported capability claims remain.

**Acceptance criterion:** There must be zero knowingly false or unsupported capability claims. Accuracy takes precedence over presentation, completeness claims, delivery speed, and perceived project maturity.