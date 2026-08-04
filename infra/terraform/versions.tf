terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

provider "azurerm" {
  features {}

  # Required from azurerm v4 onwards. Left null so it can come from
  # ARM_SUBSCRIPTION_ID, which is how CI would supply it.
  subscription_id = var.subscription_id
}
