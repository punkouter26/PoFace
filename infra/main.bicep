param location string = resourceGroup().location
param environmentName string = 'dev'
param sharedKeyVaultName string = 'kv-poshared'
param sharedKeyVaultResourceGroupName string = 'PoShared'
param logAnalyticsWorkspaceId string = '/subscriptions/bbb8dfbe-9169-432f-9b7a-fbf861b51037/resourceGroups/PoShared/providers/Microsoft.OperationalInsights/workspaces/PoShared-LogAnalytics'
param appServiceSkuName string = 'B1'

var normalizedEnvironment = toLower(replace(environmentName, '-', ''))
var webAppName = 'poface-${normalizedEnvironment}-web'
var planName = 'poface-${normalizedEnvironment}-plan'
var storageAccountName = take('poface${normalizedEnvironment}sa', 24)
var appInsightsName = 'PoFace-${environmentName}-appi'
resource sharedKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  scope: resourceGroup(sharedKeyVaultResourceGroupName)
  name: sharedKeyVaultName
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
    name: appServiceSkuName
    tier: 'Basic'
    size: appServiceSkuName
    family: 'B'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: true
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AzureStorage__AccountName'
          value: storageAccount.name
        }
        {
          name: 'KeyVault__Uri'
          value: sharedKeyVault.properties.vaultUri
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

module keyVaultAccess './keyvault-access.bicep' = {
  name: 'keyVaultAccess'
  scope: resourceGroup(sharedKeyVaultResourceGroupName)
  params: {
    keyVaultName: sharedKeyVaultName
    principalId: webApp.identity.principalId
  }
}

resource blobLifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  name: 'default'
  parent: storageAccount
  properties: loadJsonContent('storage-lifecycle.json').resources[0].properties
}

output appServiceName string = webApp.name
output appServiceUrl string = 'https://${webApp.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output sharedKeyVaultUri string = sharedKeyVault.properties.vaultUri