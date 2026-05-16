FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Velucid.ReadModel/Velucid.ReadModel.csproj", "Velucid.ReadModel/"]
COPY ["Velucid.ReadModel.Migrations/Velucid.ReadModel.Migrations.csproj", "Velucid.ReadModel.Migrations/"]
RUN dotnet restore "Velucid.ReadModel.Migrations/Velucid.ReadModel.Migrations.csproj"
COPY . .
WORKDIR /src/Velucid.ReadModel.Migrations
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Velucid.ReadModel.Migrations.dll"]
