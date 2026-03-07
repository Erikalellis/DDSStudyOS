FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DDSStudyOS.Portal.csproj", "./"]
RUN dotnet restore "DDSStudyOS.Portal.csproj"

COPY . .
RUN dotnet publish "DDSStudyOS.Portal.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "DDSStudyOS.Portal.dll"]
