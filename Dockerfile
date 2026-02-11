FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/CLI/CLI.csproj", "src/CLI/"]
RUN dotnet restore "src/CLI/CLI.csproj"

COPY . .
WORKDIR "/src/src/CLI"
RUN dotnet build "CLI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CLI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CLI.dll"]
