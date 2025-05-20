// -----------------------------------------------------------------------------
// DeltaTcpLib.cs
// A reusable .NET Standard library for delta-based TCP messaging
// -----------------------------------------------------------------------------
using System;

namespace RavenNest.BusinessLogic.Net.DeltaTcpLib
{
    // -------------------------------------------------------------------------
    // VarInt and Span reader/writer
    // -------------------------------------------------------------------------
    public static class VarInt
    {
        public static int WriteVarUInt(Span<byte> buf, ulong v)
        {
            int i = 0;
            while (v >= 0x80)
            {
                buf[i++] = (byte)(v | 0x80);
                v >>= 7;
            }
            buf[i++] = (byte)v;
            return i;
        }
        public static (ulong, int) ReadVarUInt(ReadOnlySpan<byte> buf)
        {
            ulong r = 0;
            int s = 0, i = 0;
            byte b;
            do
            {
                b = buf[i];
                r |= (ulong)(b & 0x7F) << s;
                s += 7;
                i++;
            } while ((b & 0x80) != 0);
            return (r, i);
        }
    }
}
