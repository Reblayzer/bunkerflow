locals {
  suffix = "${var.environment}-${var.location}"

  tags = merge(var.tags, {
    environment = var.environment
    managed_by  = "terraform"
  })
}

resource "azurerm_resource_group" "bunkerflow" {
  name     = "${var.resource_group_name}-${var.environment}"
  location = var.location
  tags     = local.tags
}

resource "azurerm_servicebus_namespace" "bunkerflow" {
  name                = "sbns-bunkerflow-${local.suffix}"
  resource_group_name = azurerm_resource_group.bunkerflow.name
  location            = azurerm_resource_group.bunkerflow.location
  sku                 = var.namespace_sku

  # SAS stays on because the gateway authenticates with the send-only
  # connection string below. Moving both hosts to a managed identity and
  # setting this to false is the next hardening step, not something to claim
  # as done.
  local_auth_enabled            = var.local_auth_enabled
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  tags = local.tags
}

resource "azurerm_servicebus_topic" "events" {
  name         = var.topic_name
  namespace_id = azurerm_servicebus_namespace.bunkerflow.id

  # The publisher sets a deterministic message id per source record, so the
  # broker can drop a replayed trade without the consumer being involved.
  requires_duplicate_detection            = true
  duplicate_detection_history_time_window = var.duplicate_detection_window

  default_message_ttl  = var.message_ttl
  support_ordering     = true
  partitioning_enabled = false
}

resource "azurerm_servicebus_subscription" "landing" {
  name     = var.landing_subscription_name
  topic_id = azurerm_servicebus_topic.events.id

  max_delivery_count = var.max_delivery_count

  # Anything the landing writer cannot process ends up on the subscription's
  # own dead-letter queue instead of being retried forever.
  dead_lettering_on_message_expiration      = true
  dead_lettering_on_filter_evaluation_error = true
  default_message_ttl                       = var.message_ttl
  lock_duration                             = "PT1M"
}

# Only the current schema version reaches the landing writer. A future version
# gets its own subscription rather than breaking this consumer.
resource "azurerm_servicebus_subscription_rule" "landing_schema_v1" {
  name            = "schema-v1"
  subscription_id = azurerm_servicebus_subscription.landing.id
  filter_type     = "SqlFilter"
  sql_filter      = "schemaVersion = 1"
}

resource "azurerm_servicebus_queue" "dead_letter" {
  name         = var.dead_letter_queue_name
  namespace_id = azurerm_servicebus_namespace.bunkerflow.id

  max_delivery_count                   = var.max_delivery_count
  dead_lettering_on_message_expiration = true
  default_message_ttl                  = var.message_ttl
}

# Least privilege: the gateway sends, the landing worker receives. Neither gets
# the namespace-wide manage rights, so neither can create or delete entities.
resource "azurerm_servicebus_topic_authorization_rule" "gateway_send" {
  name     = "gateway-send"
  topic_id = azurerm_servicebus_topic.events.id

  listen = false
  send   = true
  manage = false
}

# Service Bus has no subscription-level authorization rules, so receiving from a
# subscription is granted on the topic. The landing worker gets Listen and
# nothing else: it cannot publish, which keeps a consumer bug from turning into
# a poison-message loop of its own making.
resource "azurerm_servicebus_topic_authorization_rule" "landing_listen" {
  name     = "landing-listen"
  topic_id = azurerm_servicebus_topic.events.id

  listen = true
  send   = false
  manage = false
}

resource "azurerm_servicebus_queue_authorization_rule" "dead_letter_send" {
  name     = "gateway-deadletter-send"
  queue_id = azurerm_servicebus_queue.dead_letter.id

  listen = false
  send   = true
  manage = false
}
