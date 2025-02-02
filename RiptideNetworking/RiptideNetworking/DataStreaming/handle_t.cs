// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.DataStreaming
{
    public class handle_comparer : IComparer<handle_t>
    {
        public int Compare(handle_t x, handle_t y)
        {
            return ((int)x).CompareTo(((int)y));
        }
    }

    public struct handle_t : IEquatable<handle_t>
    {
        private readonly int _value;

        public handle_t(int value)
        {
            _value = value;
        }

        public static implicit operator handle_t(int value) => new handle_t(value);

        public static implicit operator int(handle_t handle) => handle._value;

        public override string ToString() => _value.ToString();

        public override bool Equals(object obj) => obj is handle_t other && Equals(other);
        public bool Equals(handle_t other) => _value == other._value;

        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(handle_t left, handle_t right) => left.Equals(right);
        public static bool operator !=(handle_t left, handle_t right) => !left.Equals(right);
    }

}
