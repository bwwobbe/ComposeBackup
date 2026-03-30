FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish src/ComposeBackup -c release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update && apt-get install -y docker.io docker-compose-v2 restic
WORKDIR /App
COPY --from=build /App/out .
ENTRYPOINT ["dotnet", "ComposeBackup.dll"]
