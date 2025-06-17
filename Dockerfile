# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy everything into the container
COPY . .

# Go into the folder where the .csproj file is located
WORKDIR "/src/Botatwork in Livechat"

# Publish the application
RUN dotnet publish "Botatwork in Livechat.csproj" -c Release -o /app/publish

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Botatwork in Livechat.dll"]
