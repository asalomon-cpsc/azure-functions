// ============================================================
// CPSC Health Check — Project Resources
// Deploy to: functions-cpsc1
// References shared APIM in rg-apim-shared (cross-RG)
// ============================================================

targetScope = 'resourceGroup'

// ─── Parameters ───────────────────────────────────────────────
@description('Base name prefix for project resources')
param appName string = 'cpsc'

@description('Existing Function App name')
param functionAppName string = 'healthchker'

@description('ACS sender address used by notification emails')
param emailSender string = 'DoNotReply@ef8338de-abde-4564-ac92-329ee056475b.azurecomm.net'

@description('Email subject for notification emails')
param emailSubject string = 'Azure Site Status Notification'

@description('Semicolon-separated notification recipients')
param emailRecipients string = 'asalomon@cpsc.gov'

@description('ACS endpoint used by the EmailClient managed identity flow')
param acsEndpoint string = 'https://vsemailaz.unitedstates.communication.azure.com'

@description('Optional dashboard URL included in notification emails')
param dashboardUrl string = ''

@description('Shared APIM resource group')
param apimResourceGroup string = 'rg-apim-shared'

@description('Shared APIM instance name')
param apimName string = 'apim-shared'

@description('Entra ID tenant ID')
param tenantId string = subscription().tenantId

@description('Static Web App location (limited regions for free tier)')
@allowed(['centralus', 'eastus2', 'westus2', 'westeurope', 'eastasia'])
param swaLocation string = 'eastus2'

// ─── Variables ────────────────────────────────────────────────
var acsName = 'acs-${appName}-email'
var swaName = 'swa-${appName}-healthcheck'

// ─── Azure Communication Services ────────────────────────────
resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: acsName
  location: 'global'
  properties: {
    dataLocation: 'United States'
    linkedDomains: [
      acsEmailDomain.id
    ]
  }
}

resource acsEmail 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: '${acsName}-email'
  location: 'global'
  properties: {
    dataLocation: 'United States'
  }
}

resource acsEmailDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: acsEmail
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

module functionSettings './functions/function-app-settings.bicep' = {
  name: 'function-app-settings'
  params: {
    functionAppName: functionAppName
    emailSender: emailSender
    emailSubject: emailSubject
    emailRecipients: emailRecipients
    acsEndpoint: acsEndpoint
    dashboardUrl: dashboardUrl
  }
}

// ─── Static Web App ──────────────────────────────────────────
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: swaName
  location: swaLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

// ─── Cross-RG Reference to Shared APIM ──────────────────────
resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' existing = {
  name: apimName
  scope: resourceGroup(apimResourceGroup)
}

// ─── APIM: Module for API config (must deploy to APIM's RG) ─
module apimConfig 'apim-healthcheck-api.bicep' = {
  name: 'apim-healthcheck-api'
  scope: resourceGroup(apimResourceGroup)
  params: {
    apimName: apimName
    functionAppName: functionAppName
    tenantId: tenantId
  }
}

// ─── Outputs ─────────────────────────────────────────────────
output acsEndpoint string = acs.properties.hostName
output acsEmailDomain string = acsEmailDomain.properties.mailFromSenderDomain
output staticWebAppUrl string = staticWebApp.properties.defaultHostname
output staticWebAppId string = staticWebApp.id
output apimGatewayUrl string = apim.properties.gatewayUrl

// Post-deployment manual steps:
// 1. Create Entra ID App Registrations (API + SPA) — cannot be done in Bicep
// 2. Assign users to "Admin" app role
// 3. Update APIM named value 'healthcheck-function-key' with actual Function App host key
// 4. Configure SPA redirect URIs with actual Static Web App URL
// 5. Update CORS allowed-origins in apim-healthcheck-api.bicep with actual SWA URL
