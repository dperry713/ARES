# ARES Architecture Decisions

## Overall Architecture: Modular Monolith

The ARES platform uses a modular monolith architecture rather than immediately distributing every domain into a microservice. This approach simplifies initial implementation while maintaining clear domain boundaries and independent deployability through stable interfaces.

### Rationale
- **Simpler initial development**: Single deployment unit, easier debugging, shared memory for domain events
- **Clear domain boundaries**: Each component has well-defined responsibilities and interfaces
- **Gradual microservice migration**: Components can be extracted into separate services as the platform matures
- **Reduced operational complexity**: Single Docker/Kubernetes deployment for initial version

### Component Boundaries

| Component | Responsibility | Interface |
|---|---|---|
| Ares.Api | External REST and WebSocket interfaces | HTTP/REST, WebSocket |
| Ares.Identity | Authentication, authorization, tenant management | OIDC/OAuth 2.0, Internal API |
| Ares.Engagements | Assessment configuration and authorization | REST API |
| Ares.Recon | Asset discovery and fingerprinting | REST API, Message queues |
| Ares.Intelligence | Vulnerability and security intelligence | REST API, Python services |
| Ares.AttackGraph | Relationship and attack-path analysis | REST API |
| Ares.Policy | Deterministic authorization | OPA/Rego, REST API |
| Ares.Approvals | Human approval workflows | REST API, GUI |
| Ares.Execution | Job scheduling and execution management | REST API |
| Ares.Worker | Isolated security test execution | Sandbox protocol, IPC |
| Ares.Modules | Security test module registry | Module manifest, Signatures |
| Ares.Verification | Independent finding verification | REST API |
| Ares.Evidence | Evidence storage and integrity | REST API, Object storage |
| Ares.Learning | Historical performance and strategy evaluation | REST API |
| Ares.Reporting | Technical and executive reporting | REST API, PDF/JSON/SARIF/CSV |
| Ares.Notifications | Operational notifications | WebSocket, SignalR |

### Technology Stack Decisions

#### Backend: .NET 10 / ASP.NET Core
- **Rationale**: Primary baseline per MEC.md §4. Performance, ecosystem, tooling, 
- **Version**: .NET 10 is the primary backend baseline. No mixing incompatible framework versions without documented compatibility decision.

#### AI Orchestration: Python 3.13+
- **Rationale**: AI model ecosystem, TensorFlow/PyTorch compatibility, modern language features
- **Interface**: Python Protocol-defined AIProvider abstraction with structured outputs
- **Providers**: OpenAI, Anthropic, Google, Azure AI, local models

#### Execution Runtime: Rust stable
- **Rationale**: Memory safety, performance, sandbox capabilities (Job Objects, seccomp), 
- **Isolation**: Windows Job Objects, Linux capabilities/seccomp/apparmor, resource quotas
- **Module interface**: ISecurityTestModule with Manifest, ValidateAsync, ExecuteAsync, VerifyAsync

#### Frontend: React + TypeScript
- **Rationale**: Ecosystem, component reuse, accessibility, React Flow for attack graph
- **UI components**: Accessible component system per MEC.md §16

#### Database: PostgreSQL
- **Rationale**: Open source, JSON support (for flexible evidence schema), mature ecosystem, 
- **Access**: EF Core / Npgsql
- **Alternative**: Neo4j for graph storage (optional initial deployment per MEC.md §4)

#### Cache: Redis
- **Rationale**: High-performance caching, Redis Streams for event processing, 
- **Use cases**: Session caching, event outbox, rate limiting, token storage

#### Policy Engine: OPA/Rego
- **Rationale**: Independent policy decision service, fail-closed policy evaluation, 
- **Versioning**: Policy version tracking, missing response fails closed
- **Decisions**: Permit, Deny, RequireApproval - missing policy response fails closed

#### Container Orchestration: Docker + Kubernetes (optional)
- **Rationale**: Container deployment, scaling, self-healing, 
- **Initial**: Modular monolith deployment, microservice extraction later

#### Observability: OpenTelemetry
- **Rationale**: Vendor-neutral observability, correlation identifiers, 
- **Signals**: Structured JSON logs, Prometheus metrics, health checks

### Key Architectural Decisions

1. **Modular monolith start**: Begin with single deployable unit, extract to microservices when domain boundaries prove stable and team scaling requires it.

2. **AI-independent execution**: The AI subsystem must be independent of the execution subsystem (MEC.md §8.1). AI can propose tests but never directly control OS processes, networks, cloud credentials, or arbitrary executables.

3. **Deterministic policy enforcement**: Policy engine must evaluate every proposed execution (MEC.md §10). Missing policy response, timeout, invalid signature, or malformed decision must fail closed.

4. **Human approval at every security-sensitive boundary**: Every invasive or potentially disruptive test requires explicit human approval bound to exact engagement, asset, module identifier, version, test parameters, and expiration (MEC.md §10.3).

5. **Evidence as first-class capability**: A finding is not verified merely because an AI model says it is exploitable (MEC.md §13). Evidence must be traceable to original observation, test, verifier, and module version.

6. **Isolated worker runtime**: The execution manager must never execute module code inside the API process (MEC.md §11). Worker must be isolated via Job Objects (Windows), containers (Linux), or Kubernetes namespaces.

7. **Separate learning subsystem**: Learning must be a separate subsystem that improves testing strategy based on independently verified historical outcomes but cannot expand the platform's permitted capabilities (MEC.md §14).

8. **Cross-platform execution isolation**: Windows and Linux workers must have separate, tested security profiles. Do not assume container isolation behaves identically across platforms (build_priorites.md §5).

9. **No arbitrary state updates from frontend**: Never permit arbitrary state updates from the frontend. State transitions must be validated by domain services (MEC.md §6.2).

10. **Module signing and registration**: Every module must be signed, versioned, registered, independently tested, and declared with an explicit capability manifest (MEC.md §7.1). Module registry must retain artifact digest, publisher identity, signature, build provenance, dependency manifest, compatibility metadata, verification requirements, and supported runtime versions.

## Design Patterns

### Command Pattern for Execution Lifecycle
Execution states (Proposed -> AwaitingApproval -> Approved -> Queued -> Running -> Verifying -> Completed) are managed through command objects rather than direct state mutations.

### Policy Decision Service
OPA/Rego-based policy engine that evaluates all proposed executions against 14 mandatory checks. Fail-closed design: any policy evaluation failure results in denial.

### Separation of Concerns
- AI proposes, human disposes (approval required)
- Generation vs. execution vs. verification are separate subsystems
- Evidence collection is independent from finding assertion
- Learning is separate from production execution

## Anti-Patterns Avoided

- **Direct AI control of OS processes**: AI never directly controls unrestricted operating-system processes (MEC.md §1.1)
- **Equating test count with security effectiveness**: Primary success metric is independently validated findings relative to testing time and risk (MEC.md §1.1)
- **Unrestricted cloud credentials**: AI prompts must never contain raw credentials (MEC.md §17.2)
- **Premature microservice distribution**: Start modular monolith, extract later when warranted
- **Arbitrary state updates from frontend**: All state transitions validated by domain services
- **Mocked API responses as real**: All visualization data must originate from actual backend state or be explicitly identified as simulated (MEC.md §16.7)
- **Decorative controls that appear functional**: Every interactive control must invoke a real, authorized application operation or be explicitly presented as unavailable (agents.md §7)