FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
# Copy only the main project csproj first for layer-cached restore (no sln, no tests)
COPY src/ImmichLoungeCompanion/ImmichLoungeCompanion.csproj src/ImmichLoungeCompanion/
RUN dotnet restore src/ImmichLoungeCompanion/ImmichLoungeCompanion.csproj
# Now copy the rest of the source (changes here don't invalidate the restore layer)
COPY src/ src/
RUN dotnet publish src/ImmichLoungeCompanion/ImmichLoungeCompanion.csproj \
    -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:4383
ENV DataDirectory=/data
VOLUME /data
EXPOSE 4383
ENTRYPOINT ["dotnet", "ImmichLoungeCompanion.dll"]
