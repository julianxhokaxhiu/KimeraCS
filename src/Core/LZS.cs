//    SPDX-FileCopyrightText: 2012 - 2021 Arzel Jérôme <myst6re@gmail.com>
//    SPDX-License-Identifier: LGPL-3.0-or-later
/***************************************************************
    4/6/1989 Haruhiko Okumura
    Use, distribute, and modify this program freely.
    Please send me your improved versions.
        PC-VAN      SCIENCE
        NIFTY-Serve PAF01022
        CompuServe  74050,1022
**************************************************************/

using System;
using System.Collections.Generic;

namespace KimeraCS.Core
{
    public class LZS
    {
        private static int matchLength;
        private static int matchPosition;
        private static int[] lson = new int[4097];
        private static int[] rson = new int[4353];
        private static int[] dad = new int[4097];
        private static byte[] textBuf = new byte[4113]; // 4096 + 17
        private static List<byte> result = new List<byte>();

        public static byte[] CompressWithHeader(byte[] fileData)
        {
            var compressed = Compress(fileData);
            int lzsSize = compressed.Length;
            byte[] header = BitConverter.GetBytes(lzsSize);
            byte[] withHeader = new byte[4 + compressed.Length];
            Array.Copy(header, 0, withHeader, 0, 4);
            Array.Copy(compressed, 0, withHeader, 4, compressed.Length);
            return withHeader;
        }

        public static byte[] Compress(byte[] fileData)
        {
            result.Clear();
            int dataLength = fileData.Length;
            int sizeAlloc = dataLength / 2;
            result.Capacity = sizeAlloc;

            int i, c, len, r, s, codeBufPtr;
            byte[] codeBuf = new byte[17];
            byte mask = 1;

            for (i = 4097; i <= 4352; ++i) rson[i] = 4096;
            for (i = 0; i < 4096; ++i) dad[i] = 4096;

            codeBuf[0] = 0;
            codeBufPtr = 1;
            s = 0;
            r = 4078;

            Array.Clear(textBuf, 0, r);

            int dataIndex = 0;
            for (len = 0; len < 18 && dataIndex < dataLength; ++len)
                textBuf[r + len] = fileData[dataIndex++];

            if (len == 0)
                return Array.Empty<byte>();

            for (i = 1; i <= 18; ++i)
                InsertNode(r - i);

            InsertNode(r);

            int curResult = 0;
            while (len > 0)
            {
                if (matchLength > len)
                    matchLength = len;

                if (matchLength <= 2)
                {
                    matchLength = 1;
                    codeBuf[0] |= mask;
                    codeBuf[codeBufPtr++] = textBuf[r];
                }
                else
                {
                    codeBuf[codeBufPtr++] = (byte)matchPosition;
                    codeBuf[codeBufPtr++] = (byte)(((matchPosition >> 4) & 0xF0) | (matchLength - 3));
                }

                if ((mask <<= 1) == 0)
                {
                    result.AddRange(codeBuf.AsSpan(0, codeBufPtr).ToArray());
                    codeBuf[0] = 0;
                    codeBufPtr = 1;
                    mask = 1;
                }

                int lastMatchLength = matchLength;
                for (i = 0; i < lastMatchLength && dataIndex < dataLength; ++i)
                {
                    c = fileData[dataIndex++];
                    DeleteNode(s);
                    textBuf[s] = (byte)c;
                    if (s < 17) textBuf[s + 4096] = (byte)c;
                    s = (s + 1) & 4095;
                    r = (r + 1) & 4095;
                    InsertNode(r);
                }

                while (i++ < lastMatchLength)
                {
                    DeleteNode(s);
                    s = (s + 1) & 4095;
                    r = (r + 1) & 4095;
                    if (--len > 0)
                        InsertNode(r);
                }
            }

            if (codeBufPtr > 1)
                result.AddRange(codeBuf.AsSpan(0, codeBufPtr).ToArray());

            return result.ToArray();
        }

        public static byte[] Decompress(byte[] data, int max)
        {
            result.Clear();
            int sizeAlloc = max + 10;
            ushort curBuff = 4078;
            ushort firstByte = 0;

            int index = 0;

            if ((ulong)sizeAlloc > 2000UL * (ulong)data.Length)
            {
                return Array.Empty<byte>();
            }

            Array.Clear(textBuf, 0, 4078);

            while (index < data.Length && result.Count < max)
            {
                if (((firstByte >>= 1) & 256) == 0)
                {
                    if (index >= data.Length) break;
                    firstByte = (ushort)(data[index++] | 0xff00);
                }

                if ((firstByte & 1) == 1)
                {
                    if (index >= data.Length) break;
                    textBuf[curBuff] = data[index++];
                    result.Add(textBuf[curBuff]);
                    curBuff = (ushort)((curBuff + 1) & 4095);
                }
                else
                {
                    if (index + 1 >= data.Length) break;
                    ushort address = data[index++];
                    ushort length = data[index++];
                    address |= (ushort)((length & 0xF0) << 4);
                    length = (ushort)((length & 0x0F) + 2 + address);

                    for (ushort i = address; i <= length; ++i)
                    {
                        textBuf[curBuff] = textBuf[i & 4095];
                        result.Add(textBuf[curBuff]);
                        curBuff = (ushort)((curBuff + 1) & 4095);
                    }
                }
            }

            return result.ToArray();
        }

