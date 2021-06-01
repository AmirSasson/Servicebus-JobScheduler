# Servicebus-JobScheduler
This repo provides an implemented concept of task scheduling using azure service bus delayed messages.
this project includes also a client simulator that initiated new Job Definitions Upserts that trigger the flows.
#### how it works?
using Publish Subscribe, the console application when starting, registers subscribers (handlers) to service bus topic subscriptions.  
Each handler, processes the message and publishes to next topic if needed.
To initiate the process, the Tester Client simulates JobDefinition Upserts.
see TopicsFlow.vsdx visio file for logical flow
##### Simulator capabilties:
- flexabilty to run as single process, or process that regsieters one or more handlers
- simuate errors (for the POC)
- local database for JobDefinition (for the POC)
- local database for JobOutputs  (for the POC)

## prerequisites:

- [dotnet 5](https://dotnet.microsoft.com/download) sdk
- optional: azure account
- optional: azure service bus namespace
- optional: [docker on windows](https://docs.docker.com/docker-for-windows/install/)

## setup:
```bash
    git clone https://github.com/AmirSasson/Servicebus-JobScheduler.git
    cd Servicebus-JobScheduler   
    code .
```
- for using azure ServiceBus, add `appsettings.overrides.json` under `src/Servicebus.JobScheduler.ExampleApp` <u>next to</u> existing`appsettings.json` with content:

```
{
  "ServiceBus": {
    "ConnectionString": "<<YOUR AZURE SERVICE BUS CONNECTION STRING>>"
  }
}
```
[Where to find the ConnectionString in the portal](https://social.msdn.microsoft.com/Forums/azure/en-US/c8edd9b5-76ea-4d93-8025-2e9d90b5ddf4/where-to-find-the-connectionstring-in-the-new-service-bus-portal)

- from within the root folder `dotnet build`
### run
- `cd ./src/Servicebus.JobScheduler.ExampleApp`
- from within the ExampleApp folder `dotnet run` - first run, by default, will configure Azure ServiceBus Entities for you.  
- (Non-Cloud) - you can also run a local In Memory version of ServiceBus `dotnet run -l true`
- you can also use `--modes <<mode[]>>` argument to run a specific/multiple modes of process i.e.  
   `dotnet run --run-setup true --all-modes false --tester-simulator false`    
   or  
    `dotnet run --modes JobDefinitionChange_ImmediateScheduleRule --tester-simulator false`  
   or  
   `dotnet run --modes JobWindowValid_ScheduleNextRun JobWindowValid_RuleTimeWindowExecution --tester-simulator false`  
   see `dotnet run -- --help` to view more running modes
### test
- from within the root folder `dotnet test` (tests tbd)


### deploy (cloud)
currently this app is running as a linux container on [AKS](https://docs.microsoft.com/en-us/azure/aks/).
#### dockerize:
from root folder run:  
[create](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal) an azure container registry, with admin credentials you can provide to the login command  
```
docker login <<registryname>>.azurecr.io
docker build -t <<registryname>>.azurecr.io/<<my-repository-name>>:latest .
docker push --all-tags <<registryname>>.azurecr.io/<<my-repository-name>>
```
to run the container locally:
`docker run <<registryname>>.azurecr.io/<<my-repository-name>>:latest`
- currently as docker container on AKS (linux), aks deployment yml included.

###### troubleshooting:
- make sure docker deamon is runnning linux containers
- restart docker
- https://github.com/Azure/azure-service-bus/issues/182
