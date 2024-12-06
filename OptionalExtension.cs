using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace MicroPatches;
internal static class OptionalExtension
{
    internal static Optional<U> Select<T, U>(this Optional<T> optional, Func<T, U> func)
    {
        if (optional.HasValue)
            return func(optional.Value);
        return default;
    }

    internal static Optional<U> Upcast<T, U>(this Optional<T> optional) where T : U
    {
        if (optional.HasValue)
            return optional.Value;

        return default;
    }
}
