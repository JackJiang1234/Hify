# Hify 后端（Hify.Host）镜像。多阶段构建：SDK 还原+发布 → 运行时镜像。
# 密钥/密码不入镜像，由环境变量注入（见 docker-compose.yml）。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Hify.Host/Hify.Host.csproj
RUN dotnet publish src/Hify.Host/Hify.Host.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hify.Host.dll"]
