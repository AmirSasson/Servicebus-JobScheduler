using System.Linq;
using System.Text.Json;

namespace Servicebus.JobScheduler.Core.Utils
{
    public static class Validator
    {
        public static void EnsureNotNull<T>(T o, string exceptionMsg)
        {
            if (o == null)
            {
                throw new System.NullReferenceException(exceptionMsg);
            }
        }

        public static void EnsureAtLeastOneNotNull(string exceptionMsg, params object[] args)
        {            
            if (args.All(arg=> arg == null))
            {
                throw new System.NullReferenceException(exceptionMsg);
            }
        }

        public static void EnsureAllNotNull(string exceptionMsg, params object[] args)
        {
            if (args.Any(arg => arg == null))
            {
                throw new System.NullReferenceException(exceptionMsg);
            }
        }
    }
}
