﻿using System;
using System.Linq;
using MoreLinq;

namespace TrackGenius.Const
{
    public static class ByteArrayExtension
    {
        public static byte[] Cut(this byte[] byteArray, int index, int length)
        {
            if (byteArray == null)
                throw new ArgumentNullException();

            if (index < 0 || index > byteArray.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index + length > byteArray.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            
            return byteArray.Skip(index).Take(length).ToArray();
        }

        public static byte[] Reverse(this byte[] byteArray)
        {
            if (byteArray == null)
                throw new ArgumentNullException();

            var result = new byte[byteArray.Length];
            byteArray.ForEach((b, index) => result[byteArray.Length - index - 1] = b);

            return result;
        }

        public static int ToInt32(this byte[] byteArray)
        {
            if (byteArray == null)
                throw new ArgumentNullException();

            var result = 0;
            byteArray.ForEach((b, index) => result += b << (byteArray.Length - index - 1) * 8);

            return result;
        }
    }
}
