namespace Servicebus.JobScheduler.Core
{
    internal enum SchedulingTopics
    {
        JobScheduled,
        JobInstanceReadyToRun,
        JobWindowValid,
        PermanentSchedulingErrors,
    }


    internal enum SchedulingSubscriptions
    {
        JobScheduled_CreateJobWindowInstance,
        JobInstanceReadyToRun_Validation,
        JobWindowValid_ScheduleNextRun,
        JobWindowValid_JobExecution,
    }
}
