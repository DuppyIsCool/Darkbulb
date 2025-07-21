# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy only the project files first
COPY Darkbulb.sln ./
COPY Darkbulb.csproj ./
RUN dotnet restore

# Copy the rest of your code
COPY . .
RUN dotnet publish -c Release -o /app/publish \
    --no-self-contained -r linux-x64

# Runtime
FROM mcr.microsoft.com/dotnet/runtime:7.0
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Darkbulb.dll"]
