# The emulator reads its topology from a config file. Baking it into the image
# instead of bind-mounting it keeps the compose stack free of host paths, which
# matters on Windows and WSL where mounts are the fragile part.
FROM mcr.microsoft.com/azure-messaging/servicebus-emulator:latest

COPY config.json /ServiceBus_Emulator/ConfigFiles/Config.json
