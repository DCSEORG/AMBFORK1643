// azure-sql.bicep - Azure SQL Database with Entra ID Only Authentication

@description('Location for resources')
param location string

@description('SQL Server name')
param sqlServerName string

@description('SQL Database name')
param sqlDatabaseName string

@description('Object ID of the Entra ID administrator')
param adminObjectId string

@description('Login name (email) of the administrator')
param adminLogin string

@description('Principal ID of the managed identity for role assignment')
param managedIdentityPrincipalId string

// Azure SQL Server with Azure AD Only Authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      principalType: 'User'
      sid: adminObjectId
      tenantId: subscription().tenantId
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// SQL Database - Basic tier for development
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

// Firewall rule to allow Azure services
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
