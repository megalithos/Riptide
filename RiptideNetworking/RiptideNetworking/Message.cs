﻿using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    internal enum HeaderType : byte
    {
        unreliable,
        ack,
        ackExtra,
        connect,
        heartbeat,
        disconnect,
        reliable,
        welcome,
        clientConnected,
        clientDisconnected,
    }

    public class Message // TODO: endianness
    {
        /// <summary>The length in bytes of the message's contents.</summary>
        public int Length { get => buffer.Count; }

        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength { get => Length - readPos; }

        private List<byte> buffer;
        private byte[] readableBuffer;
        private ushort readPos = 0;

        /// <summary>Creates a new empty message (without an ID).</summary>
        internal Message(ushort messageLength = 0)
        {
            buffer = new List<byte>(messageLength + 3); // +3 for message header
        }

        /// <summary>Creates a new message with a given ID.</summary>
        /// <param name="id">The message ID.</param>
        /// <param name="messageLength">The length in bytes of the message's contents.</param>
        public Message(ushort id, ushort messageLength = 0)
        {
            buffer = new List<byte>(messageLength + 5); // +5 for message header
            Add(id);
        }

        /// <summary>Creates a message from which data can be read. Used for receiving.</summary>
        /// <param name="data">The bytes to add to the message.</param>
        internal Message(byte[] data)
        {
            buffer = new List<byte>(data.Length);

            SetBytes(data);
        }

        #region Functions
        /// <summary>Sets the message's content and prepares it to be read.</summary>
        /// <param name="data">The bytes to add to the message.</param>
        internal void SetBytes(byte[] data)
        {
            Add(data);
            readableBuffer = buffer.ToArray();
        }

        /// <summary>Prepares the message for sending.</summary>
        internal void PrepareToSend(HeaderType headerType)
        {
            InsertByte((byte)headerType); // Header type
        }

        /// <summary>Inserts the given bytes at the start of the message.</summary>
        /// <param name="bytes">The bytes to insert.</param>
        internal void InsertBytes(byte[] bytes)
        {
            buffer.InsertRange(0, bytes);
        }

        /// <summary>Inserts the given byte at the start of the message.</summary>
        /// <param name="singleByte">The byte to insert.</param>
        private void InsertByte(byte singleByte)
        {
            buffer.Insert(0, singleByte);
        }

        /// <summary>Gets the message's content in array form.</summary>
        internal byte[] ToArray()
        {
            readableBuffer = buffer.ToArray();
            return readableBuffer;
        }
        #endregion

        #region Write & Read Data
        #region Byte
        /// <summary>Adds a byte to the message.</summary>
        /// <param name="value">The byte to add.</param>
        public void Add(byte value)
        {
            buffer.Add(value);
        }

        /// <summary>Adds an array of bytes to the message.</summary>
        /// <param name="value">The byte array to add.</param>
        public void Add(byte[] value)
        {
            buffer.AddRange(value);
        }

        /// <summary>Reads a byte from the message.</summary>
        public byte GetByte()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                byte value = readableBuffer[readPos]; // Get the byte at readPos' position
                readPos += 1;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'byte'!");
            }
        }

        /// <summary>Reads an array of bytes from the message.</summary>
        /// <param name="length">The length of the byte array.</param>
        public byte[] GetBytes(int length)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                byte[] value = buffer.GetRange(readPos, length).ToArray(); // Get the bytes at readPos' position with a range of length
                readPos += (ushort)length;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'byte[]'!");
            }
        }
        #endregion

        #region Bool
        /// <summary>Adds a bool to the message.</summary>
        /// <param name="value">The bool to add.</param>
        public void Add(bool value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a bool from the message.</summary>
        public bool GetBool()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                bool value = BitConverter.ToBoolean(readableBuffer, readPos); // Convert the bytes at readPos' position to a bool
                readPos += 1;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'bool'!");
            }
        }

        /// <summary>Adds an array of bools to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(bool[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of bools from the message.</summary>
        public bool[] GetBoolArray()
        {
            return GetBoolArray(GetUShort());
        }
        /// <summary>Reads an array of bools from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public bool[] GetBoolArray(ushort length)
        {
            bool[] array = new bool[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetBool();

            return array;
        }
        #endregion

        #region Short
        /// <summary>Adds a short to the message.</summary>
        /// <param name="value">The short to add.</param>
        public void Add(short value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a short from the message.</summary>
        public short GetShort()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                short value = BitConverter.ToInt16(readableBuffer, readPos); // Convert the bytes at readPos' position to a short
                readPos += 2;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'short'!");
            }
        }

        /// <summary>Adds an array of shorts to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(short[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of shorts from the message.</summary>
        public short[] GetShortArray()
        {
            return GetShortArray(GetUShort());
        }
        /// <summary>Reads an array of shorts from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public short[] GetShortArray(ushort length)
        {
            short[] array = new short[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetShort();

            return array;
        }
        #endregion

        #region UShort
        /// <summary>Adds a ushort to the message.</summary>
        /// <param name="value">The ushort to add.</param>
        public void Add(ushort value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a ushort from the message.</summary>
        public ushort GetUShort()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                ushort value = BitConverter.ToUInt16(readableBuffer, readPos); // Convert the bytes at readPos' position to a ushort
                readPos += 2;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'ushort'!");
            }
        }

        /// <summary>Adds an array of ushorts to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(ushort[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of ushorts from the message.</summary>
        public ushort[] GetUShortArray()
        {
            return GetUShortArray(GetUShort());
        }
        /// <summary>Reads an array of ushorts from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public ushort[] GetUShortArray(ushort length)
        {
            ushort[] array = new ushort[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetUShort();
            
            return array;
        }
        #endregion

        #region Int
        /// <summary>Adds an int to the message.</summary>
        /// <param name="value">The int to add.</param>
        public void Add(int value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads an int from the message.</summary>
        public int GetInt()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                int value = BitConverter.ToInt32(readableBuffer, readPos); // Convert the bytes at readPos' position to an int
                readPos += 4;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'int'!");
            }
        }

        /// <summary>Adds an array of ints to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(int[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of ints from the message.</summary>
        public int[] GetIntArray()
        {
            return GetIntArray(GetUShort());
        }
        /// <summary>Reads an array of ints from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public int[] GetIntArray(ushort length)
        {
            int[] array = new int[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetInt();

            return array;
        }
        #endregion

        #region UInt
        /// <summary>Adds a uint to the message.</summary>
        /// <param name="value">The uint to add.</param>
        public void Add(uint value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a uint from the message.</summary>
        public uint GetUInt()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                uint value = BitConverter.ToUInt32(readableBuffer, readPos); // Convert the bytes at readPos' position to an uint
                readPos += 4;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'uint'!");
            }
        }

        /// <summary>Adds an array of uints to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(uint[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of uints from the message.</summary>
        public uint[] GetUIntArray()
        {
            return GetUIntArray(GetUShort());
        }
        /// <summary>Reads an array of uints from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public uint[] GetUIntArray(ushort length)
        {
            uint[] array = new uint[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetUInt();

            return array;
        }
        #endregion

        #region Long
        /// <summary>Adds a long to the message.</summary>
        /// <param name="value">The long to add.</param>
        public void Add(long value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a long from the message.</summary>
        public long GetLong()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                long value = BitConverter.ToInt64(readableBuffer, readPos); // Convert the bytes at readPos' position to a long
                readPos += 8;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'long'!");
            }
        }

        /// <summary>Adds an array of longs to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(long[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of longs from the message.</summary>
        public long[] GetLongArray()
        {
            return GetLongArray(GetUShort());
        }
        /// <summary>Reads an array of longs from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public long[] GetLongArray(ushort length)
        {
            long[] array = new long[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetLong();

            return array;
        }
        #endregion

        #region ULong
        /// <summary>Adds a ulong to the message.</summary>
        /// <param name="value">The ulong to add.</param>
        public void Add(ulong value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a ulong from the message.</summary>
        public ulong GetULong()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                ulong value = BitConverter.ToUInt64(readableBuffer, readPos); // Convert the bytes at readPos' position to a ulong
                readPos += 8;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'ulong'!");
            }
        }

        /// <summary>Adds an array of ulongs to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(ulong[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of ulongs from the message.</summary>
        public ulong[] GetULongArray()
        {
            return GetULongArray(GetUShort());
        }
        /// <summary>Reads an array of ulongs from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public ulong[] GetULongArray(ushort length)
        {
            ulong[] array = new ulong[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetULong();

            return array;
        }
        #endregion

        #region Float
        /// <summary>Adds a float to the message.</summary>
        /// <param name="value">The float to add.</param>
        public void Add(float value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a float from the message.</summary>
        public float GetFloat()
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                float value = BitConverter.ToSingle(readableBuffer, readPos); // Convert the bytes at readPos' position to a float
                readPos += 4;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'float'!");
            }
        }

        /// <summary>Adds an array of floats to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(float[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of floats from the message.</summary>
        public float[] GetFloatArray()
        {
            return GetFloatArray(GetUShort());
        }
        /// <summary>Reads an array of floats from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public float[] GetFloatArray(ushort length)
        {
            float[] array = new float[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetFloat();

            return array;
        }
        #endregion

        #region Double
        /// <summary>Adds a double to the message.</summary>
        /// <param name="value">The double to add.</param>
        public void Add(double value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Reads a double from the message.</summary>
        public double GetDouble()
        {
            if (buffer.Count > readPos + 8)
            {
                // If there are unread bytes
                double value = BitConverter.ToDouble(readableBuffer, readPos); // Convert the bytes at readPos' position to a double
                readPos += 8;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'double'!");
            }
        }

        /// <summary>Adds an array of doubles to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(double[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of doubles from the message.</summary>
        public double[] GetDoubleArray()
        {
            return GetDoubleArray(GetUShort());
        }
        /// <summary>Reads an array of doubles from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public double[] GetDoubleArray(ushort length)
        {
            double[] array = new double[length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = GetDouble();
            }
            return array;
        }
        #endregion

        #region String
        /// <summary>Adds a string to the message.</summary>
        /// <param name="value">The string to add.</param>
        public void Add(string value)
        {
            Add((ushort)value.Length); // Add the length of the string to the message
            Add(Encoding.UTF8.GetBytes(value)); // Add the string itself
        }

        /// <summary>Reads a string from the message.</summary>
        public string GetString()
        {
            ushort length = GetUShort(); // Get the length of the string
            if (buffer.Count >= readPos + length)
            {
                string value = Encoding.UTF8.GetString(readableBuffer, readPos, length); // Convert the bytes at readPos' position to a string
                readPos += length;
                return value;
            }
            else
            {
                throw new Exception("Message contains insufficient bytes to read type 'string'!");
            }
        }

        /// <summary>Adds an array of strings to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to add the length of the array to the message.</param>
        public void Add(string[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);
        }

        /// <summary>Reads an array of strings from the message.</summary>
        public string[] GetStringArray()
        {
            return GetStringArray(GetUShort());
        }
        /// <summary>Reads an array of strings from the message.</summary>
        /// <param name="length">The length of the array.</param>
        public string[] GetStringArray(ushort length)
        {
            string[] array = new string[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetString();

            return array;
        }
        #endregion
        #endregion
    }
}