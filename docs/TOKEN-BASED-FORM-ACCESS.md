# Token-Based Form Access

## Overview

The **New App Deployment** form is no longer publicly reachable at `/AppDeployment/Create`.

Access works like a **magic link**:

1. An app admin calls a protected API with an API key + client details.
2. The API returns a unique token and a shareable `formUrl`.
3. The admin sends that URL to the client.
4. The client opens the URL:
  - **Not submitted yet** → Create form
  - **Already submitted** → Edit form for the same deployment

There is still no full user login system. This feature only gates **client form access and submission** behind unguessable tokens.

---



## Why this exists

Without authentication, anyone who knew `/AppDeployment/Create` could:

- open the New App Deployment form
- submit deployment data
- start the base GitHub Actions workflow (if they reached Edit and clicked Start)

Tokenized links solve that for clients without building a full identity system yet.

---



## End-to-end flow

```
Admin (Postman)
  POST /api/form-access-tokens
  Header: X-Api-Key: <FormAccess:ApiKey>
  Body: { clientName, clientAppName }
        │
        ▼
FormAccessTokensController
  • validates API key (constant-time compare)
  • FormAccessTokenService.IssueAsync
        │
        ├── existing active token for same client/app? → reuse it
        └── else → generate cryptic Base64Url token, save FormAccessTokens row
        │
        ▼
Response includes:
  token, formUrl, alreadyExisted, isSubmitted
        │
        ▼
Admin shares formUrl with client
  e.g. https://host/AppDeployment/Form/{token}
        │
        ▼
Client GET /AppDeployment/Form/{token}
        │
        ├── invalid / inactive → InvalidToken view
        ├── AppDeploymentId is null → Create view (App Name prefilled)
        └── AppDeploymentId set → Edit view for that deployment
        │
        ▼
Client POST Create (token required)
  • validates token again
  • saves AppDeployment + assets
  • MarkSubmittedAsync(token, deploymentId)
  • redirects to same form URL (Edit) with success message — no workflow yet
        │
        ▼
Client reviews / edits on Edit form
  POST Edit with same token → updates data (no workflow)
        │
        ▼
Client POST StartDeployment (token + deployment id)
  • validates token owns deployment
  • starts GitHub base workflow from saved assets
  • redirects to WorkflowProgress
        │
        ▼
Later GET same formUrl → Edit form
```

---



## Admin API — issue a token



### Endpoint

`POST /api/form-access-tokens`

### Auth


| Header         | Required | Value                                         |
| -------------- | -------- | --------------------------------------------- |
| `X-Api-Key`    | Yes      | Must match `FormAccess:ApiKey` in appsettings |
| `Content-Type` | Yes      | `application/json`                            |


If the key is missing/wrong, the API returns **401 Unauthorized**.

If `FormAccess:ApiKey` is empty in config, the API **fails closed** (always 401) so a misconfigured server cannot accept tokens.

### Request body

```json
{
  "clientName": "Acme Corp",
  "clientAppName": "Acme Auction"
}
```


| Field           | Rules                                                         |
| --------------- | ------------------------------------------------------------- |
| `clientName`    | Required, max 255 chars — organization / client label         |
| `clientAppName` | Required, max 255 chars — used to prefill `AppName` on Create |




### Success response (`200 OK`)

```json
{
  "token": "xY7_opaqueBase64UrlToken...",
  "clientName": "Acme Corp",
  "clientAppName": "Acme Auction",
  "formUrl": "https://localhost:7xxx/AppDeployment/Form/xY7_opaqueBase64UrlToken...",
  "alreadyExisted": false,
  "isSubmitted": false
}
```


| Field            | Meaning                                                        |
| ---------------- | -------------------------------------------------------------- |
| `token`          | Opaque value stored in DB and embedded in the URL              |
| `formUrl`        | Absolute link to share with the client                         |
| `alreadyExisted` | `true` if this client/app already had an active token (reused) |
| `isSubmitted`    | `true` if that token already created a deployment (edit mode)  |




### Re-issue behavior (important business rule)

Issuing again for the **same** `clientName` + `clientAppName` (trimmed, case-insensitive) does **not** create a second token.

The API returns the existing active token and sets `alreadyExisted: true`.

That prevents parallel magic links for one client/app pair.

---



## Postman example

1. Method: `POST`
2. URL: `https://localhost:<port>/api/form-access-tokens`
3. Headers:
  - `X-Api-Key`: `mad-admin-form-key-7f3c9e2a1b8d4f6e` (or your configured value)
  - `Content-Type`: `application/json`
4. Body (raw JSON):

```json
{
  "clientName": "Acme Corp",
  "clientAppName": "Acme Auction"
}
```

1. Copy `formUrl` from the response and send it to the client.

---



## Client form URLs


| URL                               | Behavior                                                |
| --------------------------------- | ------------------------------------------------------- |
| `GET /AppDeployment/Form/{token}` | Valid active token → Create or Edit                     |
| `GET /AppDeployment/Create`       | Blocked → InvalidToken view                             |
| `POST /AppDeployment/Create`      | Requires valid unused token in form body/route          |
| `POST /AppDeployment/Edit/{id}`   | Optional `token`; when present must own that deployment |
| `POST /AppDeployment/StartDeployment/{id}` | Optional `token`; starts workflow from saved deployment |

Create and Edit views include a hidden `token` field so posts keep the same capability.

### Create → Edit transition

After the first successful Create:

1. `FormAccessTokens.AppDeploymentId` is set to the new deployment Id
2. `SubmittedUtc` is stamped (UTC)
3. Opening the same form URL loads **Edit** instead of Create
4. Edit posts with the token must target that linked Id only (cannot edit someone else’s deployment)

