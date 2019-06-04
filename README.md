# Site Status Notification

The prupose of this is to provide a simple but contextual level of monitoring to any website or sections of public websites.
It uses http polling to ping the website and attempts to read the website or http service's content after getting a successfull http return code.
If an unsuccessful return code is read for 1 or more site, the application aggregates the number of failures and sends out an email alert.

## Infrastructure
The application and set of functions are built on [Microsoft Azure](https://docs.microsoft.com/en-us/azure/azure-functions/).
The alerter module of the application is provided by [SendGrid](https://sendgrid.com/), and email delivery service that integrates well with azure, as a matter of fact, it has it's own function bindings in azure functions whih allow for easy integration.

| Data Store    | Messaging     | Programming | Email Delivery Service |
| ------------- | ------------- |------------| -----------------------
| [Azure Storage Tables](https://azure.microsoft.com/en-us/services/storage/tables/)  | [Azure Queue Storage](https://azure.microsoft.com/en-us/services/storage/queues/)  | .NET | [SendGrid](https://sendgrid.com/) |


## Logical Architecture
![architecture](/azure_functions_architecture.jpg "architecture")



