FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ShoppingLab.slnx ./
COPY src/ShoppingLab.Core/ShoppingLab.Core.csproj src/ShoppingLab.Core/
COPY src/ShoppingLab.Web/ShoppingLab.Web.csproj src/ShoppingLab.Web/
RUN dotnet restore src/ShoppingLab.Web/ShoppingLab.Web.csproj
COPY src/ src/
RUN dotnet publish src/ShoppingLab.Web/ShoppingLab.Web.csproj -c Release -o /app --no-restore

# "extra" includes ICU, needed for Polish number formatting on the demo pages.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
ARG REVISION=dev
LABEL org.opencontainers.image.source="https://github.com/erloon/ai-shopping-readiness-lab" \
      org.opencontainers.image.revision="${REVISION}" \
      org.opencontainers.image.licenses="MIT"
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Lab__SelfUrl=http://127.0.0.1:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShoppingLab.Web.dll"]
