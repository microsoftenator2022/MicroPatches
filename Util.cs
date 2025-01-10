using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches;
public static class Util
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Id<T>(T value) => value;

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

    public static IEnumerable<FieldInfo> GetAllFields(this Type type)
    {
        foreach (var f in type.GetFields(AccessTools.allDeclared))
            yield return f;

        if (type.BaseType is not null)
            foreach (var f in type.BaseType.GetAllFields())
                yield return f;
    }

    public static IEnumerable<MemberInfo> GetAllMembers(this Type type)
    {
        foreach (var m in type.GetMembers(AccessTools.allDeclared))
            yield return m;

        if (type.BaseType is not null)
            foreach (var m in type.BaseType.GetAllMembers())
                yield return m;
    }

    public static IEnumerable<PropertyInfo> GetAllProperties(this Type type)
    {
        foreach (var m in type.GetProperties(AccessTools.allDeclared))
            yield return m;

        if (type.BaseType is not null)
            foreach (var m in type.BaseType.GetAllProperties())
                yield return m;
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
