# Stage 1: Build (Updated to SDK 9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the .sln file located in src/
COPY ["src/RoK.Ocr.sln", "src/"]

# Copy .csproj files preserving the folder structure
COPY ["src/RoK.Ocr.Api/RoK.Ocr.Api.csproj", "src/RoK.Ocr.Api/"]
COPY ["src/RoK.Ocr.Application/RoK.Ocr.Application.csproj", "src/RoK.Ocr.Application/"]
COPY ["src/RoK.Ocr.Domain/RoK.Ocr.Domain.csproj", "src/RoK.Ocr.Domain/"]
COPY ["src/RoK.Ocr.Infrastructure/RoK.Ocr.Infrastructure.csproj", "src/RoK.Ocr.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/RoK.Ocr.sln"

# Copy all remaining source code from the src folder
COPY src/ src/

# Compile and Publish
WORKDIR "/src/src/RoK.Ocr.Api"
RUN dotnet publish "RoK.Ocr.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime (Updated to ASPNET 9.0)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create the uploads folder
RUN mkdir -p /app/wwwroot/uploads

# Copy the executable generated in the previous stage
COPY --from=build /app/publish .

# Expose port 8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RoK.Ocr.Api.dll"]