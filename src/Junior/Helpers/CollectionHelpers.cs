using System.Collections.Immutable;

namespace Junior.Helpers
{
    internal static class CollectionHelpers
    {
        public static int BinarySearch<T>(this IReadOnlyList<T> list, T value)
            where T : IComparable<T>
            =>
            BinarySearch(list, 0, list.Count, value);

        public static int BinarySearch<T>(this IReadOnlyList<T> list, int startIndex, int length, T value)
            where T : IComparable<T>
            =>
            BinarySearch(list, startIndex, length, value, (other, value) => value.CompareTo(other));

        public static int BinarySearch<T, TArg>(this IReadOnlyList<T> list, TArg value, Func<TArg, T, int> fnCompare) =>
            BinarySearch(list, 0, list.Count, value, fnCompare);

        public static int BinarySearch<T, TArg>(this IReadOnlyList<T> list, int startIndex, int length, TArg value, Func<TArg, T, int> fnCompare)
        {
            int lo = startIndex;
            int hi = startIndex + length - 1;

            while (lo <= hi)
            {
                int i = (lo + hi) / 2;

                int c = fnCompare(value, list[i]);
                if (c == 0)
                {
                    return i;
                }
                else if (c < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }

            return ~lo;
        }


        public static ImmutableList<T> GetItemsBefore<T>(this ImmutableList<T> list, int index) =>
            (index > 0)
                ? list.RemoveRange(index, list.Count - index)
                : ImmutableList<T>.Empty;

        public static ImmutableList<T> GetItemsAfter<T>(this ImmutableList<T> list, int index) =>
            (index < list.Count - 1)
                ? list.RemoveRange(0, index + 1)
                : ImmutableList<T>.Empty;

        public static ImmutableList<T> RemoveItemsBefore<T>(this ImmutableList<T> list, int index) =>
            (index > 0)
                ? list.RemoveRange(0, index)
                : list;

        public static ImmutableList<T> RemoveItemsAfter<T>(this ImmutableList<T> list, int index) =>
            (index < list.Count - 1)
                ? list.RemoveRange(index + 1, list.Count - (index + 1))
                : list;

        public static ImmutableList<T> SubList<T>(this ImmutableList<T> list, int startingIndex, int count) =>
            list
                .RemoveItemsAfter(startingIndex + count)
                .RemoveItemsBefore(startingIndex);
    }
}
