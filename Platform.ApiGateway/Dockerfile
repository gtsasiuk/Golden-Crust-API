FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Platform.ApiGateway/Platform.ApiGateway.csproj", "Platform.ApiGateway/"]
COPY ["Platform.ServiceDefaults/Platform.ServiceDefaults.csproj", "Platform.ServiceDefaults/"]
COPY ["Platform.EventBus/Platform.EventBus.csproj", "Platform.EventBus/"]
RUN dotnet restore "Platform.ApiGateway/Platform.ApiGateway.csproj"
COPY . .
WORKDIR "/src/Platform.ApiGateway"
RUN dotnet build "Platform.ApiGateway.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "Platform.ApiGateway.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.ApiGateway.dll"]