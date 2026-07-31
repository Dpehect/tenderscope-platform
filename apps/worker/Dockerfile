FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore apps/worker/TenderScope.Worker.csproj
RUN dotnet publish apps/worker/TenderScope.Worker.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TenderScope.Worker.dll"]
