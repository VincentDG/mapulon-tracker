# Stage 1: Build the C# application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["Tracker.api.csproj", "./"]
RUN dotnet restore "Tracker.api.csproj"

# Copy all other source files and compile
COPY . .
RUN dotnet publish "Tracker.api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final runtime container
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Configure ASP.NET Core to run on port 8080 (standard for Render)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tracker.api.dll"]