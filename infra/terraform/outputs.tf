output "resource_group_name" {
  description = "Resource group holding the integration resources."
  value       = azurerm_resource_group.bunkerflow.name
}

output "servicebus_namespace" {
  description = "Fully qualified Service Bus namespace."
  value       = azurerm_servicebus_namespace.bunkerflow.name
}

output "topic_name" {
  description = "Topic the gateway publishes normalized events to."
  value       = azurerm_servicebus_topic.events.name
}

output "landing_subscription_name" {
  description = "Subscription the landing writer consumes."
  value       = azurerm_servicebus_subscription.landing.name
}

output "dead_letter_queue_name" {
  description = "Queue holding records rejected before publication."
  value       = azurerm_servicebus_queue.dead_letter.name
}

output "gateway_send_connection_string" {
  description = "Send-only connection string for the gateway."
  value       = azurerm_servicebus_topic_authorization_rule.gateway_send.primary_connection_string
  sensitive   = true
}
