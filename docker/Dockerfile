# Tutorial Validator - Executor Container
# Base image with .NET 10 SDK and all ABP prerequisites
# Reference: https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base

# Set environment variables
ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

# Install required packages
RUN apt-get update && apt-get install -y \
    curl \
    git \
    unzip \
    apt-transport-https \
    ca-certificates \
    gnupg \
    && rm -rf /var/lib/apt/lists/*

# Install Node.js LTS (v20+) - Required for ABP frontend
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

# Verify Node.js installation
RUN node --version && npm --version

# Workaround: The sdk:10.0 image may ship with an RC/preview runtime (e.g. 10.0.0-rc.2)
# while ABP CLI and EF tools target the stable 10.0.0 runtime. Install stable runtimes
# side-by-side so global tools can resolve the framework they were built against.
# This block can be removed once the sdk:10.0 tag ships with the stable 10.0.0 runtime.
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --runtime dotnet --version 10.0.0 --install-dir /usr/share/dotnet \
    && /tmp/dotnet-install.sh --runtime aspnetcore --version 10.0.0 --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh

# Add .NET tools to PATH
ENV PATH="${PATH}:/root/.dotnet/tools"

# Install ABP CLI globally
RUN dotnet tool install -g Volo.Abp.Studio.Cli

# Install EF Core CLI globally
RUN dotnet tool install -g dotnet-ef

# Verify tool installations
RUN abp --version && dotnet ef --version

# Pre-install ABP CLI extensions (e.g., StandardSolutionTemplates) by running
# a throwaway project creation. This caches the extensions in the Docker image
# layer, preventing the "CLI is restarting to install extensions" behavior
# during actual test execution which can confuse the AI agent.
RUN abp new WarmupProject -u mvc --no-random-port -o /tmp/abp-warmup || true
RUN rm -rf /tmp/abp-warmup

# Create directories
WORKDIR /app

# Copy solution and project files for restore (layer caching)
COPY src/Validator.Core/Validator.Core.csproj /src/Validator.Core/
COPY src/Validator.Executor/Validator.Executor.csproj /src/Validator.Executor/

# Restore dependencies
RUN dotnet restore /src/Validator.Executor/Validator.Executor.csproj

# Copy source code
COPY src/Validator.Core/ /src/Validator.Core/
COPY src/Validator.Executor/ /src/Validator.Executor/

# Build and publish
RUN dotnet publish /src/Validator.Executor/Validator.Executor.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Set working directory for execution
WORKDIR /workspace

# Default entry point
ENTRYPOINT ["dotnet", "/app/publish/Validator.Executor.dll"]

# Default command arguments
CMD ["run", "--input", "/output/testplan.json", "--workdir", "/workspace", "--output", "/output/results"]
