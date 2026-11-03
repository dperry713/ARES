# ARES API Reference

## Base URL
`/api/v1`

## Authentication
All API endpoints require OIDC/OAuth 2.0 authentication. Use access tokens with appropriate scopes.

### Supported Roles
- PlatformAdministrator: Full access to all endpoints
- TenantAdministrator: Manage tenant's engagements, assets, users
- SecurityLead: View and approve engagements, manage findings
- SecurityOperator: Create engagements, execute tests, view results
- Approver: Approve/reject test hypotheses
- Auditor: Read-only access to all audit records
- ReadOnlyViewer: Read-only access to non-sensitive data

### Authentication Flow
1. Authenticate via OIDC provider (IdP-initiated or ARES-initiated)
2. Receive access token and refresh token
3. Include `Authorization: Bearer <access_token>` header in all requests
4. Token refresh using refresh token grant before expiration

## Engagement Endpoints

### Create Engagement
```http
POST /api/v1/engagements
```
**Request body:**
```json
{
  "tenantId": "guid-string",
  "name": "Engagement name",
  "description": "Engagement description",
  "authorizationReference": "AR-2024-001",
  "startAt": "2024-01-15T00:00:00Z",
  "endAt": "2024-12-31T23:59:59Z",
  "scopeDefinition": "192.168.1.0/24,app.example.com",
  "allowedAssessmentTypes": ["web", "network"],
  "excludedTargets": ["10.0.0.1"]
}
```

### Get Engagement
```http
GET /api/v1/engagements/{id}
```

### Authorize Engagement
```http
POST /api/v1/engagements/{id}/authorize
```

### Start Engagement
```http
POST /api/v1/engagements/{id}/start
```

### Pause Engagement
```http
POST /api/v1/engagements/{id}/pause
```

### Resume Engagement
```http
POST /api/v1/engagements/{id}/resume
```

### Complete Engagement
```http
POST /api/v1/engagements/{id}/complete
```

## Tenant Endpoints

### Create Tenant
```http
POST /api/v1/tenants
```
**Request body:**
```json
{
  "name": "Customer Name",
  "description": "Customer description",
  "configuration": "{\"key\": \"value\"}",
  "retentionPolicy": "7years"
}
```

## Policy Evaluation Endpoints

### Evaluate Policy
```http
POST /api/v1/policy/evaluate
```
**Request body:**
```json
{
  "engagementId": "guid-string",
  "testCategory": "web_vulnerability_scan",
  "moduleId": "dependency_assessment",
  "moduleVersion": "1.0.0",
  "requiresHumanApproval": true,
  "maximumConcurrency": 5,
  "rateLimits": "default"
}
```

**Response:**
```json
{
  "decision": "permit|deny|requireapproval",
  "reasonCode": "PERMIT|ENGAGEMENT_NOT_AUTHORIZED|...",
  "explanations": ["..."],
  "evaluatedAt": "2024-11-01T12:00:00Z"
}
```

## Execution Endpoints

### Create Execution (Propose)
```http
POST /api/v1/executions
```
**Request body:**
```json
{
  "engagementId": "guid-string",
  "hypothesisId": "guid-string",
  "moduleId": "dependency_assessment",
  "moduleVersion": "1.0.0"
}
```

### Get Execution
```http
GET /api/v1/executions/{id}
```

### Cancel Execution
```http
POST /api/v1/executions/{id}/cancel
```

### Get Execution Events
```http
GET /api/v1/executions/{id}/events
```

## Finding Endpoints

### Get Findings
```http
GET /api/v1/findings
```

### Get Finding
```http
GET /api/v1/findings/{id}
```

### Get Finding Evidence
```http
GET /api/v1/findings/{id}/evidence
```

### Review Finding
```http
POST /api/v1/findings/{id}/review
```

### Resolve Finding
```http
POST /api/v1/findings/{id}/resolve
```

## Approval Endpoints

### Get Approvals
```http
GET /api/v1/approvals
```

### Get Approval
```http
GET /api/v1/approvals/{id}
```

### Approve Operation
```http
POST /api/v1/approvals/{id}/approve
```

### Reject Operation
```http
POST /api/v1/approvals/{id}/reject
```

## WebSocket Events
The following events are communicated via WebSocket for real-time updates:
- `engagement_created`
- `engagement_authorized`
- `engagement_started`
- `engagement_paused`
- `engagement_completed`
- `execution_queued`
- `execution_running`
- `execution_completed`
- `execution_failed`
- `finding_detected`
- `finding_verified`
- `finding_rejected`
- `worker_health`
- `policy_decision`

## Error Handling
All API errors use RFC 9457 Problem Details format:
```json
{
  "type": "https://ares.example.com/problems/invalid-request",
  "title": "Bad Request",
  "status": 400,
  "detail": "Specific error description",
  "instance": "/api/v1/engagements"
}
```

### Standard Error Codes
- `400`: Invalid request format or content
- `401`: Authentication required or token expired
- `403`: Insufficient permissions
- `404`: Resource not found
- `409`: State transition conflict (e.g., invalid engagement state for operation)
- `413`: Payload too large
- `429`: Rate limit exceeded
- `500`: Internal server error
- `503`: Service unavailable (e.g., policy engine down, AI provider unavailable)