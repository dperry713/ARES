# ARES — AI-Driven Penetration Testing Platform

ARES is a modular, AI-driven penetration-testing platform that automates the security assessment lifecycle. The platform combines autonomous reconnaissance, vulnerability intelligence, attack-surface mapping, controlled security test execution, independent finding verification, and professional security reporting.

## Current Milestone: P13 First Slice (2026-11-02)

**Status**: Implementation complete across 28 projects; 116/116 tests passing (95 unit + 21 API); build 0 warnings / 0 errors.

**Architecture**: .NET 10 modular monolith with ASP.NET Core API, Clean Architecture domain model, PostgreSQL persistence, OPA/Rego policy engine, Redis caching, Serilog structured logging, OpenTelemetry instrumentation.

**Core Capabilities** (all verified through unit and API tests):
- Engagement lifecycle management with full authorization workflow
- Policy engine with 14 deterministic checks (engagement authorization, scope enforcement, module validation, approval requirements, execution limits, tenant activity, emergency stop)
- Bound approval store with hash binding, TTL expiry, single-use enforcement
- Execution management with full lifecycle (Proposed→AwaitingApproval→Approved→Queued→Running→Verifying→Completed)
- Emergency-stop end-to-end (latch blocks policy+scope+queue+lease+start; recovery re-opens with audit trail)
- Tamper-evident evidence store with SHA-256 ingest, per-engagement hash chain, tenant isolation, export manifest
- Finding verification ladder (Suspected→Evidence→VerificationInProgress→Verified; independent check required; confidence never advances)
- Adversarial scope hardening (encoded IPs, special-use destinations, @-smuggling, tenant separation, canonicalization)
- Tenant isolation enforced across engagements, policies, executions, and evidence
- PostgreSQL durability option (PostgresStateJournal + PostgresEvidenceService, conditional on connection strings)
- AI provider abstraction with deterministic fallback
- 21 API endpoints covering authZ matrix, tenant isolation, findings review/resolve, emergency-stop, execution paths

## Repository Structure

28 .NET 10 class library projects under `src/backend/`:
- `Ares.Api` — ASP.NET Core API gateway
- `Ares.Domain` — Core entity shapes
- `Ares.Contracts` — Shared contracts/interfaces
- `Ares.Application` — Application services
- `Ares.Identity` — Authentication/authorization/tenant management
- `Ares.Engagements` — Assessment configuration and authorization
- `Ares.Recon` — Asset discovery and fingerprinting
- `Ares.Intelligence` — Vulnerability and security intelligence
- `Ares.AttackGraph` — Relationship and attack-path analysis
- `Ares.Policy` — Deterministic authorization (OPA/Rego)
- `Ares.Approvals` — Human approval workflows (real service, not stub)
- `Ares.Execution` — Job scheduling and execution management
- `Ares.Worker` — Isolated security test execution framework
- `Ares.Modules` — Security test module registry
- `Ares.Verification` — Independent finding verification
- `Ares.Evidence` — Evidence storage and integrity (PostgreSQL PostgresEvidenceService wired)
- `Ares.Learning` — Historical performance and strategy evaluation
- `Ares.Reporting` — Technical and executive reporting
- `Ares.Notifications` — Operational notifications
- `AreS.Schemas` — Data schemas
- `Ares.Orchestrator` — AI orchestration framework
- `Ares.Providers` — AI provider adapters
- `Ares.Planner` — Planning and hypothesis generation
- `Ares.Policy` — Policy engine
- `Ares.Approvals` — Approval service

## Test Results

| Suite | Passed | Total |
|-------|--------|-------|
| **Unit Tests** | **95** | 95 |
| **API Tests** | **21** | 21 |
| **Total** | **116** | 116 |

All quality gates pass: build (0 errors), unit tests (100%), API tests (100%).

## Quality Gates

- `dotnet build`: 0 C# compilation errors (with `-warnaserror`)
- `dotnet test` (unit): 95/95 passing
- `dotnet test` (API): 21/21 passing
- All authZ/tenant isolation scenarios verified
- All execution path fail-closed paths verified
- Emergency stop admin-only enforcement verified

## Known Dependency Risk

- **Npgsql 8.0.0** reports high severity vulnerability (GHSA-x9vc-6hfv-hg8c) in 12 projects as transitive dependency
- Accepted as known third-party risk — build compiles, all tests pass; not an application-code defect
- Resolution path: upgrade to Npgsql 8.0.2+/9.x in separate dependency-management task

## Recent Additions (P13 — 2026-11-02)

- PostgreSQL persistence: PostgresStateJournal + PostgresEvidenceService wired into DI
- API test expansion: 21 tests (up from 16) covering authZ, tenant isolation, findings, emergency-stop, execution paths
- ExecutionService fix: Removed broken `_jobObject Lazy<JobObject?>`
- Package hygiene: Centralized package versions via Directory.Packages.props
- P13 first slice: Recon engine, attack-graph correlation, learning engine, console execution, benchmarks, enterprise tenant isolation

## Quick Start

```powershell
# Build
cd C:\Users\d\pentest
dotnet build Ares.Solution.slnx --no-incremental

# Run unit tests
dotnet test tests/unit/Ares.UnitTests/Ares.UnitTests.csproj

# Run API tests
dotnet test tests/unit/Ares.ApiTests/Ares.ApiTests.csproj
```