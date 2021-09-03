FROM mcr.microsoft.com/dotnet/sdk:6.0.100-preview.7-bullseye-slim-amd64 AS build-env
WORKDIR /app

COPY ./*sln ./

COPY ./OpenFTTH.RouteNetwork.Validator/*.csproj ./OpenFTTH.RouteNetwork.Validator/

RUN dotnet restore --packages ./packages

COPY . ./
WORKDIR /app/OpenFTTH.RouteNetwork.Validator
RUN dotnet publish -c Release -o out --packages ./packages

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0.0-preview.7-bullseye-slim-amd64
WORKDIR /app

COPY --from=build-env /app/OpenFTTH.RouteNetwork.Validator/out .
ENTRYPOINT ["dotnet", "OpenFTTH.RouteNetwork.Validator.dll"]
