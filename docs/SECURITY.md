# Security

Security model, threat mitigations, and secrets policy for **MobileAppDeployment**.

---

## Threat model

| Threat | Attack vector | Mitigation | Residual risk |
|--------|---------------|------------|---------------|
| **Asset upload path traversal** | Malicious filename / path in multipart upload | Extension allow-list only; `Path.GetFullPath` canonicalization; directory boundary check | Low — asset keys are server-controlled constants |
| **Token brute force** | Guessing `/AppDeployment/Form/{token}` | 256-bit random tokens; rate limiting on form submit (20/min) and token API (10/min) | Low — tokens are unguessable at scale |
| **SMTP credential exposure** | Secrets in git or logs | Secrets in user-secrets / env vars; MailKit async; EF sensitive logging disabled | Low — rotate Mailgun password if leaked |
| **Clickjacking** | Embedding admin UI in iframe | `X-Frame-Options: DENY`; CSP `frame-ancestors 'none'` | Low |
| **XSS** | Injected scripts in form fields | CSP restricts script sources; Razor HTML encoding | Medium — review any new inline scripts |
| **Admin UI unauthorized access** | Direct navigation to `/AppDeployment` | ASP.NET Core Identity cookie auth on admin routes | Low — use strong admin password |
| **API key timing leak** | `X-Api-Key` header guessing | `CryptographicOperations.FixedTimeEquals` in `ApiKeyAuthorizationFilter` | Low |
| **OneSignal key at rest** | DB breach exposes push credentials | Data Protection API encryption in `OneSignalRestApiKeyEncrypted` column | Low — protect `DataProtection-Keys/` volume |
| **CSRF on form posts** | Cross-site form submission | `[ValidateAntiForgeryToken]` on all mutating MVC actions | Low |

---

## Secrets management policy

| Secret | Storage (dev) | Storage (production) | Never commit |
|--------|---------------|----------------------|--------------|
| PostgreSQL password | `dotnet user-secrets` | Environment variable | Yes |
| `FormAccess:ApiKey` | user-secrets | Environment variable | Yes |
| `Identity:AdminPassword` | user-secrets | Environment variable | Yes |
| GitHub PAT | user-secrets | `GITHUB_TOKEN` or env var | Yes |
| Mailgun SMTP password | user-secrets | Environment variable | Yes |
| OneSignal REST API Key | Client form → encrypted in DB | Same | Plaintext column cleared on save |

`appsettings.json` and `appsettings.Development.json` contain **placeholders only**.

Data Protection key ring: `DataProtection-Keys/` — excluded from git; **must be persisted** in production (volume mount).

---

## OneSignal REST API key encryption

1. Client submits `OneSignalRestApiKey` via the password field on Create/Edit.
2. `AppDeploymentService` calls `ISecretProtectionService.Protect()` before `InsertAsync` / `UpdateAsync`.
3. Ciphertext is stored in `OneSignalRestApiKeyEncrypted`; plaintext field is cleared before persistence.
4. On `GetByIdAsync` / `GetAllAsync`, ciphertext is decrypted back into the in-memory `OneSignalRestApiKey` property for workflow use.
5. Views never receive the key — `AppDeploymentFormViewModel.FromEntity()` clears the field.

Purpose string: `"OneSignalRestApiKey"` (see `DataProtectionSecretService`).

---

## Rate limiting

| Policy | Limit | Endpoints |
|--------|-------|-----------|
| `token-api` | 10 req / 60 s per IP | `POST /api/form-access-tokens` |
| `form-submit` | 20 req / 60 s per IP | `POST /AppDeployment/Create`, `POST /AppDeployment/Edit` |
| `global` | 200 req / 60 s per IP | All endpoints |

Returns `429 Too Many Requests` with `Retry-After: 60`.

---

## Authentication

- **Admin UI**: Cookie-based ASP.NET Core Identity (single seeded admin).
- **Client forms**: Magic-link tokens (`/AppDeployment/Form/{token}`) — no login required.
- **Token API**: `X-Api-Key` header — no cookie session.

Cookie settings: `HttpOnly`, `Secure`, `SameSite=Strict`, 8-hour expiry.

---

## Security headers

Applied by `SecurityHeadersMiddleware` on every response:

- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy` (disables camera, mic, geolocation, payment)
- `Content-Security-Policy` (Bootstrap/jQuery CDN allow-list)

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full middleware pipeline.
