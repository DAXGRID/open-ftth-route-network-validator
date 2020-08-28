FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

COPY ./*sln ./

COPY ./OpenFTTH.RouteNetwork.Validator/*.csproj ./OpenFTTH.RouteNetwork.Validator/

RUN dotnet restore --packages ./packages

COPY . ./
WORKDIR /app/OpenFTTH.RouteNetwork.Validator
RUN dotnet publish -c Release -o out --packages ./packages

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app

COPY --from=build-env /app/OpenFTTH.RouteNetwork.Validator/out .
ENTRYPOINT ["dotnet", "OpenFTTH.RouteNetwork.Validator.dll"]
