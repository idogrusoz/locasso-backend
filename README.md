# Locasso Backend

## Authentication Flow

This backend uses Azure App Service Authentication (Easy Auth) with Sign in with Apple and Google providers. The authentication flow works as follows:

1. Users sign in with Apple/Google in the Expo app (on the device)
2. In production:
   - The app directs the user to the Azure App Service Authentication endpoint
   - Azure validates the authentication with the provider (Apple/Google)
   - Azure redirects back to the app with a token in the `x-zumo-auth` header
   - Our API receives user information from this token and checks/creates the user in our database
3. In development:
   - The app receives a token directly from Apple/Google 
   - The app passes user information to the backend as query parameters with `_dev_mode=true`
   - Our API creates or finds the user in the database

## Setup Instructions

### Prerequisites

- .NET 9 SDK
- PostgreSQL
- Docker and Docker Compose (for local development)
- Azure account
- Apple Developer Program membership

### Database Setup

1. Create a PostgreSQL database
2. Update the connection string in `appsettings.json`
3. Run migrations:
   ```
   dotnet ef migrations add InitialMigration --project src/Web/Web.csproj
   dotnet ef database update --project src/Web/Web.csproj
   ```

### Local Development with Docker

1. Build and run the containers:
   ```
   docker-compose up --build
   ```
2. Access the API at http://localhost:9090
3. Access Swagger at http://localhost:9090/swagger/index.html

### Azure App Service Authentication Setup

1. Deploy the application to Azure App Service
2. Configure Authentication in your App Service:
   - Upload the `authentication.json` file to the App Service using the Azure CLI or Portal
   - Configure the Apple and Google identity providers

3. For Apple Sign In:
   - Create an App ID and a service ID in the Apple Developer portal
   - Configure Sign in with Apple for your App ID
   - Generate a client secret JWT
   - Set the App Service application setting `APPLE_GENERATED_CLIENT_SECRET` to the JWT value
   - Set the `APPLE_CLIENT_ID` setting to your Apple Service ID

4. For Google Sign In:
   - Create OAuth credentials in the Google Cloud Console
   - Set the App Service application setting `GOOGLE_CLIENT_SECRET` to your OAuth client secret
   - Configure the authorized redirect URLs to include your Azure app URL

### GitHub Pipeline Secrets

For CI/CD with GitHub Actions, set up these secrets:

1. **Database credentials**:
   - `CONNECTION_STRING`: PostgreSQL connection string for production

2. **Azure authentication secrets**:
   - `AZURE_AD_B2C_INSTANCE`: Your Azure AD B2C instance URL
   - `AZURE_AD_B2C_CLIENT_ID`: Your Azure AD B2C client ID
   - `AZURE_AD_B2C_DOMAIN`: Your Azure AD B2C domain
   - `AZURE_AD_B2C_POLICY_ID`: Your sign-up/sign-in policy ID

3. **Apple authentication**:
   - `APPLE_CLIENT_SECRET`: The JWT token for Apple Sign In
   - `APPLE_CLIENT_ID`: Your Apple Developer Service ID
   - `APPLE_KEY_ID`: Your Apple private key ID
   - `APPLE_TEAM_ID`: Your Apple Developer Team ID

4. **Deployment credentials**:
   - `AZURE_CREDENTIALS`: JSON credentials for Azure deployment
   - `AZURE_WEBAPP_NAME`: The name of your Azure App Service

## User Entity

The User entity stores:
- Unique ID
- External ID (from authentication provider)
- Email
- Name
- Photo URL
- Role (Admin, Guide, or Traveler)
- Authentication provider
- Created timestamp
- Last login timestamp

## Authentication Design Pattern

This implementation uses the following design patterns:

1. **Repository Pattern**: For data access separation
2. **Mediator Pattern**: Using MediatR for command/query handling
3. **Strategy Pattern**: For different authentication providers

## Apple Sign-In JWT Generation

For generating the required Apple client secret JWT, you can use a tool like the following C# code:

```csharp
using Microsoft.IdentityModel.Tokens;

public static string GetAppleClientSecret(string teamId, string clientId, string keyId, string p8key)
{
    string audience = "https://appleid.apple.com";

    string issuer = teamId;
    string subject = clientId;
    string kid = keyId;

    IList<Claim> claims = new List<Claim> {
        new Claim ("sub", subject)
    };

    CngKey cngKey = CngKey.Import(Convert.FromBase64String(p8key), CngKeyBlobFormat.Pkcs8PrivateBlob);

    SigningCredentials signingCred = new SigningCredentials(
        new ECDsaSecurityKey(new ECDsaCng(cngKey)),
        SecurityAlgorithms.EcdsaSha256
    );

    JwtSecurityToken token = new JwtSecurityToken(
        issuer,
        audience,
        claims,
        DateTime.Now,
        DateTime.Now.AddDays(180),
        signingCred
    );
    token.Header.Add("kid", kid);
    token.Header.Remove("typ");

    JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

    return tokenHandler.WriteToken(token);
}
``` 