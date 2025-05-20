// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    public static class SpanWriter
    {
        public static int Write(this Span<byte> span, Guid g)
        {
            g.ToByteArray().CopyTo(span);
            return 16;
        }
        public static int Write(this Span<byte> span, ulong v) => VarInt.WriteVarUInt(span, v);
        public static int Write(this Span<byte> span, uint v)
        {
            BinaryPrimitives.WriteUInt32BigEndian(span, v);
            return 4;
        }
        public static int Write(this Span<byte> span, short v)
        {
            BinaryPrimitives.WriteInt16BigEndian(span, v);
            return 2;
        }
        public static int Write(this Span<byte> span, float v)
        {
            var b = BitConverter.SingleToInt32Bits(v);
            BinaryPrimitives.WriteInt32BigEndian(span, b);
            return 4;
        }
        public static int Write(this Span<byte> span, bool v)
        {
            span[0] = (byte)(v ? 1 : 0);
            return 1;
        }
        public static int Write(this Span<byte> span, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return VarInt.WriteVarUInt(span, (ulong)0);
            }

            var b = Encoding.UTF8.GetBytes(s);
            int p = VarInt.WriteVarUInt(span, (ulong)b.Length);
            b.CopyTo(span.Slice(p));
            return p + b.Length;
        }
        public static int Write(this Span<byte> span, DateTime dt) => VarInt.WriteVarUInt(span, (ulong)dt.Ticks);
        public static int Write(this Span<byte> span, Island i)
        {
            span[0] = (byte)i;
            return 1;
        }
        public static int Write(this Span<byte> span, CharacterFlags f)
        {
            BinaryPrimitives.WriteInt32BigEndian(span, (int)f);
            return 4;
        }
    }
}
