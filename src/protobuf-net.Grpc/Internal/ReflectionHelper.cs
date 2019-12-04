using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    internal static class ReflectionHelper
    {
        public static MethodInfo GetContinueWithForTask(Type inputType, Type outputType)
        {
            var genericContinueWith = typeof(Task<>).MakeGenericType(inputType)
                .GetMethods().Where(IsDesiredContinueWithMethod).Single();
            return genericContinueWith.MakeGenericMethod(outputType);
        }

        public static bool IsDesiredContinueWithMethod(MethodInfo mi)
        {
            if (mi.Name != nameof(Task.ContinueWith))
                return false;
            var parameters = mi.GetParameters();
            if (parameters.Length != 1)
                return false;
            var funcParamType = parameters[0].ParameterType;
            if (!funcParamType.IsGenericType || funcParamType.GetGenericTypeDefinition() != typeof(Func<,>))
                return false;
            var taskArgument = funcParamType.GetGenericArguments()[0];
            return taskArgument.IsGenericType && taskArgument.GetGenericTypeDefinition() == typeof(Task<>);
        }

    }
}
