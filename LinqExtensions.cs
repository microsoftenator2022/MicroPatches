using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroUtils.Linq
{
    public static class LinqExtensions
    {
        public static IEnumerable<(int index, T item)> Indexed<T>(this IEnumerable<T> source)
        {
            var index = 0;
            foreach (var item in source)
                yield return (index++, item);
        }

        public static IEnumerable<T> FindSequence<T>(this IEnumerable<T> source, int length, IEnumerable<Func<T, bool>> predicateSequence)
        {
            var i = 0;
            foreach (var result in predicateSequence.Zip(source, (f, x) => f(x)))
            {
                if (!result) return source.Skip(1).FindSequence(length, predicateSequence);

                i++;

                if (i >= length) return source.Take(i);
            }

            return Enumerable.Empty<T>();
        }

        public static IEnumerable<T> FindSequence<T>(this IEnumerable<T> source, IEnumerable<Func<T, bool>> predicateSequence) =>
            source.FindSequence(predicateSequence.Count(), predicateSequence);

        public static IEnumerable<T> FindSequence<T>(this IEnumerable<T> source, int length, Func<IEnumerable<T>, bool> predicate)
        {
            var subSeq = source.Take(length);
            if (subSeq.Count() < length) return Enumerable.Empty<T>();

            if (predicate(subSeq)) return subSeq;

            return source.Skip(1).FindSequence(length, predicate);
        }

        public static (T, IEnumerable<T>) Pop<T>(this IEnumerable<T> source) => (source.First(), source.Skip(1));
    }
}
