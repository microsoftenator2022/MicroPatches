using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace MicroPatches;
internal static class OptionalExtension
{
    //internal static Optional<U> Select<T, U>(this Optional<T> optional, Func<T, U> func)
    //{
    //    if (optional.HasValue)
    //        return func(optional.Value);
    //    return default;
    //}

    internal static Optional<U> Upcast<T, U>(this Optional<T> optional) where T : U
    {
        if (optional.HasValue)
            return optional.Value;

        return default;
    }
}

public static class Optional
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> None<T>() => default;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> Some<T>(T value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<TResult> Map<T, TResult>(this Optional<T> optional, Func<T, TResult> map) =>
        optional.HasValue ? (Optional<TResult>)map(optional.Value) : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> OfNullable<T>(this T? value) where T : notnull =>
        value is not null ? (Optional<T>)value : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? ToObj<T>(this Optional<T> optional) => optional.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> Return<T>(T value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<TResult> Bind<T, TResult>(this Optional<T> optional, Func<T, Optional<TResult>> binder) =>
        optional.HasValue ? binder(optional.Value) : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<Optional<T>, Optional<TResult>> Lift<T, TResult>(Func<T, TResult> f) => x => x.Map(f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> OrElseWith<T>(this Optional<T> optional, Func<Optional<T>> orElseThunk) =>
        optional.HasValue ? optional.Value : orElseThunk();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> OrElse<T>(this Optional<T> optional, Optional<T> ifNoValue) =>
        optional.HasValue ? optional : ifNoValue;

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static Optional<TResult> Apply<T, TResult>(this Optional<Func<T, TResult>> lifted, Optional<T> optional) =>
    //    lifted.HasValue ? optional.Map(lifted.Value) : default;

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static Func<Optional<T>, Optional<TResult>> Apply<T, TResult>(Optional<Func<T, TResult>> lifted) =>
    //    optional => lifted.Apply(optional);

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static Optional<TResult> Apply2<T1, T2, TResult>(this Optional<Func<T1, T2, TResult>> lifted,
    //    Optional<T1> optional1,
    //    Optional<T2> optional2) =>
    //    lifted.Map(F.Curry).Apply(optional1).Apply(optional2);

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static Optional<TResult> Apply<T1, T2, TResult>(this Optional<Func<T1, T2, TResult>> lifted,
    //    Optional<T1> optional1,
    //    Optional<T2> optional2) =>
    //    Apply2(lifted, optional1, optional2);

    public static IEnumerable<U> Choose<T, U>(this IEnumerable<T> source, Func<T, Optional<U>> chooser)
    {
        foreach (var element in source)
        {
            var selected = chooser(element);
            if (selected.HasValue)
                yield return selected.Value;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> TryFirst<T>(this IEnumerable<T> source) =>
        source.Select((x => (Optional<T>)x)).FirstOrDefault();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> TryFind<T>(this IEnumerable<T> source, Func<T, bool> predicate) =>
        source.Where(predicate).TryFirst();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DefaultWith<T>(this Optional<T> optional, Func<T> defaultThunk) =>
        optional.HasValue ? optional.Value : defaultThunk();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DefaultValue<T>(this Optional<T> optional, T defaultValue) =>
        optional.HasValue ? optional.Value : defaultValue;
}
