# Azure Web App deployment

The [deploy.yml](.github/workflows/deploy.yml) workflow builds and publishes `src/Nestify.Api`, then deploys the published output to the production slot of an Azure Web App.

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
