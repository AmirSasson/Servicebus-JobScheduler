namespace Servicebus.JobScheduler.ExampleApp
{
    public enum Topics
    {
        JobDefinitionChange,
        ReadyToRunJobWindow,
        JobWindowValid,
        JobWindowConditionMet,
        JobWindowConditionNotMet, // no subscribers now..
        PermanentErrors,        
    }


    public enum Subscriptions
    {
        JobDefinitionChange_ImmediateScheduleRule,
        ReadyToRunJobWindow_Validation,
        JobWindowValid_ScheduleNextRun,
        JobWindowValid_RuleTimeWindowExecution,
        JobWindowConditionMet_Publish,
        JobWindowConditionMet_Suppress,
        JobWindowConditionNotMet_ScheduleIngestionDelay
    }
}
