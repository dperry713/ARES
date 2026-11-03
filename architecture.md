```mermaid
flowchart TD
    A["**React + TypeScript**<br/>Operations console and approval interface"]
    B["**ASP.NET Core API**<br/>Identity, engagements, orchestration and reporting"]
    
    subgraph middle [" "]
        direction LR
        C["**Python AI**<br/>Planning and intelligence"]
        D["**OPA**<br/>Policy decisions"]
    end
    
    E["**Human approval + execution manager**<br/>Authorization, capability validation and scheduling"]
    
    F["**Rust isolated worker runtime**<br/>Constrained execution of signed, approved security modules"]
    
    G["**Evidence**<br/>Integrity and provenance"]
    H["**Verification**<br/>Independent validation"]

    A --> B
    B --> C
    B --> D
    C --> E
    D --> E
    E --> F
    F --> G
    F --> H

    classDef dark fill:#111,stroke:#444,color:#eee,rx:10
    classDef accent fill:#0d2b1e,stroke:#2d8a5e,color:#fff,rx:10
    classDef mid fill:#1c1c1c,stroke:#555,color:#ddd,rx:8

    class A,B,E dark
    class F accent
    class C,D,G,H mid