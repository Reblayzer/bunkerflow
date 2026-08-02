variable "environment" {
  description = "Environment short name, used in every resource name."
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "test", "prod"], var.environment)
    error_message = "environment must be one of: dev, test, prod."
  }
}

variable "location" {
  description = "Azure region."
  type        = string
  default     = "westeurope"
}

variable "resource_group_name" {
  description = "Resource group that holds the integration resources."
  type        = string
  default     = "rg-bunkerflow"
}

variable "namespace_sku" {
  description = <<-EOT
    Service Bus SKU. Topics and subscriptions need Standard or higher; Premium
    is what a production workload would use for predictable throughput.
  EOT
  type        = string
  default     = "Standard"

  validation {
    condition     = contains(["Standard", "Premium"], var.namespace_sku)
    error_message = "namespace_sku must be Standard or Premium; Basic has no topics."
  }
}

variable "topic_name" {
  description = "Topic every normalized integration event is published to."
  type        = string
  default     = "bunkerflow-events"
}

variable "landing_subscription_name" {
  description = "Subscription the landing writer reads from."
  type        = string
  default     = "landing"
}

variable "dead_letter_queue_name" {
  description = "Queue for records rejected before they reach the topic."
  type        = string
  default     = "bunkerflow-deadletter"
}

variable "max_delivery_count" {
  description = "Deliveries attempted before Service Bus dead-letters a message."
  type        = number
  default     = 5

  validation {
    condition     = var.max_delivery_count >= 1 && var.max_delivery_count <= 100
    error_message = "max_delivery_count must be between 1 and 100."
  }
}

variable "message_ttl" {
  description = "ISO 8601 duration a message may sit unconsumed before it expires."
  type        = string
  default     = "P14D"
}

variable "duplicate_detection_window" {
  description = <<-EOT
    ISO 8601 window the broker remembers message ids in. BunkerFlow sends a
    deterministic message id per source record, so a replay inside this window
    is discarded by Service Bus before a consumer ever sees it.
  EOT
  type        = string
  default     = "PT10M"
}

variable "local_auth_enabled" {
  description = <<-EOT
    Whether SAS keys may be used. The hosts currently authenticate with a
    send-only connection string, so this defaults to true. Set it to false once
    both hosts use a managed identity.
  EOT
  type        = bool
  default     = true
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
  default = {
    project = "bunkerflow"
    owner   = "integration"
  }
}
