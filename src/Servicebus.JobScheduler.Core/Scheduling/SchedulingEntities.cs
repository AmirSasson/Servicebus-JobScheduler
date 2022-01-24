namespace Servicebus.JobScheduler.Core
{
    internal static class EntitiesPathHelper
    {
        public static string JobTypeName<TJobPayload>()
        {
            return typeof(TJobPayload).Name.Replace("<", "__").Replace(">", "__");
        }

        public static string GetDynamicTopicName<TJobPayload>(SchedulingDynamicTopicsPrefix topic)
        {
            return topic.ToString() + JobTypeName<TJobPayload>();
        }

        public static string GetDynamicSubscriptionName<TJobPayload>(SchedulingynamicSubscriptionsPrefix sub)
        {
            return sub.ToString().Replace("_SUBNAME_", JobTypeName<TJobPayload>());
        }

    }
    internal enum SchedulingDynamicTopicsPrefix
    {
        JobWindowValid,
    }

    internal enum SchedulingynamicSubscriptionsPrefix
    {
        JobWindowValid_SUBNAME__ScheduleNextRun,
        JobWindowValid_SUBNAME__JobExecution,

    }
    internal enum SchedulingTopics
    {
        JobScheduled,
        JobInstanceReadyToRun,
        PermanentSchedulingErrors,
    }


    internal enum SchedulingSubscriptions
    {
        JobScheduled_CreateJobWindowInstance,
        JobInstanceReadyToRun_Validation,
    }
}
