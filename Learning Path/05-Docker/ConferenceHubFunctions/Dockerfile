FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConferenceHub.Functions.csproj", "./"]
RUN dotnet restore "ConferenceHub.Functions.csproj"
COPY . .
RUN dotnet build "ConferenceHub.Functions.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConferenceHub.Functions.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
