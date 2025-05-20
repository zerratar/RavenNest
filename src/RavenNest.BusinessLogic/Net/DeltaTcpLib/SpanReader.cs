// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Text;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    public static class SpanReader
    {
        public static ulong ReadVarUInt(this ReadOnlySpan<byte> span, ref int pos)
        {
            var (v, i) = VarInt.ReadVarUInt(span.Slice(pos));
            pos += i;
            return v;
        }
        public static byte ReadByte(this ReadOnlySpan<byte> span, ref int pos) => span[pos++];
        public static Guid ReadGuid(this ReadOnlySpan<byte> span, ref int pos)
        {
            var g = new Guid(span.Slice(pos, 16));
            pos += 16;
            return g;
        }
        public static uint ReadUInt32BE(this ReadOnlySpan<byte> span, ref int pos)
        {
            var v = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(pos, 4));
            pos += 4;
            return v;
        }
        public static short ReadInt16BE(this ReadOnlySpan<byte> span, ref int pos)
        {
            var v = BinaryPrimitives.ReadInt16BigEndian(span.Slice(pos, 2));
            pos += 2;
            return v;
        }
        public static float ReadFloatBE(this ReadOnlySpan<byte> span, ref int pos)
        {
            var bits = BinaryPrimitives.ReadInt32BigEndian(span.Slice(pos, 4)); pos += 4;
            return BitConverter.Int32BitsToSingle(bits);
        }
        public static bool ReadBool(this ReadOnlySpan<byte> span, ref int pos) => span[pos++] != 0;
        public static string ReadString(this ReadOnlySpan<byte> span, ref int pos)
        {
            var len = (int)span.ReadVarUInt(ref pos);
            if (len == 0 || len >= span.Length) return string.Empty;
            var s = Encoding.UTF8.GetString(span.Slice(pos, len));
            pos += len;
            return s;
        }
        public static string ReadShortString(this ReadOnlySpan<byte> span, ref int pos)
        {
            var len = (byte)span[pos++];
            if (len == 0) return string.Empty;
            var s = Encoding.UTF8.GetString(span.Slice(pos, len));
            pos += len;
            return s;
        }
        public static DateTime ReadDateTime(this ReadOnlySpan<byte> span, ref int pos)
        {
            var ticks = (long)span.ReadVarUInt(ref pos);
            return new DateTime(ticks, DateTimeKind.Utc);
        }
        public static Island ReadIsland(this ReadOnlySpan<byte> span, ref int pos) => (Island)span[pos++];
        public static CharacterFlags ReadFlags(this ReadOnlySpan<byte> span, ref int pos)
        { var v = BinaryPrimitives.ReadInt32BigEndian(span.Slice(pos, 4)); pos += 4; return (CharacterFlags)v; }
    }
}
