using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KimeraCS.Extensions
{
    public static class BinaryExtensions
    {
        public static T ReadStruct<T>(this BinaryReader reader) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = reader.ReadBytes(size);
            if (buffer.Length != size)
                throw new EndOfStreamException($"Expected {size} bytes but got {buffer.Length}");

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                handle.Free();
            }
        }

        public static void WriteStruct<T>(this BinaryWriter writer, T data) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(data, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                writer.Write(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static void SeekStruct<T>(this BinaryReader reader) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            reader.BaseStream.Seek(size, SeekOrigin.Current);
        }
    }
}