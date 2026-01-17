# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ConferenceHub.csproj", "./"]
RUN dotnet restore "ConferenceHub.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "ConferenceHub.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConferenceHub.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "ConferenceHub.dll"]
