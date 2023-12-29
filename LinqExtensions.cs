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

        /// <summary>
        /// Selects distinct elements from a sequence, first applying a selector function and using a provided equality comparer
        /// </summary>
        public static IEnumerable<T> DistinctBy<T, U>(this IEnumerable<T> seq, Func<T, U> selector, IEqualityComparer<U> comparer)
        {
            var distinct = new List<U>();

            foreach (var item in seq)
            {
                var selected = selector(item);

                if (!distinct.Contains(selected, comparer))
                {
                    distinct.Add(selected);
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Creates a dictionary from a sequence of Key/Value pairs
        /// </summary>

        /// <typeparam name="TKey">Key type</typeparam>
        /// <typeparam name="TValue">Value type</typeparam>
        /// <param name="source">Source sequence</param>
        public static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey key, TValue value)> source) =>
            source.ToDictionary(kv => kv.key, kv => kv.value);

        /// <summary>
        /// Creates a dictionary from a sequence of Key/Value pairs using a provided <see cref="IEqualityComparer{T}"/>
        /// </summary>
        /// <param name="source">Source sequence</param>
        /// <param name="keyComparer">Key equality comparer</param>
        public static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>
            (this IEnumerable<(TKey key, TValue value)> source, IEqualityComparer<TKey> keyComparer) =>
            source.ToDictionary(kv => kv.key, kv => kv.value, keyComparer);


        /// <summary>
        /// Selects distinct elements from a sequence, first applying a selector function and using the default equality comparer
        /// </summary>
        public static IEnumerable<T> DistinctBy<T, U>(this IEnumerable<T> seq, Func<T, U> selector) => DistinctBy(seq, selector, EqualityComparer<U>.Default);
    }
}
