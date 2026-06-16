# Build Bento
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Bento.csproj -c Release -o /app

# Runtime and Tooling
FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && apt-get install -y --no-install-recommends git git-lfs p7zip-full jq curl ca-certificates sshpass openssh-client rclone && apt-get clean && rm -rf /var/lib/apt/lists/*
COPY --from=build /app /opt/bento

# Run. The DOTNET_RUNNING_IN_CONTAINER env variable (set by the base image) switches bento to fully-flagged mode.
ENTRYPOINT ["/opt/bento/bento"]
