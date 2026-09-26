# Deploy Nestify API to Render

This repository deploys `src/Nestify.Api` as a Docker Web Service. The Docker image listens on port `8080`, and Swagger UI is available at `/swagger` in Production.

## 1. Prepare Neon

1. Create a Neon PostgreSQL project and database.
2. Run `src/Nestify.Database/nestify.sql` against the database using the **direct** Neon connection string.
3. Apply files in `src/Nestify.Database/migrations/` in filename order.
4. Run the seed scripts only when the database is ready for sample data.
5. Keep two Neon connection strings available:
   - **Direct connection**: use for schema changes and migrations. It normally contains `-pooler` nowhere in the hostname.
   - **Pooled connection**: use for the running API when the service has many short-lived connections. It normally contains `-pooler` in the hostname.

Nestify currently uses Dapper and SQL files, not EF Core. There is no `dotnet ef database update` step in this repository. If EF Core is added later, run migrations with the direct Neon connection, never through the transaction pooler. Do not run schema migrations automatically on every Web Service startup.

The API normalizes the database connection to `SSL Mode=Require;Trust Server Certificate=true;` for Neon. `Trust Server Certificate=true` is appropriate here because Neon provides the TLS endpoint; the connection is still encrypted.

## 2. Connect GitHub to Render

1. Push this repository to GitHub, including `Dockerfile` and `.dockerignore`.
2. In Render, choose **New > Web Service**.
3. Connect GitHub, authorize the repository, and select the Nestify repository.
4. Select the branch to deploy, usually `main`.
5. Set **Language** to **Docker** and leave the Dockerfile path as `./Dockerfile`.
6. Set the service **Region** close to the Neon region when possible.
7. Choose an instance size and create the service. Render builds from the repository root.
8. In **Settings > Environment**, add the variables below, then use **Manual Deploy > Deploy latest commit**.

Render assigns a URL such as `https://nestify-api.onrender.com`. Do not add a trailing slash when using it as the API base URL.

## 3. Configure Render environment variables

Add these values in Render. Use **Secret** visibility for passwords, tokens, and provider URLs.

| Key | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` |
| `ConnectionStrings__DefaultConnection` | Neon pooled or direct PostgreSQL URI, with `sslmode=require` |
| `Jwt__Secret` | A random secret of at least 32 characters |
| `Jwt__Issuer` | A stable value such as `Nestify` |
| `Jwt__Audience` | A stable value such as `Nestify.Client` |
| `CLOUDINARY_URL` | Cloudinary URL used for image uploads |
| `UPLOAD_PICTURE` | Cloudinary unsigned upload preset |
| `GEMINI_API_KEY` | Google Gemini API key, if assistant endpoints are used |

`ConnectionStrings__DefaultConnection` maps to `ConnectionStrings:DefaultConnection` in ASP.NET Core. The API also accepts the existing local-development variables `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `JWT_SECRET`, `JWT_ISSUER`, and `JWT_AUDIENCE`, but the Render connection-string and `Jwt__*` names above are preferred.

Do not commit any of these values. Render environment variables are injected at runtime and are not part of the Docker image.

## 4. Verify the deployment

After the first deploy, open:

```text
https://YOUR-RENDER-SERVICE.onrender.com/swagger
```

The page should load over HTTPS even though the container listens internally on HTTP port 8080. Render terminates public TLS and forwards traffic to the container.

Use the service URL as the API base URL in the Blazor client. The API enables public CORS for browser calls and uses bearer JWTs for protected endpoints. Never put `Jwt__Secret`, a database password, or provider keys in the browser application.

## 5. Live verification checklist

### URL and startup

- Open `https://YOUR-RENDER-SERVICE.onrender.com/swagger` and confirm Swagger UI loads.
- Confirm Render logs show a healthy deployment and no missing-configuration exception.
- Open one public GET endpoint from Swagger and confirm it returns JSON rather than a 502 or 500.

### Database reads and writes

- Register a test account through `POST /api/v1/auth/register` using a unique email.
- Log in through `POST /api/v1/auth/login` and confirm the response contains an access token.
- Call a public lookup/read endpoint and confirm it returns seeded data from Neon.
- Authorize Swagger with `Bearer <access-token>` and create a small test record through a protected POST endpoint.
- Read the record back, then delete or clean it up using the corresponding endpoint. Confirm the change in Neon if needed.

### JWT authentication

- Call a protected endpoint without an `Authorization` header and confirm `401 Unauthorized`.
- Call it with `Authorization: Bearer <access-token>` and confirm the expected `200`/`201` response.
- Change one character in the token and confirm the request returns `401`.
- Confirm a user cannot call an admin-only endpoint and receives `403` (or `401` where the endpoint intentionally hides authorization details).
- Exercise refresh-token and logout endpoints with the test account, if those endpoints are part of the current test pass.

### Client integration

- Set the Blazor API base URL to the Render HTTPS URL.
- Sign in from the deployed client and inspect the browser Network panel for CORS errors.
- Confirm authenticated requests carry `Authorization: Bearer ...` and do not expose any server secret.
