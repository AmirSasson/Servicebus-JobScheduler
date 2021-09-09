namespace Servicebus.JobScheduler.Core
{
    internal enum SchedulingTopics
    {
        JobDefinitionChange,
        ReadyToRunJobWindow,
        JobWindowValid,
        PermanentSchedulingErrors,
    }


    internal enum SchedulingSubscriptions
    {
        JobDefinitionChange_ImmediateScheduleRule,
        ReadyToRunJobWindow_Validation,
        JobWindowValid_ScheduleNextRun,
        JobWindowValid_RuleTimeWindowExecution,
    }
}
