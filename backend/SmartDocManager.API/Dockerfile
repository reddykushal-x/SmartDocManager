# Step 1: Use the official .NET 10 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copy the solution and project files to restore dependencies
COPY ["SmartDocManager.API/SmartDocManager.API.csproj", "SmartDocManager.API/"]
COPY ["SmartDocManager.Application/SmartDocManager.Application.csproj", "SmartDocManager.Application/"]
COPY ["SmartDocManager.Domain/SmartDocManager.Domain.csproj", "SmartDocManager.Domain/"]
COPY ["SmartDocManager.Infrastructure/SmartDocManager.Infrastructure.csproj", "SmartDocManager.Infrastructure/"]

RUN dotnet restore "SmartDocManager.API/SmartDocManager.API.csproj"

# Copy the remaining source code and build the production release
COPY . .
WORKDIR /app/SmartDocManager.API
RUN dotnet publish "SmartDocManager.API.csproj" -c Release -o /out

# Step 2: Build the final runtime image using the lightweight .NET 10 ASP.NET core image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /out .

# Create the Uploads directory inside the container for PDF storage
RUN mkdir -p /app/Uploads

# Expose port 5000 for network routing
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "SmartDocManager.API.dll"]