# Azure Web App deployment

The [deploy.yml](.github/workflows/deploy.yml) workflow builds and publishes `src/Nestify.Api`, then deploys the published output to the production slot of an Azure Web App.

It runs automatically for pushes to `main`. Use **Actions > Deploy Nestify API > Run workflow** for a manual deployment.

