// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.Collections
{
    internal struct ArraySlice<T> : IEquatable<ArraySlice<T>>
    {
        public T[] Array;
        public int StartIndex;
        public int Length;

        public ArraySlice(T[] array, int startIndex, int length)
        {
            Array = array;
            StartIndex = startIndex;
            Length = length;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range for ArraySlice of length {Length}.");
                return Array[StartIndex + index];
            }
            set
            {
                if (index < 0 || index >= Length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range for ArraySlice of length {Length}.");
                Array[StartIndex + index] = value;
            }
        }

        public bool Equals(ArraySlice<T> other)
        {
            return Array == other.Array &&
                Length == other.Length &&
                StartIndex == other.StartIndex;
        }

        public bool IsNull()
        {
            return Array == null;
        }
    }
}
