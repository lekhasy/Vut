FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Vut.Silo/Vut.Silo.csproj", "Vut.Silo/"]
RUN dotnet restore "Vut.Silo/Vut.Silo.csproj"
COPY . .
WORKDIR /src/Vut.Silo
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000 11111 30000
ENTRYPOINT ["dotnet", "Vut.Silo.dll"]
