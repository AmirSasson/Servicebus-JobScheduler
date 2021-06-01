#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app
RUN ls -la

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
RUN ls -la 
COPY . .
RUN ls -la
RUN dotnet restore "src/Servicebus.JobScheduler.ExampleApp/Servicebus.JobScheduler.ExampleApp.csproj"
RUN ls -la
#COPY . .
#RUN ls -la
#COPY . .
#WORKDIR "/src/Servicebus.JobScheduler.ExampleApp"
RUN dotnet build "src/Servicebus.JobScheduler.ExampleApp/Servicebus.JobScheduler.ExampleApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Servicebus.JobScheduler.ExampleApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Servicebus.JobScheduler.ExampleApp.dll"]