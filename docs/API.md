# API Reference

## `POST /api/form-access-tokens`

Issues (or returns an existing) client form-access token for the mobile app deployment form.

### Authentication

| Header | Required | Description |
|--------|----------|-------------|
| `X-Api-Key` | Yes | Must match `FormAccess:ApiKey` from configuration (user-secrets in dev, environment variable in production). |
| `Content-Type` | Yes | `application/json` |

Rate limit: **10 requests per 60 seconds per IP** (`token-api` policy).

### Request body

```json
{
  "clientName": "Acme Corporation",
  "clientAppName": "Acme Mobile",
  "email": "client@example.com"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `clientName` | string (max 255) | Yes | Organization / client name stored on the token. |
| `clientAppName` | string (max 255) | Yes | Prefills **App Name** on the client form (truncated to 30 characters). |
| `email` | string (max 255) | No | When provided, the form link is emailed via Mailgun SMTP. Token issuance succeeds even if email fails. |

### Success response — `200 OK`

```json
{
  "token": "xK9mP2vQ8nR4wL6jH3tY1sA0bC5dE7fG9hI0k",
  "clientName": "Acme Corporation",
  "clientAppName": "Acme Mobile",
  "formUrl": "https://deploy.example.com/AppDeployment/Form/xK9mP2vQ8nR4wL6jH3tY1sA0bC5dE7fG9hI0k",
  "alreadyExisted": false,
  "isSubmitted": false,
  "emailRecipient": "client@example.com",
  "emailSent": true,
  "emailError": null
}
```

| Field | Description |
|-------|-------------|
| `token` | Opaque URL-safe token (256-bit entropy). |
| `formUrl` | Absolute URL the client opens to fill the deployment form. |
| `alreadyExisted` | `true` when an active token already existed for the same client/app pair. |
| `isSubmitted` | `true` when the client has already completed the initial Create save. |
| `emailSent` | Present only when `email` was supplied. |
| `emailError` | SMTP error message when `emailSent` is `false`. |

### Error responses

| Status | When | Body |
|--------|------|------|
| `400 Bad Request` | Invalid JSON or validation failure | RFC 7807 Problem Details / validation errors |
| `401 Unauthorized` | Missing or invalid `X-Api-Key` | `{ "error": "Invalid or missing API key. Send a valid X-Api-Key header." }` |
| `429 Too Many Requests` | Rate limit exceeded | Plain text + `Retry-After: 60` |

### Example — curl

```bash
curl -X POST "https://deploy.example.com/api/form-access-tokens" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_ADMIN_API_KEY" \
  -d '{
    "clientName": "Acme Corporation",
    "clientAppName": "Acme Mobile",
    "email": "client@example.com"
  }'
```

### Client form flow

1. Share `formUrl` with the client (or rely on the optional email).
2. Client opens `GET /AppDeployment/Form/{token}` — shows Create or Edit depending on submission state.
3. Client saves via `POST /AppDeployment/Create` or `POST /AppDeployment/Edit` (token-gated, no admin login required).
4. Admin or client starts deployment via `POST /AppDeployment/StartDeployment`.

See also: [TOKEN-BASED-FORM-ACCESS.md](TOKEN-BASED-FORM-ACCESS.md)
