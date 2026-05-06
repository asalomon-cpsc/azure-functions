targetScope = 'resourceGroup'

@description('Existing Function App name to configure')
param functionAppName string

@description('ACS sender address used by notification emails')
param emailSender string

@description('Email subject for notification emails')
param emailSubject string = 'Azure Site Status Notification'

@description('Semicolon-separated notification recipients')
param emailRecipients string

@description('ACS endpoint used by the EmailClient managed identity flow')
param acsEndpoint string

@description('Optional dashboard URL included in notification emails')
param dashboardUrl string = ''

resource functionApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: functionAppName
}

#disable-next-line use-resource-symbol-reference
var existingAppSettings = reference(resourceId('Microsoft.Web/sites/config', functionAppName, 'appsettings'), '2022-09-01').properties

var managedIdentityEmailSettings = {
  EMAIL_SENDER: emailSender
  EMAIL_SUBJECT: emailSubject
  EMAIL_RECIPIENTS: emailRecipients
  ACS_ENDPOINT: acsEndpoint
}

var optionalDashboardSetting = empty(dashboardUrl) ? {} : {
  dashboard_url: dashboardUrl
}

resource functionAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: union(existingAppSettings, managedIdentityEmailSettings, optionalDashboardSetting)
}