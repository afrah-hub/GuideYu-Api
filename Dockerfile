# Use the SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["GuidYu-API/GuidYu-API.csproj", "GuidYu-API/"]
RUN dotnet restore "GuidYu-API/GuidYu-API.csproj"

# Copy everything else and build the project
COPY . .
WORKDIR "/src/GuidYu-API"
RUN dotnet build "GuidYu-API.csproj" -c Release -o /app/build

# Publish the project
FROM build AS publish
RUN dotnet publish "GuidYu-API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Generate final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port and configure entrypoint
EXPOSE 8080
ENTRYPOINT ["dotnet", "GuidYu-API.dll"]
