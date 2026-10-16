# K Sweet Kreations Co API

ASP.NET Core / Entity Framework Core targeting .NET 10, with SQL Server persistence and authenticated Customer, Staff and Admin website workflows.

For Azure Basic-tier hosting in South Africa North, see [Azure deployment](deploy/README.md).

## Configure and run

From `K-Sweet-Kreations-Co-Backend-API`, install the .NET 10 SDK and configure local secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your SQL Server connection string>"
dotnet user-secrets set "Jwt:Key" "<a cryptographically random secret of at least 32 bytes>"
dotnet user-secrets set "SeedAdmin:Email" "<your admin email>"
dotnet user-secrets set "SeedAdmin:Password" "<a unique password of at least 12 characters>"
dotnet user-secrets set "SeedAdmin:Enabled" "true"
dotnet ef database update
dotnet run --launch-profile http
```

Seeding is explicitly opt-in and creates an Admin only when no Admin exists. Disable `SeedAdmin:Enabled` after initial bootstrap. Customer registration and Admin-created Staff accounts are available through the website. JWT issuer, audience and expiry defaults are in appsettings; no signing key is committed. Production uses hosting secrets/environment variables such as `Jwt__Key` and `ConnectionStrings__DefaultConnection`.

The website dev server proxies its API/media requests to `http://localhost:5088`. The API serves a built website from `wwwroot/index.html` when frontend build files are copied there. Production requires HTTPS. Use same-origin hosting or same-site subdomains with exact `Cors:AllowedOrigins`; session cookies use HTTP-only, secure-in-production, `SameSite=Strict` settings and server revocation.

## Database changes

`WebsiteIntegration` aligns stored Consultant roles and historical statuses, adds contact/product/collection fields, reviews and blocked dates, and protects quotation/request conversion and payment references with unique indexes. `RetrySafeSubmissions` persists retry receipts. Back up and review the existing database before deployment. Duplicate historical payment references or request orders stop migration rather than deleting records. Historical payment-stage orders require collection approval again; verified payment records are preserved.

Do not use automatic `EnsureCreated` for the real database. The normal application uses SQL Server migrations. Only the explicit Testing environment uses disposable SQLite databases for HTTP integration tests.

Private inspiration images and payment proofs live outside `wwwroot`, in `PrivateUploads` by default. Override `Storage:PrivateRoot` with a persistent private storage directory. Product photographs live under `wwwroot/uploads/products`; preserve this directory across deployments. Validate filesystem permissions and back up both database and media together. Private uploads are excluded from publish content.

## API and validation

Identity routes are under `/api/Auth`; workflow routes are under `/api/website`. The website integration document in the frontend repository lists the full contract. Staff can perform operational work; Admin additionally manages products, blocked dates, payment verification, feedback publication and Staff accounts. Customers can access only their own records. Public browsing and enquiries do not require an account.

The API validates inputs and actual upload signatures, recalculates checkout totals, enforces collection rules and payment/fulfilment guards, rejects repeated payment verification, and converts a request to one order transactionally. Stable submission UUIDs prevent duplicate orders/requests on network retries. Errors avoid internal details; logs retain server diagnostics. Rate limits default to 120 requests/minute/IP and 15 sign-in/registration attempts/minute/IP, configurable under `RateLimits`. Untrusted browser origins are rejected for mutations.

The old domain controller classes are retained as non-HTTP historical regression code (`NonController`). Their incompatible routes are retired. New integrations must use the website contract; database properties with historical Consultant names preserve existing foreign keys.

## Verification

From this repository:

```sh
dotnet restore
dotnet test K-Sweet-Kreations-Co-Backend-API.slnx
```

With the frontend checkout beside this repository, run `npm run test:integration` there after building this solution in Debug. The test launcher creates its own temporary SQLite database, signing key, Admin and private upload directory, and never connects to the configured SQL Server. SQL Server migrations still require staging validation.

Card processing, notification delivery, password-recovery email and Staff MFA require additional integrations. EFT verification is a real authorised manual operation; a submitted proof is not counted as money received until Admin verifies it. Paid-order refunds remain manual.
