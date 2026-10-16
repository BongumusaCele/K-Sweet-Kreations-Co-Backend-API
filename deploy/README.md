# Azure deployment

Current website: https://k-sweet-creations-co.azurewebsites.net. It uses the original Basic B1 plan and SQL database. The former React-Bakery site runs a small Node redirect server after cutover, so it does not start another full API on the shared plan. Both sites are in South Africa North.

This deployment hosts the built React website and ASP.NET Core API in one Linux App Service, with a separate Azure SQL Database. The requested configuration is **Basic B1 App Service** and **Basic Azure SQL (5 DTU, 2 GB)** in **South Africa North**, inside the existing college resource group. These are paid tiers charged against the subscription/available credits. Existing applications and their Shared App Service plan are not modified.

## Deploy or update

Run from the shared Project directory in PowerShell 7 with Azure CLI, .NET 10, Node.js and both repositories available:

```powershell
az login
./K-Sweet-Kreations-Co-Web-API/deploy/Deploy-Azure.ps1 -Mode Prepare
./K-Sweet-Kreations-Co-Web-API/deploy/Deploy-Azure.ps1 -Mode Validate
./K-Sweet-Kreations-Co-Web-API/deploy/Deploy-Azure.ps1 -Mode Deploy
```

Parameters at the top of the script identify the subscription, resource group, region and resource names. Reuse the original parameter values on updates. `Prepare` builds React with `/api`, publishes the API, adds React's production files to the package and creates a new zip. `Validate` checks the ARM template against Azure without creating resources. `Deploy` provisions the Basic resources, runs migrations against the new database, creates the Admin account and a restricted database user, sets the app's configuration, deploys the package and checks the live frontend and API/database health.

No database from the local development machine is copied. On a later deployment, migrations update the Azure schema and existing Azure records are preserved. Admin seeding only creates an Admin when none exists.

For code changes that require no database migrations, run `-Mode Prepare` followed by `-Mode Update`. Update only uploads the package and checks health; it preserves the database, accounts, resource tiers and application settings.

For a reviewed additive schema update, run `-Mode Prepare`, `-Mode Migrate`, then `-Mode Update`. Migrate executes pending EF migrations using private deployment credentials, without provisioning resources, seeding accounts or changing the runtime database password. It permits only the current computer's IPv4 address temporarily and removes that firewall rule in a finally block.

`ProductDeletion` adds nullable `Products.DeletedAt`. Deleting a product marks it deleted and unavailable; catalogue queries and new checkouts exclude it, while historical orders retain their product references and photographs. Existing rows start with no deletion date. No business records are removed by the migration.

The script prints the actual Azure website URL after deployment. Azure may use a regional hostname with a generated suffix; use that printed URL instead of guessing the hostname from the app name. React and `/api` share this origin, so the HTTP-only sign-in cookie works without cross-site CORS settings.

## Credentials

Generated credentials are saved in `credentials.local.json` beside the script. On Windows this file is restricted to the current Windows account and SYSTEM. It contains the first Admin login password, SQL deployment credentials, the runtime database password and the JWT signing key. It is ignored by Git. Do not attach it to an issue or publish it. `parameters.local.json`, `settings.local.json`, `deployment.local.json` and build artifacts are also ignored.

After successful deployment, `admin-login.local.json` contains only the website URL and Admin login details. This file is also private and ignored by Git. To repeat the live verification without adding sample business records, run `node K-Sweet-Kreations-Co-Web-API/deploy/Smoke-Azure.mjs` from the Project directory. It checks HTTPS, database health, the Admin session cookie, registered customer visibility and sign-out.

The API's runtime connection uses the contained `ksk_app` database user with data-reader/data-writer roles. SQL administrator credentials are used locally for migrations, not by the running website. Admin bootstrap is disabled on the hosted HTTP app.

Application configuration is stored in Azure App Service settings. For a later hardened production rollout, move secrets to an approved secret store and establish a rotation procedure. Do not remove the local deployment credentials until they have been backed up securely or replaced with a managed deployment identity.

## Upload persistence and networking

Uploads use the `ksweetmedia4d7f` StorageV2 account (Standard LRS, South Africa North). Both containers are private: `product-images` holds `products/` files, and `private-documents` holds `payment-proofs/` and `custom-cakes/`. The API serves public product images at stable `/uploads/products/` URLs and authorizes each private proof or inspiration-image download. Storage credentials stay in App Service settings and ignored, ACL-protected deployment files; they never reach the browser.

Product images accept JPG/PNG/WebP up to 2 MB. EFT proofs accept PDF/JPG/PNG/WebP up to 5 MB. Server validation checks file extensions, size and signatures; the PDF response downloads as an attachment with its correct content type. Product creation and editing submit a single multipart form; no typed image path is needed.

On startup with Blob Storage configured, existing `/home/data/ksweet/products` and `/home/data/ksweet/private` files are copied to their containers without changing database references or deleting originals. Local development without `Storage:BlobConnectionString` continues to use filesystem storage. `Verify-AzureStorage.ps1` checks upload/download/deletion with temporary blobs and creates no database records.

`Move-AzureWebsite.ps1 -Mode Configure` copies the original app settings to the new website and connects both apps to Blob Storage. Deploy the updated package to the original app first so it migrates files, then deploy to the new app and verify it. Only then run `-Mode Cutover` to redirect the former hostname and update the private Admin login file. Customer accounts, products and orders use the same database throughout; users sign in again on the new hostname.

Azure SQL firewall rules permit the App Service's possible outbound IPv4 addresses. A separate rule for the local deployment computer is created during migration and removed in a `finally` block. The broad “Allow Azure services” rule is not enabled. Re-run deployment if App Service outbound addresses change after a hosting-plan move.

`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set for Azure's reverse proxy so HTTPS, origin validation and secure cookies see the external request scheme. HTTPS-only hosting, TLS 1.2 minimum and disabled FTP are configured in the template.

## Verify

- Open the printed website URL and check catalogue/account pages.
- `GET /api/health` must return `{"status":"healthy"}` with HTTP 200.
- Sign in with the Admin email and the password from the private local file; add approved products and prices.
- Register a separate Customer to exercise request, quotation, order and EFT-proof workflows.
- Verify that a product photograph and a private proof remain available after an application restart.

An empty Azure database has no sample products or customer orders. Card payments, password-recovery email and automated notifications remain separate integrations. Basic tiers are appropriate for this student deployment; they do not provide staging slots or an automatically configured disaster-recovery setup.
