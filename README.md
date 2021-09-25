# Servicebus-JobScheduler
This repo provides an implemented framework of task scheduling using azure service bus delayed messages.
this project includes also a client simulator that initiated new Job Definitions Upserts that trigger the flows.

available on [nuget](https://www.nuget.org/packages/Servicebus.JobScheduler.Core/).

[![Build Status](https://app.travis-ci.com/AmirSasson/Servicebus-JobScheduler.svg?branch=main)](https://app.travis-ci.com/AmirSasson/Servicebus-JobScheduler)
  
[![NuGet Badge](https://buildstats.info/nuget/Servicebus.JobScheduler.Core)](https://www.nuget.org/packages/Servicebus.JobScheduler.Core/)

#### how the scheduling framework works?
When starting the application, the framework registers subscribers (handlers) to service bus topic subscriptions to handle the scheduling.  
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
  - Local - used for development, when you less care about persistency and stabilty. see run option `--local true`.
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
   `dotnet run --all-modes false --tester-simulator false`    
   or run the scheduler workers
    `dotnet run --modes scheduling --tester-simulator false`  
   or run the scheduler the executing tasks
   `dotnet run --modes executing JobWindowConditionMet_Publish JobWindowConditionMet_Suppress JobWindowConditionNotMet_ScheduleIngestionDelay --tester-simulator false`  
   see `dotnet run -- --help` to view more running modes
- **most common dev run setup** would be to run the scheduling and simulator in separate process than the bussiness logic  
  to do so, run these commands each on different window:
    1. simulator only: `dotnet run --all-modes false --tester-simulator true`
    2. scheduling only: `dotnet run --modes scheduling --tester-simulator false`
    3. job executing business logic: `dotnet run --modes executing JobWindowConditionMet_Publish JobWindowConditionMet_Suppress JobWindowConditionNotMet_ScheduleIngestionDelay  --tester-simulator false`

### test
- from within the root folder `dotnet test` (tests tbd)

## Use as consumer
typical job execution workflow
```csharp
            var builder = new JobSchedulerBuilder()
                .UseLoggerFactory(loggerFactory)                
                .WithCancelationSource(source)
                .WithConfiguration(config)
                .AddRootJobExecuter<JobCustomData>(
                    new WindowExecutionSubscriber(loggerFactory.CreateLogger<WindowExecutionSubscriber>(), options.ExecErrorRate, TimeSpan.FromSeconds(1.5)),
                    concurrencyLevel: 3,
                    new RetryPolicy { PermanentErrorsTopic = Topics.PermanentErrors.ToString(), RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) })                
                .WithJobChangeProvider(db);
            _scheduler = await builder.Build();
```

see example [Program.cs](https://github.com/AmirSasson/Servicebus-JobScheduler/blob/6b84f19151dbeff12e38eb22cd46c3f3edf13ba8/src/Servicebus.JobScheduler.ExampleApp/Program.cs#L117) for more complex options such as Dependency injection that provides scoping per job, and other options

### <a id="scheduling"></a>Scheduling jobs ###
jobs can be scheduled in 3 different methods:
  1. **Time Interval sliding window** - occurs every X seconds from the actual schduling time.  
     in the below example, lets assume it was called on time `10:30:10`
     so the first job window will start immediately `(10:30:10)` and its time range would be `10:28:10 <-> 10:30:10`  
     second job would start on `10:32:10` and its time range would be `10:30:10 <-> 10:32:10` (after 120 from previouse window)
```csharp
    var job = new Job<SomeCustomPayload>
    {
      ....                            
        Schedule = new JobSchedule { PeriodicJob = true, RunIntervalSeconds = 120 },                   
      ....
    }
    _scheduler.ScheduleJob(job);
```

  1. **Cron Job scheduling** .in the below example, lets assume it was called on time `10:30:10`, the cron represents [At every 2nd minute](https://crontab.guru/#*/2_*_*_*_*)
     so the first job window will start immediately `(10:30:10)` and its time range would be `10:28:00 <-> 10:30:00` (up to range of 120 seconds)  
     second job would start on `10:32:00` and its time range would be `10:30:00 <-> 10:32:00` (exatly 120 seconds and At every 2nd minute)
```csharp
    var job = new Job<SomeCustomPayload>
    {
      ....
        Schedule = new JobSchedule { PeriodicJob = true, CronSchedulingExpression = "*/2 * * * *" },                  
      ....      
    }
    _scheduler.ScheduleJob(job);

```   
  OR you can schedule a cron job that will look on a specific lookback time window, in this example it would run every 2 minutes, and look back on a 1 hour (3600sec) time range, (overlapped time ranges)
  ```csharp
     var job = new Job<SomeCustomPayload>
    {
      ....
        Schedule = new JobSchedule { PeriodicJob = true, CronSchedulingExpression = "*/2 * * * *" ,RunIntervalSeconds = 3600},                  
      ....      
    }
     _scheduler.ScheduleJob(job);
  ```   
     
  3. **adhoc runs of a predfeined time window**  
     in this example lets assume you would like to re-run jobs from `12/28/2020 1:00:00 PM` till `12/28/2020 4:00:00 PM`, we cancel the window validation phase in this case.

```csharp
  new JobDefinition
  {
    ....                          
      Schedule = new JobSchedule { PeriodicJob = true, ScheduleEndTime = DateTime.Parse("12/28/2020 4:00:00 PM"), ForceSuppressWindowValidation = true, RunIntervalSeconds = 120 },
      LastRunWindowUpperBound = DateTime.Parse("12/28/2020 1:00:00 PM")        
    ....
  }
   _scheduler.ScheduleJob(job);
```
### Job orchestrating
In some cases Jobs can be complex, and own a few crusial steps till they are considered completed. mostly when each step contains "unstable" IO operations.  
in those cases the app may orchestrate a Job flow using **Sub Jobs**.  
Sub jobs can continue the flow of the root job executer. in that case Root Job Executer, should return **HandlerResponse.ContinueWithResult** that will tell the framework that the job is not done yet, and another sub job(or jobs) will continue the flow. each SubJob will benfit same retrying, logging and independcy the framework provides, while the root job be considered as completed, and be ready to handle more new jobs.
to use Sub Jobs see example app [Program.cs](https://github.com/AmirSasson/Servicebus-JobScheduler/blob/6b84f19151dbeff12e38eb22cd46c3f3edf13ba8/src/Servicebus.JobScheduler.ExampleApp/Program.cs#L117). that calls `AddSubJobHandlerType`.

### Error Handling
* Each Job handler (or subJobHandler) can provide its own retry poilcy.
  * retry policy can determine the **Permanent Topic** in which final error would be transfered to this topic, for later processing.
  * by default each handling will be immediately retried 5 times till scheduling for later tries (based on the provided retry policy)
  * handler can also fire a `ExecutionPermanentException` to stop retrying and fire to **Permanent Topic**
* the framework also provides a **Permanent Topic** for job that had scheduling problems (prior to execution).

## deploy (cloud)
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
