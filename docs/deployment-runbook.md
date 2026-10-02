# Deployment runbook

This runbook covers the Render full-stack deployment and the Azure App Service backend deployment.

## Verify a deployment

### Azure backend

1. Open the `Deploy Nestify API` workflow in GitHub Actions.
2. Confirm both `Build and publish API` and `Deploy to Azure Web App` finish successfully.
3. Open the Azure App Service Swagger endpoint:

   ```text
   https://YOUR-AZURE-APP.azurewebsites.net/swagger
   ```

4. Check **App Service > Monitoring > Log stream** if the application does not start.

### Render application

1. Open the Render frontend URL.
2. Confirm the frontend can load data from the Render API.
3. Check the Render service logs for startup or database connection errors.
4. Verify a public API endpoint and one authenticated flow.

## Roll back an Azure deployment

1. Open **GitHub > Actions > Deploy Nestify API**.
2. Identify the last successful workflow run and its commit.
3. Revert the faulty change in a pull request, or deploy the known-good commit from a controlled branch.
4. Merge the revert to `main`; the normal workflow will redeploy it.
5. Confirm Swagger and a representative authenticated endpoint work.

Do not delete the App Service or recreate OIDC credentials during a rollback.

## Roll back a Render deployment

1. Open the affected Render service.
2. Open its deployment history.
3. Select the last known-good deployment.
4. Use **Rollback** or redeploy that commit.
5. Verify the frontend, API, and database connectivity.

## Monitoring and alerts

- Review GitHub Actions workflow results after each push to `main`.
- Use Azure **Log stream**, **Diagnose and solve problems**, and **Metrics** for the App Service.
- Use Render service logs and deployment history for the Render services.
- Configure Azure cost alerts for the App Service subscription, especially when using a paid plan.
- Treat database, JWT, Cloudinary, and Gemini configuration errors as startup incidents; never log their values.

## Common failures

| Symptom | Checks |
|---|---|
| `AADSTS70021` during Azure login | Confirm the federated credential matches `tasnimap/Nestify`, branch `main`, and audience `api://AzureADTokenExchange`. |
| Azure authorization failure | Confirm the Entra app has Website Contributor on the Web App. |
| Web App not found | Confirm `AZURE_WEBAPP_NAME` contains only the App Service name. |
| API starts with a database error | Check `ConnectionStrings__DefaultConnection` or the `DB_*` settings and database firewall rules. |
| Swagger loads but frontend requests fail | Confirm the frontend API URL and CORS configuration. |
| Deployment builds but the app is unhealthy | Inspect Azure Log stream and verify required production settings are present. |

Never enable the optional EF migration step for this repository unless EF Core migrations and the required packages have been added. The current application uses Dapper and checked-in SQL migrations.
