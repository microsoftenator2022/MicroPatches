using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
}
