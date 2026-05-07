FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG GITHUB_USER
ARG GITHUB_TOKEN

COPY ["src/Kariyer.Identity/*.csproj", "./"]
COPY ["nuget.config", "./"]
RUN dotnet restore

COPY dotnet-tools.json ./dotnet-tools.json
RUN dotnet tool restore --tool-manifest dotnet-tools.json

COPY . .

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

USER app
EXPOSE 5001

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Kariyer.Identity.dll"]