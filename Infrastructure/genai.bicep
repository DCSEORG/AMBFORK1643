// genai.bicep - Azure OpenAI and Cognitive Search resources

@description('Location for resources (Sweden Central for GPT-4o availability)')
param location string = 'swedencentral'

@description('Unique suffix for naming')
param uniqueSuffix string

@description('Principal ID of the managed identity for role assignment')
param managedIdentityPrincipalId string

// Ensure lowercase names for Azure OpenAI
var openAIName = toLower('aoai-expensemgmt-${uniqueSuffix}')
var searchName = toLower('search-expensemgmt-${uniqueSuffix}')

// Azure OpenAI Service
resource openAI 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: openAIName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

// GPT-4o Model Deployment
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-05-13'
    }
  }
}

// Cognitive Services OpenAI User Role Assignment
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, 'Cognitive Services OpenAI User')
  scope: openAI
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd') // Cognitive Services OpenAI User
    principalType: 'ServicePrincipal'
  }
}

// Azure Cognitive Search
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
  }
}

// Search Service Contributor Role Assignment
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, 'Search Service Contributor')
  scope: search
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0') // Search Service Contributor
    principalType: 'ServicePrincipal'
  }
}

// Search Index Data Contributor Role Assignment
resource searchIndexRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, 'Search Index Data Contributor')
  scope: search
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7') // Search Index Data Contributor
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIName string = openAI.name
output openAIModelName string = gpt4oDeployment.name
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output searchName string = search.name
