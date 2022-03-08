using System;
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
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(serialized);
        }

        public static T FromJson<T>(this string serialized, Type asType)
        {
            return (T)Newtonsoft.Json.JsonConvert.DeserializeObject(serialized, asType);
        }

        public static string ToJson<T>(this T o)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(o);
        }

        public static string ToJson<T>(this T o, Type asType)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(o, asType, new Newtonsoft.Json.JsonSerializerSettings { });
        }
    }
}
