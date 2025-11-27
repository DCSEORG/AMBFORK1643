![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# Expense Management System

A modernized cloud-native Azure application for expense management, converted from a legacy system using GitHub Copilot Coding Agent.

## Features

- **Modern Web UI**: ASP.NET Core 8.0 Razor Pages with Bootstrap 5
- **REST APIs**: Full CRUD operations with Swagger documentation
- **AI-Powered Chat**: Natural language expense management via Azure OpenAI (GPT-4o)
- **Secure Authentication**: Azure AD-only authentication with Managed Identity
- **Azure Infrastructure**: Bicep templates for Infrastructure as Code

## Quick Start

### Prerequisites
- Azure CLI installed and logged in (`az login`)
- Active Azure subscription
- .NET 8.0 SDK (for local development)

### Deployment

#### Option 1: Basic Deployment (without AI)
```bash
# Make script executable
chmod +x deploy.sh

# Deploy to Azure
./deploy.sh
```

#### Option 2: Full Deployment (with AI Chat)
```bash
# Make script executable
chmod +x deploy-with-chat.sh

# Deploy to Azure with GenAI services
./deploy-with-chat.sh
```

### Post-Deployment

Access the application at:
- **Main UI**: `https://<app-name>.azurewebsites.net/Index`
- **Approve Expenses**: `https://<app-name>.azurewebsites.net/Approve`
- **Chat Assistant**: `https://<app-name>.azurewebsites.net/Chat`
- **API Docs**: `https://<app-name>.azurewebsites.net/swagger`

## Local Development

```bash
# Navigate to the project
cd src/ExpenseManagement/ExpenseManagement

# Update connection string in appsettings.Development.json
# Use "Authentication=Active Directory Default" for local dev

# Run the application
dotnet run
```

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for the Azure services diagram.

### Components
- **Azure App Service** (S1 SKU) - Hosts the web application
- **Azure SQL Database** (Basic tier) - Stores expense data
- **User Assigned Managed Identity** - Secure authentication between services
- **Azure OpenAI** (optional) - Powers the AI chat assistant
- **Azure Cognitive Search** (optional) - RAG capabilities for enhanced responses

## Project Structure

```
├── Infrastructure/           # Bicep templates
│   ├── main.bicep           # Main deployment template
│   ├── app-service.bicep    # App Service configuration
│   ├── azure-sql.bicep      # SQL Database configuration
│   ├── managed-identity.bicep
│   └── genai.bicep          # Azure OpenAI & Search
├── src/ExpenseManagement/   # ASP.NET Core application
│   └── ExpenseManagement/
│       ├── Api/             # REST API controllers
│       ├── Models/          # Data models
│       ├── Services/        # Business logic
│       └── Pages/           # Razor Pages
├── Database-Schema/         # SQL schema files
├── Legacy-Screenshots/      # Original UI screenshots
├── deploy.sh               # Basic deployment script
├── deploy-with-chat.sh     # Full deployment with GenAI
└── stored-procedures.sql   # Database stored procedures
```

## Security

- ✅ Azure AD-Only Authentication (MCAPS compliant)
- ✅ Managed Identity (no passwords in code)
- ✅ HTTPS enforced
- ✅ TLS 1.2 minimum
- ✅ FTPS disabled

## Credits

Created using [App-Mod-Booster](https://github.com/DougChisholm/App-Mod-Booster) - a project demonstrating how GitHub Copilot Coding Agent can modernize legacy applications.
