FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /App

# Copy everything
COPY ./Kasbot.APP ./Kasbot.APP
COPY ./Kasbot.API ./Kasbot.API
COPY ./Kasbot.sln ./Kasbot.sln

# Restore as distinct layers
RUN dotnet restore

# Build a release
RUN dotnet build -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0

RUN apt update && apt install -y ffmpeg libopus-dev opus-tools libsodium-dev
WORKDIR /App

COPY Docker/start.sh .
RUN chmod +x start.sh

COPY --from=build-env /App/out .

ENTRYPOINT ["./start.sh" ]