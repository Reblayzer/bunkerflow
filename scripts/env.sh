#!/usr/bin/env bash
# Local dev environment for BunkerFlow.
# The .NET SDK is installed user-locally under ~/.dotnet (no sudo required).
# DOTNET_SYSTEM_GLOBALIZATION_INVARIANT is set because this WSL distro has no
# libicu installed; install libicu-dev to drop it.
export PATH="$HOME/.dotnet:$HOME/.local/bin:$PATH"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