        public static byte[] DecompressAll(byte[] data)
        {
            return DecompressAll(data, data.Length);
        }

        public static byte[] DecompressAll(byte[] data, int fileSize)
        {
            result.Clear();
            int sizeAlloc = fileSize * 5;
            result.Capacity = sizeAlloc;

            ushort curBuff = 4078;
            ushort firstByte = 0;
            int index = 0;

            Array.Clear(textBuf, 0, 4078);

            while (index < fileSize)
            {
                if (((firstByte >>= 1) & 256) == 0)
                {
                    if (index >= fileSize) break;
                    firstByte = (ushort)(data[index++] | 0xFF00);
                }

                if ((firstByte & 1) == 1)
                {
                    if (index >= fileSize) break;
                    textBuf[curBuff] = data[index++];
                    result.Add(textBuf[curBuff]);
                    curBuff = (ushort)((curBuff + 1) & 4095);
                }
                else
                {
                    if (index + 1 >= fileSize) break;
                    ushort offset = data[index++];
                    ushort length = data[index++];
                    offset |= (ushort)((length & 0xF0) << 4);
                    length = (ushort)((length & 0x0F) + 2 + offset);

                    for (ushort i = offset; i <= length; ++i)
                    {
                        textBuf[curBuff] = textBuf[i & 4095];
                        result.Add(textBuf[curBuff]);
                        curBuff = (ushort)((curBuff + 1) & 4095);
                    }
                }
            }

            return result.ToArray();
        }

        public static byte[] DecompressAllWithHeader(byte[] data)
        {
            if (data.Length < 4)
                return Array.Empty<byte>();

            int lzsSize = BitConverter.ToInt32(data, 0);

            if (lzsSize != data.Length - 4)
                return Array.Empty<byte>();

            byte[] compressedData = new byte[lzsSize];
            Array.Copy(data, 4, compressedData, 0, lzsSize);

            return DecompressAll(compressedData);
        }

        public static void InsertNode(int r)
        {
            int i, p, cmp = 1;
            byte[] key = new byte[18];
            Array.Copy(textBuf, r, key, 0, Math.Min(18, textBuf.Length - r));
            p = 4097 + key[0];

            rson[r] = lson[r] = 4096;
            matchLength = 0;

            while (true)
            {
                if (cmp >= 0)
                {
                    if (rson[p] != 4096)
                    {
                        p = rson[p];
                    }
                    else
                    {
                        rson[p] = r;
                        dad[r] = p;
                        return;
                    }
                }
                else
                {
                    if (lson[p] != 4096)
                    {
                        p = lson[p];
                    }
                    else
                    {
                        lson[p] = r;
                        dad[r] = p;
                        return;
                    }
                }

                for (i = 1; i < 18; i++)
                {
                    cmp = key[i] - textBuf[p + i];
                    if (cmp != 0)
                        break;
                }

                if (i > matchLength)
                {
                    matchPosition = p;
                    if ((matchLength = i) >= 18)
                        break;
                }
            }

            dad[r] = dad[p];
            lson[r] = lson[p];
            rson[r] = rson[p];
            dad[lson[p]] = r;
            dad[rson[p]] = r;

            if (rson[dad[p]] == p)
            {
                rson[dad[p]] = r;
            }
            else
            {
                lson[dad[p]] = r;
            }

            dad[p] = 4096;
        }

        public static void DeleteNode(int p)
        {
            int q;

            if (dad[p] == 4096) return;

            if (rson[p] == 4096)
            {
                q = lson[p];
            }
            else if (lson[p] == 4096)
            {
                q = rson[p];
            }
            else
            {
                q = lson[p];
                while (rson[q] != 4096)
                {
                    q = rson[q];
                }

                rson[dad[q]] = lson[q];
                dad[lson[q]] = dad[q];
                lson[q] = lson[p];
                dad[lson[p]] = q;
                rson[q] = rson[p];
                dad[rson[p]] = q;
            }

            dad[q] = dad[p];

            if (rson[dad[p]] == p)
            {
                rson[dad[p]] = q;
            }
            else
            {
                lson[dad[p]] = q;
            }

            dad[p] = 4096;
        }

        public static void Clear()
        {
            result.Clear();
        }
    }
}
