# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ShopHoa.Api/ShopHoa.Api.csproj", "ShopHoa.Api/"]
RUN dotnet restore "ShopHoa.Api/ShopHoa.Api.csproj"

COPY ShopHoa.Api/ ShopHoa.Api/
WORKDIR "/src/ShopHoa.Api"
RUN dotnet publish "ShopHoa.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ShopHoa.Api.dll"]
