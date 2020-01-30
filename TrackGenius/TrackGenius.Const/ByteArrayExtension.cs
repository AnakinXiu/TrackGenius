using System;
using System.Core;
using MoreLinq;

namespace TrackGenius.Const
{
    public static class ByteArrayExtension
    {
        public static byte[] Cut(this byte[] byteArray, int index, int length)
        {
            if (index < 0 || index > byteArray.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index + length > byteArray.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            var result = new byte[length];
            byteArray.CopyTo(result, index);

            return result;
        }

        public static byte[] Reverse(this byte[] byteArray)
        {
            var result = new byte[byteArray.Length];
            byteArray.ForEach((b, index) => result[byteArray.Length - index] = b);

            return result;
        }

        public static int ToInt32(this byte[] byteArray)
        {
            var result = 0;
            byteArray.ForEach((b, index) => result += b << (byteArray.Length - index - 1) * 8);

            return result;
        }
    }
}
