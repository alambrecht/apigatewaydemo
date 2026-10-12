FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY AuthService.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
# Folder for the signing key; owned by the non-root app user.
RUN mkdir /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AuthService.dll"]
