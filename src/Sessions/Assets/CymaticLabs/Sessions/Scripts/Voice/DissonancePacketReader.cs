using System;
using System.Collections.Generic;
using System.Text;
using Dissonance;
using Dissonance.Audio.Codecs;
using Dissonance.Datastructures;
using Dissonance.Networking.Client;

namespace CymaticLabs.Sessions.Unity3d
{
    internal struct PacketReader
    {
        internal const ushort Magic = 0x8bc7;

        private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(PacketReader).Name);

        #region fields and properties
        private readonly ArraySegment<byte> _array;
        private int _count;

        public ArraySegment<byte> Read
        {
            // ReSharper disable once AssignNullToNotNullAttribute (Justification: Array segment Array property cannot be null, unless this is a default instance in which case something else is horribly wrong)
            get { return new ArraySegment<byte>(_array.Array, _array.Offset, _count); }
        }

        public ArraySegment<byte> Unread
        {
            // ReSharper disable once AssignNullToNotNullAttribute (Justification: Array segment Array property cannot be null)
            get { return new ArraySegment<byte>(_array.Array, _array.Offset + _count, _array.Count - _count); }
        }

        public ArraySegment<byte> All
        {
            get { return _array; }
        }
        #endregion

        #region constructor
        public PacketReader(ArraySegment<byte> array)
        {
            if (array.Array == null)
                throw new ArgumentNullException("array");

            _array = array;
            _count = 0;
        }

        public PacketReader([NotNull] byte[] array)
            : this(new ArraySegment<byte>(array))
        {
        }
        #endregion

        #region read primitive
        private void Check(int count, string type)
        {
            if (_array.Count - count - _count < 0)
                throw Log.CreatePossibleBugException(string.Format("Insufficient space in packet reader to read {0}", type), "4AFBC61A-77D4-43B8-878F-796F0D921184");
        }

        /// <summary>
        /// Read a byte without performing a check on the size of the array first
        /// </summary>
        /// <returns></returns>
        private byte FastReadByte()
        {
            _count++;
            // ReSharper disable once PossibleNullReferenceException (Justification: Array segment Array property cannot be null)
            return _array.Array[_array.Offset + _count - 1];
        }

        /// <summary>
        /// Read a single byte
        /// </summary>
        /// <returns></returns>
        public byte ReadByte()
        {
            Check(sizeof(byte), "byte");

            return FastReadByte();
        }

        /// <summary>
        /// Read a 16 bit unsigned integer
        /// </summary>
        /// <returns></returns>
        public ushort ReadUInt16()
        {
            Check(sizeof(ushort), "ushort");

            var un = new Union16
            {
                MSB = FastReadByte(),
                LSB = FastReadByte()
            };

            return un.UInt16;
        }

        /// <summary>
        /// Read a 32 bit unsigned integer
        /// </summary>
        /// <returns></returns>
        public uint ReadUInt32()
        {
            Check(sizeof(uint), "uint");

            var un = new Union32();

            un.SetBytesFromNetworkOrder(
                FastReadByte(),
                FastReadByte(),
                FastReadByte(),
                FastReadByte()
            );

            return un.UInt32;
        }

        /// <summary>
        /// Read a slice of the internal array (returns a reference to the array, does not perform a copy).
        /// </summary>
        /// <returns></returns>
        public ArraySegment<byte> ReadByteSegment()
        {
            //Read length prefix
            var length = ReadUInt16();

            //Now check that the rest of the data is available
            Check(length, "byte[]");

            //Get the segment from the middle of the buffer
            // ReSharper disable once AssignNullToNotNullAttribute (Justification: Array segment Array property cannot be null)
            var segment = new ArraySegment<byte>(_array.Array, Unread.Offset, length);
            _count += length;

            return segment;
        }

        /// <summary>
        /// Read a string (potentially null)
        /// </summary>
        /// <returns></returns>
        [CanBeNull]
        public string ReadString()
        {
            //Read the length prefix
            var length = ReadUInt16();

            //Special case for null
            if (length == 0)
                return null;
            else
                length--;

            //Now check that the rest of the string is available
            Check(length, "string");

            //Read the string
            var unread = Unread;
            // ReSharper disable once AssignNullToNotNullAttribute (Justification: Array segment Array property cannot be null)
            var str = Encoding.UTF8.GetString(unread.Array, unread.Offset, length);

            //Apply the offset over the string length
            _count += length;

            return str;
        }

        #endregion

        #region read high level
        public bool ReadPacketHeader(out DisonnanceMessageTypes messageType)
        {
            var magic = ReadUInt16() == Magic;

            if (magic)
                messageType = (DisonnanceMessageTypes)ReadByte();
            else
                messageType = default(DisonnanceMessageTypes);

            return magic;
        }

        #endregion
    }
}
