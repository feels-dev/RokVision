# Stage 1: Build (Using SDK 9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copies solution and project files to restore dependencies (cache optimization)
COPY ["src/RoK.Ocr.sln", "src/"]
COPY ["src/RoK.Ocr.Api/RoK.Ocr.Api.csproj", "src/RoK.Ocr.Api/"]
COPY ["src/RoK.Ocr.Application/RoK.Ocr.Application.csproj", "src/RoK.Ocr.Application/"]
COPY ["src/RoK.Ocr.Domain/RoK.Ocr.Domain.csproj", "src/RoK.Ocr.Domain/"]
COPY ["src/RoK.Ocr.Infrastructure/RoK.Ocr.Infrastructure.csproj", "src/RoK.Ocr.Infrastructure/"]

# Restores NuGet dependencies
RUN dotnet restore "src/RoK.Ocr.sln"

# Copies all source code
COPY src/ src/

# Builds and publishes binaries in Release mode
WORKDIR "/src/src/RoK.Ocr.Api"
RUN dotnet publish "RoK.Ocr.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime (Using ASP.NET 9.0)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Creates necessary folders for uploads and vocabulary
RUN mkdir -p /app/wwwroot/uploads /app/Assets

# Copies compiled binaries from the previous stage
COPY --from=build /app/publish .

# --- MASTER TOUCH ---
# Copies the Assets folder (Commander JSONs) to the execution directory
# The VocabularyLoader expects to find files in ./Assets/
COPY src/RoK.Ocr.Infrastructure/Assets/ ./Assets/

# Exposes default ASP.NET port
EXPOSE 8080

# Defines application entry point
ENTRYPOINT ["dotnet", "RoK.Ocr.Api.dll"]