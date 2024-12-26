using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Kingmaker;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches;
public static class Util
{
    public static bool IsAssignableTo<T>(this Type type) => typeof(T).IsAssignableFrom(type);

    public static MethodInfo? GetInterfaceMethodImplementation(this Type declaringType, MethodInfo interfaceMethod)
    {
        var map = declaringType.GetInterfaceMap(interfaceMethod.DeclaringType);
        return map.InterfaceMethods
            ?.Zip(map.TargetMethods, (i, t) => (i, t))
            .FirstOrDefault(pair => pair.i == interfaceMethod)
            .t;
    }

    public static Type? GetListTypeElementType(Type listType)
    {
        var interfaceType = listType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IList<>));
        
        if (interfaceType is null)
            return null;

        return interfaceType.GetGenericArguments()?[0];
    }

    public static void DebugLog(this LogChannel channel, string message, LogSeverity severity = LogSeverity.Message)
    {
#if DEBUG
        Action<string> logF = severity switch
        {
            LogSeverity.Message => channel.Log,
            LogSeverity.Warning => channel.Warning,
            LogSeverity.Error => channel.Error,
            _ => _ => { }
        };

        logF(message);
#endif
    }

    public static void DebugLog(this LogChannel channel, Func<bool> predicate, string message, LogSeverity severity = LogSeverity.Message)
    {
#if DEBUG
        if (predicate()) channel.DebugLog(message, severity);
#endif
    }

    public static void DebugLogException(this LogChannel channel, Exception e)
    {
#if DEBUG
        channel.Exception(e);
#endif
    }
}
