FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["BlazorApp/BlazorApp.csproj", "BlazorApp/"]
RUN dotnet restore "BlazorApp/BlazorApp.csproj"
COPY . .
WORKDIR "/src/BlazorApp"
RUN dotnet build "BlazorApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BlazorApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install DejaVu fonts for SixLabors.Fonts compatibility
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core && rm -rf /var/lib/apt/lists/*

RUN chmod u+x docker-entrypoint.sh

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_HTTP_PORTS=80
ENV ASPNETCORE_URLS=http://*:80

ENTRYPOINT ["dotnet", "BlazorApp.dll"]