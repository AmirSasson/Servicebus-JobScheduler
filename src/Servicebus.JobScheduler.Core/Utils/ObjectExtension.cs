using System.Text.Json;

namespace Servicebus.JobScheduler.Core.Utils
{
    public static class ObjectExtension
    {
        public static T Clone<T>(this T o)
        {
            return ToJson(o).FromJson<T>();
        }

        public static T FromJson<T>(this string serialized)
        {
            return JsonSerializer.Deserialize<T>(serialized);
        }

        public static string ToJson<T>(this T o)
        {
            return JsonSerializer.Serialize(o);
        }
    }
}
