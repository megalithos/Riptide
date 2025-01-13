// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Riptide.Collections
{
    internal class RingBuffer<T> : IEnumerable<T>
    {
        int _head;
        int _tail;
        int _count;

        T[] _array;

        public int Count => _count;
        public bool IsFull => _count == _array.Length;

        public RingBuffer(int capacity)
        {
            _array = new T[capacity];
        }

        public T Peek()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException();
            }

            return _array[_tail];
        }
        
        public T PeekLast()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException();
            }
            int index = _head - 1;
            if (index < 0)
                index = _array.Length - 1;
            return _array[index];
        }

        public void Push(T item)
        {
            if (IsFull)
            {
                throw new InvalidOperationException();
            }

            _array[_head] = item;
            _head = (_head + 1) % _array.Length;
            _count += 1;
        }

        public T Pop()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException();
            }

            var item = _array[_tail];

            _array[_tail] = default;
            _tail = (_tail + 1) % _array.Length;
            _count -= 1;

            return item;
        }

        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;

            Array.Clear(_array, 0, _array.Length);
        }

        public int Capacity => _array.Length;

        public IEnumerator<T> GetEnumerator()
        {
            int index = _tail;
            for (int i = 0; i < _count; i++)
            {
                yield return _array[index];
                index = (index + 1) % _array.Length;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Resize(int newSize)
        {
            AssertUtil.True(newSize > Capacity, "newSize > Capacity()");

            T[] newArray = new T[newSize];

            int counter = 0;
            foreach (var element in this)
            {
                newArray[counter] = element;
                counter++;
            }

            _tail = 0;
            _head = counter;

            _array = newArray;
        }
    }
}
