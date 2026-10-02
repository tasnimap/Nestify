# Azure Web App deployment

The [deploy.yml](../.github/workflows/deploy.yml) workflow builds and publishes `src/Nestify.Api`, then deploys the published output to the production slot of an Azure Web App.

It runs automatically for pushes to `main`. Use **Actions > Deploy Nestify API > Run workflow** for a manual deployment.

## Required GitHub secrets

Create these repository secrets under **Settings > Secrets and variables > Actions**:

| Secret | Value |
|---|---|
| `AZURE_CLIENT_ID` | Application (client) ID of the Microsoft Entra app registration used by the federated credential |
| `AZURE_TENANT_ID` | Microsoft Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID containing the Web App |
| `AZURE_WEBAPP_NAME` | Azure App Service Web App name |

Configure an Entra federated identity credential for the GitHub repository and branch (`main`) or environment used by the workflow. Grant the app registration the least-privileged role needed to deploy the Web App, such as **Website Contributor** scoped to that App Service.

## App Service application settings

Add these settings under **Azure Portal > Web App > Configuration > Application settings**. App Service maps double underscores to nested ASP.NET Core configuration keys.

| Setting | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` |
| `ConnectionStrings__DefaultConnection` | Production PostgreSQL connection string or URI |
| `Jwt__Secret` | Random signing secret of at least 32 characters |
| `Jwt__Issuer` | Stable JWT issuer, such as `Nestify` |
| `Jwt__Audience` | Stable JWT audience, such as `Nestify.Client` |
| `CLOUDINARY_URL` | Cloudinary service URL |
| `UPLOAD_PICTURE` | Cloudinary unsigned upload preset |
| `GEMINI_API_KEY` | Gemini API key when assistant endpoints are enabled |

If a connection string is not supplied, the API falls back to the separate `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, and optional `DB_SSL_MODE` variables. Keep all secrets in App Service settings and never commit their values.

## Database migrations

This repository currently uses Dapper and checked-in SQL migrations, not Entity Framework Core. Apply the SQL files in `src/Nestify.Database/migrations/` using a controlled release process and the direct PostgreSQL connection before deploying the application. Do not run schema changes automatically on every application startup.

The workflow contains an opt-in migration step controlled by the repository variable `RUN_DATABASE_MIGRATIONS=true`. If enabled, also create the `AZURE_MIGRATION_CONNECTION_STRING` repository secret with a direct database connection. Enable the step only after adding EF Core packages and migrations to the API project; it runs before publishing:

```bash
dotnet ef database update --project src/Nestify.Api/Nestify.Api.csproj \
  --startup-project src/Nestify.Api/Nestify.Api.csproj
```

For production, a reviewed EF migration bundle is an alternative:

```bash
dotnet ef migrations bundle --self-contained -r linux-x64 -o efbundle
./efbundle --connection "$ConnectionStrings__DefaultConnection"
```

Run migrations before the deployment step and use a direct database connection rather than a transaction pooler.
