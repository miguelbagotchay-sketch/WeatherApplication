FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy and restore project file
COPY WeatherApplication.csproj .
RUN dotnet restore WeatherApplication.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish WeatherApplication.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render will provide the PORT environment variable
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "WeatherApplication.dll"]