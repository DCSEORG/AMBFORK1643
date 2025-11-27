# Azure Services Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                        Expense Management System - Azure Architecture                 │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                       │
│   ┌─────────────┐                                                                     │
│   │   User      │                                                                     │
│   │  Browser    │                                                                     │
│   └──────┬──────┘                                                                     │
│          │                                                                            │
│          │ HTTPS                                                                      │
│          ▼                                                                            │
│   ┌─────────────────────────────────────────┐                                        │
│   │     Azure App Service (S1 SKU)          │◄─────────────────────────┐             │
│   │     ─────────────────────────           │                          │             │
│   │     • ASP.NET Core 8.0 Razor Pages      │      ┌───────────────────┴───────────┐ │
│   │     • REST APIs (Swagger)               │      │  User Assigned Managed        │ │
│   │     • Chat UI                           │──────┤  Identity                      │ │
│   │                                         │      │  (mid-appmodassist-xxx)       │ │
│   │     Location: UK South                  │      └───────────────────┬───────────┘ │
│   └──────────────┬──────────────────────────┘                          │             │
│                  │                                                      │             │
│                  │ Managed Identity Auth                                │             │
│                  ▼                                                      │             │
│   ┌─────────────────────────────────────────┐                          │             │
│   │     Azure SQL Database                  │◄─────────────────────────┘             │
│   │     ─────────────────                   │      (db_datareader, db_datawriter,    │
│   │     • Server: sql-expensemgmt-xxx       │       EXECUTE permissions)             │
│   │     • Database: Northwind               │                                        │
│   │     • Tier: Basic                       │                                        │
│   │     • Entra ID Only Auth                │                                        │
│   │                                         │                                        │
│   │     Location: UK South                  │                                        │
│   └─────────────────────────────────────────┘                                        │
│                                                                                       │
│   ═══════════════════════════════════════════════════════════════════════════════    │
│                    Optional GenAI Resources (deploy-with-chat.sh)                     │
│   ═══════════════════════════════════════════════════════════════════════════════    │
│                                                                                       │
│   ┌─────────────────────────────────────────┐      ┌─────────────────────────────┐   │
│   │     Azure OpenAI Service                │      │  Azure Cognitive Search     │   │
│   │     ────────────────────                │      │  ─────────────────────      │   │
│   │     • Name: aoai-expensemgmt-xxx        │      │  • Name: search-xxx         │   │
│   │     • Model: GPT-4o                     │      │  • SKU: Basic               │   │
│   │     • SKU: S0                           │      │                             │   │
│   │     • Capacity: 8                       │      │  Location: Sweden Central   │   │
│   │                                         │      └─────────────────────────────┘   │
│   │     Location: Sweden Central            │                                        │
│   │     (Required for GPT-4o availability)  │                                        │
│   └─────────────────────────────────────────┘                                        │
│                                                                                       │
└─────────────────────────────────────────────────────────────────────────────────────┘

                              Data Flow
                              ─────────

    1. User accesses App Service via HTTPS
    2. App Service authenticates to SQL using Managed Identity
    3. All database operations use stored procedures
    4. Chat requests route through Azure OpenAI for natural language processing
    5. AI uses function calling to interact with expense APIs

                         Security Features
                         ─────────────────

    ✓ Azure AD-Only Authentication on SQL Server (MCAPS compliant)
    ✓ User Assigned Managed Identity (no passwords/secrets in code)
    ✓ HTTPS enforced on App Service
    ✓ TLS 1.2 minimum on all services
    ✓ FTPS disabled on App Service
    ✓ Azure RBAC for OpenAI access (Cognitive Services OpenAI User role)
```

## Resource Dependencies

```
                    ┌────────────────────────┐
                    │    Resource Group      │
                    │  rg-expensemgmt-demo   │
                    └───────────┬────────────┘
                                │
            ┌───────────────────┼───────────────────┐
            │                   │                   │
            ▼                   ▼                   ▼
    ┌───────────────┐  ┌───────────────┐  ┌───────────────┐
    │   Managed     │  │  App Service  │  │   SQL Server  │
    │   Identity    │  │    Plan       │  │               │
    └───────┬───────┘  └───────┬───────┘  └───────┬───────┘
            │                   │                   │
            └─────────►┌───────┴───────┐◄──────────┘
                       │  App Service  │
                       │ (references   │
                       │  all above)   │
                       └───────────────┘
```
