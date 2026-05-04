// ============================================================
// APIM API Config for Health Check project
// Deployed as a module into rg-apim-shared (APIM's resource group)
// Adds: backend, named value, API, operations, policies
// ============================================================

targetScope = 'resourceGroup'

@description('Shared APIM instance name')
param apimName string

@description('Function App name for backend URL')
param functionAppName string

@description('Entra ID tenant ID for JWT validation')
param tenantId string

// ─── Reference existing APIM ─────────────────────────────────
resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' existing = {
  name: apimName
}

// ─── Named Value: Function Key ───────────────────────────────
resource functionKey 'Microsoft.ApiManagement/service/namedValues@2023-09-01-preview' = {
  parent: apim
  name: 'healthcheck-function-key'
  properties: {
    displayName: 'healthcheck-function-key'
    value: 'REPLACE_WITH_FUNCTION_KEY'
    secret: true
  }
}

// ─── Backend ─────────────────────────────────────────────────
resource backend 'Microsoft.ApiManagement/service/backends@2023-09-01-preview' = {
  parent: apim
  name: 'healthcheck-functions'
  properties: {
    protocol: 'http'
    url: 'https://${functionAppName}.azurewebsites.net/api'
    credentials: {
      header: {
        'x-functions-key': ['{{healthcheck-function-key}}']
      }
    }
  }
  dependsOn: [functionKey]
}

// ─── API ─────────────────────────────────────────────────────
resource api 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'healthcheck-api'
  properties: {
    displayName: 'Health Check API'
    path: 'healthcheck'
    protocols: ['https']
    subscriptionRequired: false
  }
}

// ─── API-level Policy: CORS + Backend + Rate Limit ───────────
resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2023-09-01-preview' = {
  parent: api
  name: 'policy'
  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
    <base />
    <cors allow-credentials="true">
      <allowed-origins>
        <origin>*</origin>
      </allowed-origins>
      <allowed-methods>
        <method>GET</method>
        <method>POST</method>
        <method>PUT</method>
        <method>DELETE</method>
        <method>OPTIONS</method>
      </allowed-methods>
      <allowed-headers>
        <header>*</header>
      </allowed-headers>
    </cors>
    <set-backend-service backend-id="healthcheck-functions" />
    <rate-limit calls="60" renewal-period="60" />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
'''
  }
  dependsOn: [backend]
}

// ═══════════════════════════════════════════════════════════════
// PUBLIC OPERATIONS (no auth)
// ═══════════════════════════════════════════════════════════════

resource opGetStatus 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'get-status'
  properties: {
    displayName: 'Get Status'
    method: 'GET'
    urlTemplate: '/status'
  }
}

resource opGetStatusPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opGetStatus
  name: 'policy'
  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
    <base />
    <rewrite-uri template="/statusSiteStateReader" />
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
'''
  }
}

resource opGetHistory 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'get-status-history'
  properties: {
    displayName: 'Get Status History'
    method: 'GET'
    urlTemplate: '/status/history'
  }
}

resource opGetHistoryPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opGetHistory
  name: 'policy'
  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
    <base />
    <rewrite-uri template="/statusHistoryReader" />
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
'''
  }
}

// ═══════════════════════════════════════════════════════════════
// ADMIN OPERATIONS (JWT required — Entra ID, role: Admin)
// ═══════════════════════════════════════════════════════════════

// Shared JWT policy XML fragment
var jwtPolicyPrefix = '<validate-jwt header-name="Authorization" failed-validation-httpcode="401" require-scheme="Bearer"><openid-config url="https://login.microsoftonline.com/${tenantId}/v2.0/.well-known/openid-configuration" /><required-claims><claim name="roles" match="any"><value>Admin</value></claim></required-claims></validate-jwt>'

resource opAdminPoll 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'admin-poll'
  properties: {
    displayName: 'Trigger Poll (Admin)'
    method: 'POST'
    urlTemplate: '/admin/poll'
  }
}

resource opAdminPollPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opAdminPoll
  name: 'policy'
  properties: {
    format: 'xml'
    value: '<policies><inbound><base />${jwtPolicyPrefix}<rewrite-uri template="/httpPollerTrigger" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}

resource opAdminGetUrls 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'admin-get-urls'
  properties: {
    displayName: 'Get URLs (Admin)'
    method: 'GET'
    urlTemplate: '/admin/urls'
  }
}

resource opAdminGetUrlsPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opAdminGetUrls
  name: 'policy'
  properties: {
    format: 'xml'
    value: '<policies><inbound><base />${jwtPolicyPrefix}<rewrite-uri template="/statusUrlListReader" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}

resource opAdminCreateUrl 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'admin-create-url'
  properties: {
    displayName: 'Create URL (Admin)'
    method: 'POST'
    urlTemplate: '/admin/urls'
  }
}

resource opAdminCreateUrlPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opAdminCreateUrl
  name: 'policy'
  properties: {
    format: 'xml'
    value: '<policies><inbound><base />${jwtPolicyPrefix}<rewrite-uri template="/urlPersister" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}

resource opAdminUpdateUrl 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'admin-update-url'
  properties: {
    displayName: 'Update URL (Admin)'
    method: 'PUT'
    urlTemplate: '/admin/urls'
  }
}

resource opAdminUpdateUrlPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opAdminUpdateUrl
  name: 'policy'
  properties: {
    format: 'xml'
    value: '<policies><inbound><base />${jwtPolicyPrefix}<rewrite-uri template="/urlPersister" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}

resource opAdminDeleteUrl 'Microsoft.ApiManagement/service/apis/operations@2023-09-01-preview' = {
  parent: api
  name: 'admin-delete-url'
  properties: {
    displayName: 'Delete URL (Admin)'
    method: 'DELETE'
    urlTemplate: '/admin/urls'
  }
}

resource opAdminDeleteUrlPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-09-01-preview' = {
  parent: opAdminDeleteUrl
  name: 'policy'
  properties: {
    format: 'xml'
    value: '<policies><inbound><base />${jwtPolicyPrefix}<rewrite-uri template="/urlPersister" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}
