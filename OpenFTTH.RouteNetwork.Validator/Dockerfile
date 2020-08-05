#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["OpenFTTH.RouteNetwork.Validator/OpenFTTH.RouteNetwork.Validator.csproj", "OpenFTTH.RouteNetwork.Validator/"]
RUN dotnet restore "OpenFTTH.RouteNetwork.Validator/OpenFTTH.RouteNetwork.Validator.csproj"
COPY . .
WORKDIR "/src/OpenFTTH.RouteNetwork.Validator"
RUN dotnet build "OpenFTTH.RouteNetwork.Validator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OpenFTTH.RouteNetwork.Validator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenFTTH.RouteNetwork.Validator.dll"]