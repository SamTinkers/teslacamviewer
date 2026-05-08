# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

# Prevent 'Warning: apt-key output should not be parsed (stdout is not a terminal)'
ENV APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1

# .NET 5 SDK is on Debian buster, which Debian moved to archive in 2024.
# Repoint apt at archive.debian.org and disable Valid-Until checks (signatures
# remain verified). Without this, apt-get update returns 404 on every Release
# file and any apt install in this stage fails.
RUN sed -i 's|http://deb.debian.org/debian|http://archive.debian.org/debian|g; s|http://security.debian.org|http://archive.debian.org/debian-security|g' /etc/apt/sources.list && \
    sed -i '/buster-updates/d' /etc/apt/sources.list && \
    echo 'Acquire::Check-Valid-Until "false";' > /etc/apt/apt.conf.d/99-no-check-valid-until

# install NodeJS 14.x (16+ requires newer libstdc++ than buster ships;
# 14 is the highest the buster image can run, sufficient for Angular 8 build)
RUN apt-get update -yq
RUN apt-get install curl gnupg ca-certificates -yq
RUN curl -sL https://deb.nodesource.com/setup_14.x | bash -
RUN apt-get install -y nodejs build-essential

# Copy csproj and restore as distinct layers
COPY . .
RUN dotnet restore "/app/teslacamviewer.web/teslacamviewer.web.csproj"

# Copy everything else and build
COPY ./ ./
RUN dotnet publish "/app/teslacamviewer.web/teslacamviewer.web.csproj" -c Release -o out

# Build runtime image (also buster-based — same archive fix)
FROM mcr.microsoft.com/dotnet/aspnet:5.0
RUN sed -i 's|http://deb.debian.org/debian|http://archive.debian.org/debian|g; s|http://security.debian.org|http://archive.debian.org/debian-security|g' /etc/apt/sources.list && \
    sed -i '/buster-updates/d' /etc/apt/sources.list && \
    echo 'Acquire::Check-Valid-Until "false";' > /etc/apt/apt.conf.d/99-no-check-valid-until
RUN mkdir /teslacamdata
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "teslacamviewer.web.dll"]
