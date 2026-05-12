FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Velucid.ProjectorService/Velucid.ProjectorService.csproj", "Velucid.ProjectorService/"]
RUN dotnet restore "Velucid.ProjectorService/Velucid.ProjectorService.csproj"
COPY . .
WORKDIR /src/Velucid.ProjectorService
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Velucid.ProjectorService.dll"]
