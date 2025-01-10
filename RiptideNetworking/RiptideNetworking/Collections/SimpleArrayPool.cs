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
    /// <summary>
    /// Simple pool with array size fixed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class SimpleArrayPool<T>
    {
        private readonly int arrayLength;

        private RingBuffer<T[]> pool;

        public SimpleArrayPool(int arrayLength, int capacity)
        {
            this.arrayLength = arrayLength;

            pool = new RingBuffer<T[]>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                pool.Push(new T[arrayLength]);
            }
        }

        public T[] Acquire()
        {
            if (pool.Count == 0)
            {
                int prevCapacity = pool.Capacity;
                pool.Resize(prevCapacity * 2);
                for (int i = 0; i < prevCapacity; i++)
                {
                    pool.Push(new T[arrayLength]);
                }
            }
            return pool.Pop();
        }

        public void Release(T[] array)
        {
            pool.Push(array);
        }

        public int Capacity()
        {
            return pool.Capacity;
        }
    }
}
