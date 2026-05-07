FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG GITHUB_USER
ARG GITHUB_TOKEN

COPY ["nuget.config", "./"]

# Tool manifest — must be in .config/
COPY [".config/dotnet-tools.json", ".config/dotnet-tools.json"]
RUN dotnet tool restore

COPY ["src/Kariyer.Identity/Kariyer.Identity.csproj", "src/Kariyer.Identity/"]
RUN dotnet restore "src/Kariyer.Identity/Kariyer.Identity.csproj"

COPY . .
RUN dotnet publish "src/Kariyer.Identity/Kariyer.Identity.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5001
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Kariyer.Identity.dll"]