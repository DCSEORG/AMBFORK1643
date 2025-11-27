// managed-identity.bicep - User Assigned Managed Identity

@description('Location for resources')
param location string

@description('Unique suffix for naming')
param uniqueSuffix string

var managedIdentityName = 'mid-appmodassist-${uniqueSuffix}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityName string = managedIdentity.name