### Save vs start workflow

Saving and starting the deployment process are **decoupled**:

| Action | Endpoint | Starts GitHub workflow? |
|--------|----------|-------------------------|
| **Save deployment** (Create first time) | `POST Create` | No — redirects to Edit with success message |
| **Save deployment** (Edit) | `POST Edit` | No — stays on Edit with success message |
| **Start App Deployment Process** | `POST StartDeployment` | Yes — uses saved DB row + asset paths |

On Create, **Start App Deployment Process** is shown but **disabled** until the deployment exists (after first save, on Edit it is enabled).

See `docs/GITHUB-INTEGRATION.md` for dispatch details, retries, and `PublicBaseUrl` / ngrok.

---



## Configuration

Section: `FormAccess` in `MobileAppDeployment/appsettings.json` (and Development override).

```json
"FormAccess": {
  "ApiKey": "mad-admin-form-key-7f3c9e2a1b8d4f6e",
  "PublicBaseUrl": ""
}
```


| Key             | Purpose                                                                                                                     |
| --------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `ApiKey`        | Fixed admin key for `X-Api-Key`. Change for real environments; do not share with clients.                                   |
| `PublicBaseUrl` | Optional absolute base used when building `formUrl` (e.g. ngrok). Empty → use the host that received the admin API request. |


Bound to `MobileAppDeployment.Options.FormAccessOptions` in `Program.cs`.

**Security note:** the default key in repo is for local/dev convenience. For any shared/staging/production host, rotate `FormAccess:ApiKey` via environment variables or secret store and never commit the production value.

---



## Database



### Table: `FormAccessTokens`


| Column            | Type                     | Notes                                         |
| ----------------- | ------------------------ | --------------------------------------------- |
| `Id`              | int PK                   | Identity                                      |
| `Token`           | varchar(64) unique       | Opaque Base64Url string                       |
| `ClientName`      | varchar(255)             | From admin API                                |
| `ClientAppName`   | varchar(255)             | From admin API / App Name prefill             |
| `AppDeploymentId` | int? FK → AppDeployments | Null until first submit; `ON DELETE SET NULL` |
| `IsActive`        | bool                     | Inactive tokens are rejected                  |
| `CreatedUtc`      | timestamptz              | Issuance time                                 |
| `SubmittedUtc`    | timestamptz?             | First successful Create time                  |




### Migration

`MobileAppDeployment/Migrations/20260708123000_AddFormAccessTokens.cs`

Apply with:

```powershell
cd MobileAppDeployment
dotnet ef database update
```

---



## Code map (for new developers)


| Piece      | Path                                                                          | Role                                        |
| ---------- | ----------------------------------------------------------------------------- | ------------------------------------------- |
| Options    | `Options/FormAccessOptions.cs`                                                | Config binding                              |
| Entity     | `Models/FormAccessToken.cs`                                                   | DB row                                      |
| API DTOs   | `Models/CreateFormAccessTokenRequest.cs`, `Models/FormAccessTokenResponse.cs` | Request/response                            |
| Repository | `Repositories/FormAccessTokenRepository.cs`                                   | EF queries                                  |
| Service    | `Services/FormAccessTokenService.cs`                                          | Issue / resolve / mark submitted; token RNG |
| Admin API  | `Controllers/FormAccessTokensController.cs`                                   | `POST /api/form-access-tokens`              |
| Form MVC   | `Controllers/AppDeploymentController.cs`                                      | `Form`, gated `Create`/`Edit`/`StartDeployment` |
| Views      | `Views/AppDeployment/Create.cshtml`, `Edit.cshtml`, `_DeploymentFormFooter.cshtml`, `InvalidToken.cshtml` | Save + Start buttons, token hidden field |
| DbContext  | `Data/ApplicationDbContext.cs`                                                | `FormAccessTokens` set + indexes            |




### Token generation details

`FormAccessTokenService.GenerateToken()`:

- fills 32 random bytes (`RandomNumberGenerator`)
- encodes as URL-safe Base64 (no padding, `+` → `-`, `/` → `_`)
- result is opaque (not a JWT; no claims inside the string)

All authorization state lives in the database row looked up by that string.

---



## What is still open (not part of this feature)


| Area                         | Current state                                                    |
| ---------------------------- | ---------------------------------------------------------------- |
| Index / Details / Delete     | Still reachable without a client token (internal admin browsing) |
| Admin Index “Edit by Id”     | Still allowed without token                                      |
| Full login / roles           | Not implemented                                                  |
| Token expiry / one-time open | Not implemented — token stays valid while `IsActive`             |
| Token revoke API             | Not implemented — set `IsActive = false` in DB if needed         |


Those can be added later if/when full authentication ships.

---



## Manual test checklist

1. Apply migration (`dotnet ef database update`).
2. Start the app.
3. Open `/AppDeployment/Create` directly → InvalidToken page.
4. Postman: create token with correct `X-Api-Key` → receive `formUrl`.
5. Postman: wrong API key → 401.
6. Open `formUrl` → Create form, App Name prefilled from `clientAppName`.
7. Submit Create successfully → success message, Edit form, **Start** button enabled.
8. Open same `formUrl` again → Edit form.
9. Update a field and submit Save → success, stay on Edit (no workflow).
10. Click **Start App Deployment Process** → workflow progress page.
11. Postman issue again for same client/app → same token, `alreadyExisted: true`.
12. Index no longer shows “+ New deployment” CTA; banner points admins at the API.

---



## Related docs

- `docs/GITHUB-INTEGRATION.md` — what happens when the user clicks **Start App Deployment Process**
- `docs/ENTITY-FRAMEWORK-CORE-GUIDE.md` — how migrations work in this project

