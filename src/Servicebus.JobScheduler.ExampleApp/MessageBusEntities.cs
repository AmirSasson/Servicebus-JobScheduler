namespace Servicebus.JobScheduler.ExampleApp
{
    public enum Topics
    {
        JobWindowConditionMet,
        JobWindowConditionNotMet,
        PermanentExecutionErrors,
    }


    public enum Subscriptions
    {
        JobWindowConditionMet_Publish,
        JobWindowConditionMet_Suppress,
        JobWindowConditionNotMet_ScheduleIngestionDelay
    }
}
