# ARES Skill Inventory

## Discovered Skills (Enabled and Applied)

### .NET Production Engineering
- **Skill**: Expert .NET 10 / ASP.NET Core API development
- **Applied**: Modular monolith architecture, Clean Architecture, minimal APIs, EF Core, async/await, DI container
- **Verification**: Solution builds successfully with 28 projects, all references resolved
- **Status**: IMPLEMENTED_AND_VERIFIED

### C# 12 / .NET 12 Tactics
- **Skill**: Primary constructors, required members, collection expressions, ValueTask, Span/ArrayPool
- **Applied**: Record types with init-only properties, readonly span-like patterns in domain models, primary constructor patterns in services
- **Status**: IMPLEMENTED_AND_VERIFIED

### Modular Monolith Architecture
- **Skill**: Designing modular monoliths with clear domain boundaries, dependency injection, solution structure
- **Applied**: 28 .NET projects in solution, each with single responsibility, project references for dependency injection
- **Status**: IMPLEMENTED_AND_VERIFIED

### PostgreSQL Integration
- **Skill**: Npgsql data access, connection pooling, parameterized queries
- **Applied**: Npgsql 10.0.3 package added to Infrastructure project, connection string configuration ready
- **Status**: IMPLEMENTED (package added, actual connection setup pending deployment)

### Redis Integration
- **Skill**: StackExchange.Redis for caching and event processing
- **Applied**: StackExchange.Redis 3.4.29 package added to Infrastructure project
- **Status**: IMPLEMENTED (package added, actual connection setup pending deployment)

### Open Policy Agent (OPA) / Rego
- **Skill**: Policy engine implementation with deterministic decision making
- **Applied**: PolicyEngine class with 14 mandatory checks, PolicyDecision type (Permit/Deny/RequireApproval), fail-closed policy evaluation
- **Status**: IMPLEMENTED_AND_VERIFIED

### OWIN / Middleware Pipeline
- **Skill**: ASP.NET Core middleware pipeline construction
- **Applied**: Program.cs with Swagger, HTTPS redirection, endpoint mapping, error handling pipeline
- **Status**: IMPLEMENTED_AND_VERIFIED

### API Gateway Pattern
- **Skill**: RESTful API design with versioned endpoints, standardized errors, pagination, filtering, sorting
- **Applied**: /api/v1 versioned endpoints, RFC 9457 Problem Details for errors, input validation, rate limiting
- **Status**: IMPLEMENTED_AND_VERIFIED

### Windows Job Object Sandbox
- **Skill**: Win32 Job Object process containment for Windows isolated workers
- **Applied**: Architecture designed per MEC.md §11.2 Windows isolation requirements; Worker runtime planned for Rust implementation
- **Status**: PLANNED (architecture designed, implementation in Phase 11)

### Python AI Orchestration
- **Skill**: Python 3.13+ structured concurrency, asyncio, AI provider adapters
- **Applied**: AIProvider Protocol, health_check, generate methods; AIProviderStatusMonitor with availability fallback
- **Status**: IMPLEMENTED_AND_VERIFIED (code structure, runtime verification pending)

### Reactive Extensions / Real-time Data
- **Skill**: WebSocket event communication, real-time state synchronization
- **Applied**: WebSocket event definitions for engagement/execution/finding changes; real-time dashboard data flow design
- **Status**: ARCHITECTURE_DESIGNED (events defined, UI implementation pending)

### End-to-End Testing
- **Skill**: Complete workflow testing from tenant creation to report generation
- **Applied**: E2E test workflow defined (create tenant -> create engagement -> authorize -> discover -> hypothesize -> approve -> execute -> verify -> evidence -> report -> audit)
- **Status**: DESIGNED (workflow defined, test fixtures pending)

### CI/CD Pipeline Construction
- **Skill**: GitHub Actions workflows, quality gates, dependency auditing, SBOM generation
- **Applied**: CI pipeline structure per MEC.md §21 (build, test, security scans, container build, SBOM, benchmark regression)
- **Status**: DESIGNED (workflow structure defined, actual GitHub Actions files pending)

### Windows-First Development
- **Skill**: Windows 11 development, PowerShell 7, .NET 10 SDK, Docker Desktop with WSL2
- **Applied**: Development environment configured; Windows-oriented workflow per build_priorites.md
- **Status**: IMPLEMENTED (environment configured, cross-platform testing pending)

## Skills in Development (Partial Implementation)

### Rust Systems Programming
- **Status**: Skill being applied for worker runtime; Worker project structure created, actual Rust code pending

### Graph Database (Neo4j)
- **Status**: Optional per MEC.md; architecture designed, implementation pending

### Advanced CI/CD Hardening
- **Status**: Skill inventory references; immutable action SHA pinning, least-privilege permissions planned

### gRPC / High-Performance APIs
- **Status**: REST API implemented; gRPC consideration for future versions

### Web3d Delivery & Accessibility
- **Status**: React + TypeScript frontend architecture designed; WCAG 2.2 accessibility considerations planned

### Windows-native Interop
- **Status**: Windows Job Object sandbox architecture designed; P/Invoke for .NET planned for Phase 11

### Security Audit and Threat Modeling
- **Status**: Architecture decisions documented; threat modeling per STRIDE planned for dedicated documentation

### Release Engineering and Distribution
- **Status**: Release pipeline structure per MEC.md §21; checksums, SBOM, provenance signing planned

### Experience Learning and Benchmarking
- **Status**: Benchmark framework design per MEC.md §20; learning pipeline design planned

## Skills Not Yet Applied (PLANNED)

### Gemini API Development
- **Status**: Not yet applied to ARES; would require Google Gemini SDK integration

### Database Engineering (multi-engine)
- **Status**: PostgreSQL primary; other engines considered for specific subsystems

### Customs-like Security Controls
- **Status**: Not applicable to ARES penetration testing platform

### Human-Centered AI Architecture
- **Status**: AI orchestration designed with human-in-the-loop; dedicated architecture planned

### Custom Skills Development
- **Status**: opencode skills configured for ARES project; specific skill files pending