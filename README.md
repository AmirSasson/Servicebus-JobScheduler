# Servicebus-JobScheduler
This repo provides an implemented concept of task scheduling using azure service bus delayed messages.
this project includes also a client simulator that initiated new Job Definitions Upserts that trigger the flows.
#### how it works?
using Publish Subscribe, the console application when starting, registers subscribers (handlers) to service bus topic subscriptions.  
Each handler, processes the message and publishes to next topic if needed.
To initiate the process, the Tester Client simulates JobDefinition Upserts.
see TopicsFlow.vsdx visio file for logical flow
##### Job Scheduler capabilties:
- enables scheduling by time interval [moreinfo](#scheduling)
- enables scheduling by [cron](https://crontab.cronhub.io/) expressions [moreinfo](#scheduling)
- immediate retries on errors
- [exponential backoff](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-retries-exponential-backoff#:~:text=Retries%20with%20exponential%20backoff%20is%20a%20technique%20that,more%20than%20a%20few%20seconds%20for%20any%20reason.) retries by config
- dead letter queueing for zero data loss + transparency
- enables [azure service bus partitioning](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-partitioning) for **high scale** by config
- enables ad-hoc job runs for manual/recovery scenarios [moreinfo](#scheduling)  
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
- You can choose 2 modes for the service bus: 
  - Local - used for development, when you less care about persistency and stabilty. see run option `--local`.
  - Cloud - production use, using azure ServiceBus, to use this option add `appsettings.overrides.json` under `src/Servicebus.JobScheduler.ExampleApp` <u>next to</u> existing`appsettings.json` with content:

    ```json
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

### <a id="scheduling"></a>Scheduling jobs ###
jobs can be scheduled in 3 different methods:
  1. **Time Interval sliding window** - occurs every X seconds from the actual schduling time.  
     in the below example, lets assume it was called on time `10:30:10`
     so the first job window will start immediately `(10:30:10)` and its time range would be `10:27:10 <-> 10:30:10` (range of 180 seconds)  
     second job would start on `10:32:10` and its time range would be `10:29:10 <-> 10:32:10` (after 120 from previouse window)

     ```csharp
    new JobDefinition
    {
      ....                    
        WindowTimeRangeSeconds = 180,
        Schedule = new JobSchedule { PeriodicJob = true, RunIntervalSeconds = 120 },                   
      ....
    }

     ```

  2. **Cron Job scheduling** .in the below example, lets assume it was called on time `10:30:10`, the cron represents [At every 2nd minute](https://crontab.guru/#*/2_*_*_*_*)
     so the first job window will start immediately `(10:30:10)` and its time range would be `10:28:10 <-> 10:30:00` (up to range of 120 seconds)  
     second job would start on `10:32:00` and its time range would be `10:30:00 <-> 10:32:00` (exatly 120 seconds and At every 2nd minute)
  ```csharp
    new JobDefinition
    {
      ....                    
        WindowTimeRangeSeconds = 120,
        Schedule = new JobSchedule { PeriodicJob = true, CronSchedulingExpression = "*/2 * * * *" },                  
      ....
    }

  ```   
     
  3. **adhoc runs of a predfeined time window**  
     in this example lets assume you would like to re-run jobs from `12/28/2020 1:00:00 PM` till `12/28/2020 4:00:00 PM`, we cancel the window validation phase in this case.

  ```
  new JobDefinition
  {
    ....                    
      WindowTimeRangeSeconds = 120,
      Schedule = new JobSchedule { PeriodicJob = true, ScheduleEndTime = DateTime.Parse("12/28/2020 4:00:00 PM"), ForceSuppressWindowValidation = true, RunIntervalSeconds = 120 },
      LastRunWindowUpperBound = DateTime.Parse("12/28/2020 1:00:00 PM")        
    ....
  ```

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
