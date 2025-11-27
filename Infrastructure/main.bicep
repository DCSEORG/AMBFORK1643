// main.bicep - Main deployment template for Expense Management System
// Deploys App Service, SQL Database, and optionally GenAI resources

@description('Location for all resources')
param location string = 'uksouth'

@description('Deploy GenAI resources (Azure OpenAI and Cognitive Search)')
param deployGenAI bool = false

@description('Object ID of the user deploying (for SQL admin)')
param adminObjectId string

@description('Login name (email) of the SQL admin')
param adminLogin string

// Generate unique suffix using resource group ID
var uniqueSuffix = uniqueString(resourceGroup().id)
var appName = 'app-expensemgmt-${uniqueSuffix}'
var sqlServerName = 'sql-expensemgmt-${uniqueSuffix}'
var sqlDatabaseName = 'Northwind'

// Deploy User Assigned Managed Identity
module managedIdentity 'managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
  }
}

// Deploy App Service with Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    appName: appName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
  }
}

// Deploy Azure SQL Database
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Conditionally deploy GenAI resources
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: 'swedencentral' // Required for GPT-4o availability
    uniqueSuffix: uniqueSuffix
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output sqlDatabaseName string = sqlDatabaseName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityName string = managedIdentity.outputs.managedIdentityName

// Conditional GenAI outputs
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
