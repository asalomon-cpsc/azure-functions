// ============================================================
// Shared API Management Instance
// Deploy to: rg-apim-shared
// Reusable across projects — each project adds its own APIs
// ============================================================

targetScope = 'resourceGroup'

@description('APIM instance name')
param apimName string = 'apim-shared'

@description('Azure region')
param location string = resourceGroup().location

@description('Publisher email for APIM (required)')
param publisherEmail string

@description('Publisher name for APIM')
param publisherName string = 'Platform Team'

// ─── API Management (Consumption) ────────────────────────────
resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' = {
  name: apimName
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

// ─── Outputs ─────────────────────────────────────────────────
output apimName string = apim.name
output apimId string = apim.id
output apimGatewayUrl string = apim.properties.gatewayUrl
