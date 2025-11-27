// app-service.bicep - Azure App Service with S1 SKU

@description('Location for resources')
param location string

@description('Name of the App Service')
param appName string

@description('Managed Identity Resource ID')
param managedIdentityId string

@description('Managed Identity Client ID')
param managedIdentityClientId string

@description('SQL Server name for connection string')
param sqlServerName string

@description('SQL Database name for connection string')
param sqlDatabaseName string

var appServicePlanName = 'asp-expensemgmt-${uniqueString(resourceGroup().id)}'

// App Service Plan with S1 SKU to avoid cold start
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: false // Windows
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
        {
          name: 'ManagedIdentityClientId'
          value: managedIdentityClientId
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServerName}.database.windows.net,1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${managedIdentityClientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
          type: 'SQLAzure'
        }
      ]
    }
  }
}

output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServicePrincipalId string = appService.identity.userAssignedIdentities[managedIdentityId].principalId
