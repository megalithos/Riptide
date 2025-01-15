// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.Utils
{
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct ByteAmount : IEquatable<ByteAmount>
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        private const long BytesPerKilobyte = 1000;
        private const long BytesPerMegabyte = BytesPerKilobyte * 1000;

        private long totalBytes;

        public ByteAmount(int bytes)
        {
            totalBytes = bytes;
        }

        public ByteAmount(long bytes)
        {
            totalBytes = bytes;
        }

        public long Bytes
        {
            get => totalBytes;
        }

        public double Kilobytes
        {
            get => totalBytes / (double)BytesPerKilobyte;
        }

        public double Megabytes
        {
            get => totalBytes / (double)BytesPerMegabyte;
        }

        public override string ToString()
        {
            if (totalBytes >= BytesPerMegabyte)
                return $"{Megabytes:0.##} MB";
            else if (totalBytes >= BytesPerKilobyte)
                return $"{Kilobytes:0.##} KB";
            else
                return $"{Bytes} bytes";
        }

        public static ByteAmount FromKilobytes(double kilobytes)
        {
            return new ByteAmount((long)(Math.Round(kilobytes * BytesPerKilobyte)));
        }

        public static ByteAmount FromMegabytes(double megabytes)
        {
            return new ByteAmount((long)(Math.Round(megabytes * BytesPerMegabyte)));
        }

        public static bool operator ==(ByteAmount lhs, ByteAmount rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ByteAmount lhs, ByteAmount rhs)
        {
            return !lhs.Equals(rhs);
        }

        public static bool operator <(ByteAmount lhs, ByteAmount rhs)
        {
            return lhs.Bytes < rhs.Bytes;
        }

        public static bool operator >(ByteAmount lhs, ByteAmount rhs)
        {
            return lhs.Bytes > rhs.Bytes;
        }

        public override int GetHashCode()
        {
            return totalBytes.GetHashCode();
        }

        public bool Equals(ByteAmount other)
        {
            return totalBytes == other.totalBytes;
        }
    }
}
