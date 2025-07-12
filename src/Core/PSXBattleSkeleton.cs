using KimeraCS.Core;
using KimeraCS.Extensions;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KimeraCS.PSX
{
    #region Polygons
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PolyCount
    {
        public ushort Size;
        public ushort TexPage;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public short X;
        public short Y;
        public short Z;
        public short W;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Color
    {
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Unused;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PolyIndices
    {
        public ushort A;
        public ushort B;
        public ushort C;
        public ushort D;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ColorTriangle
    {
        public PolyIndices Vertexs;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Color[] Colors;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ColorQuadric
    {
        public PolyIndices Vertexs;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Color[] Colors;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TexTriangle
    {
        PolyIndices Vertex;
        public byte U0, V0;
        public ushort Flags;
        public byte U1, V1;
        public byte U2, V2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TexQuadric
    {
        PolyIndices Vertex;
        public byte U0, V0;
        public ushort Flags;
        public byte U1, V1;
        public byte U2, V2;
        public byte U3, V3;
        public ushort Unused;
    }

    public struct PolyData
    {
        public PolyCount VertexHeader;
        public Vertex[] VertexData;
        public short NumberOfTexPolygons;
        public short TexPolygonFlags;
        public TexTriangle[] TexPolygons;
        public short NumberofTexQuadrics;
        public short TexQuadricsFlags;
        public TexQuadric[] TexQuadrics;
        public short NumberOfColorPolygons;
        public short ColorPolygonFlags;
        public ColorTriangle[] ColorPolygons;
        public short NumberOfColorQuadrics;
        public short ColorQuadricFlags;
        public ColorQuadric[] ColorQuadrics;

        public PolyData(BinaryReader reader)
        {
            // Vertex Data
            VertexHeader = reader.ReadStruct<PolyCount>();
            var numVertices = VertexHeader.Size / Marshal.SizeOf<Vertex>();
            VertexData = new Vertex[numVertices];
            for (int i = 0; i < numVertices; i++)
                VertexData[i] = reader.ReadStruct<Vertex>();

            // Tex Polygons
            NumberOfTexPolygons = reader.ReadInt16();
            TexPolygonFlags = reader.ReadInt16();

            if (NumberOfTexPolygons > 0)
            {
                TexPolygons = new TexTriangle[NumberOfTexPolygons];

                for (int i = 0; i < NumberOfTexPolygons; i++)
                    TexPolygons[i] = reader.ReadStruct<TexTriangle>();
            }

            // Tex Quadrics
            NumberofTexQuadrics = reader.ReadInt16();
            TexQuadricsFlags = reader.ReadInt16();

            if (NumberofTexQuadrics > 0)
            {
                TexQuadrics = new TexQuadric[NumberofTexQuadrics];

                for (int i = 0; i < NumberofTexQuadrics; i++)
                    TexQuadrics[i] = reader.ReadStruct<TexQuadric>();
            }

            // Color Polygons
            NumberOfColorPolygons = reader.ReadInt16();
            ColorPolygonFlags = reader.ReadInt16();

            if (NumberOfColorPolygons > 0)
            {
                ColorPolygons = new ColorTriangle[NumberOfColorPolygons];

                for (int i = 0; i < NumberOfColorPolygons; i++)
                    ColorPolygons[i] = reader.ReadStruct<ColorTriangle>();
            }

            // Color Quadrics
            NumberOfColorQuadrics = reader.ReadInt16();
            ColorQuadricFlags = reader.ReadInt16();

            if (NumberOfColorQuadrics > 0)
            {
                ColorQuadrics = new ColorQuadric[NumberOfColorQuadrics];

                for (int i = 0; i < NumberOfColorQuadrics; i++)
                    ColorQuadrics[i] = reader.ReadStruct<ColorQuadric>();
            }
        }
    }
    #endregion

    #region Bones
    public struct Bone
    {
        public ushort Parent;
        public short Length;
        public uint Offset;
        public PolyData Data;
    }

    public struct BoneData
    {
        public uint NumberOfBones;
        public uint RootBone1;
        public uint RootBone2;
        public Bone[] Bones;

        public BoneData(BinaryReader reader)
        {
            NumberOfBones = reader.ReadUInt32();
            RootBone1 = reader.ReadUInt32();
            RootBone2 = reader.ReadUInt32();

            Bones = new Bone[NumberOfBones];

            // Read Bone Header
            for (int i = 0; i < NumberOfBones; i++)
            {
                Bones[i].Parent = reader.ReadUInt16();
                Bones[i].Length = reader.ReadInt16();
                Bones[i].Offset = reader.ReadUInt32();
            }

            // Read Bone Data
            for (int i = 0; i < NumberOfBones; i++)
            {
                if (Bones[i].Offset > 0) Bones[i].Data = new PolyData(reader);
            }
        }
    }
    #endregion

    #region Animations
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AnimFrameMiniHeader
    {
        public short NumFrames;
        public short BlockSize;
    }

    public struct AnimationFrameData
    {
        public AnimFrameMiniHeader AnimFrameMiniHeader;
        public byte[] FrameData;
        public byte[/* (4 - (AnimFrameMiniHeader.BlockSize + 5) % 4)) % 4 */] Padding;
    }

    public struct AnimationData
    {
        public AnimationFrameData[] Frames;

        public AnimationData(BinaryReader reader, uint[] animPointers, uint nextSectionPtr)
        {
            // TODO: Find the real length
            Frames = new AnimationFrameData[animPointers.Length];

            for (int i = 0; i < Frames.Length; i++)
            {
                Frames[i].AnimFrameMiniHeader = reader.ReadStruct<AnimFrameMiniHeader>();
                Frames[i].FrameData = reader.ReadBytes(Frames[i].AnimFrameMiniHeader.BlockSize);
                var padding = (int)(i + 1 == Frames.Length ? nextSectionPtr - reader.BaseStream.Position : animPointers[i + 1] - reader.BaseStream.Position);
                if (padding > 0) Frames[i].Padding = reader.ReadBytes(padding);
            }
        }
    }
    #endregion

    #region Textures
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TimHeader
    {
        public byte Tag; // Always 0x10
        public byte Version; // Always 0x0
        public ushort Padding;
        public uint Bpp; // 00 = 4-bit, 01 = 8-bit, 10 = 16-bit, 11 = 24-bit, 20 = CLP flag
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ImageHeader
    {
        public uint Length;
        public short X;
        public short Y;
        public short Width;
        public short Height;
    }

    public struct ImageData
    {
        public ImageHeader Header;
        public ushort[/* ImageHeader.Width * ImageHeader.Height */] Data;
    }

    public struct TIMData
    {
        public TimHeader Header;
        public ImageData CLUT;
        public ImageData Image;

        public TIMData(BinaryReader reader)
        {
            int size = 0;
            byte[] buffer;

            Header = reader.ReadStruct<TimHeader>();

            // CLUT
            CLUT.Header = reader.ReadStruct<ImageHeader>();
            size = CLUT.Header.Width * CLUT.Header.Height;
            CLUT.Data = new ushort[size];
            buffer = reader.ReadBytes(size * sizeof(ushort));
            Buffer.BlockCopy(buffer, 0, CLUT.Data, 0, buffer.Length);

            // Image Data
            Image.Header = reader.ReadStruct<ImageHeader>();
            size = size = Image.Header.Width * Image.Header.Height;
            Image.Data = new ushort[size];
            buffer = reader.ReadBytes(size * sizeof(ushort));
            Buffer.BlockCopy(buffer, 0, Image.Data, 0, buffer.Length);
        }
    }
    #endregion

    #region Weapons
    public struct WeaponData
    {
        public Bone[] Bones;

        public WeaponData(BinaryReader reader, uint[] wpnPointers)
        {
            Bones = new Bone[wpnPointers.Length];

            // Read Bone Header
            for (int i = 0; i < Bones.Length; i++)
            {
                reader.ReadBytes(4); // First 4 bytes are always 0x0
                Bones[i].Parent = reader.ReadUInt16();
                Bones[i].Length = reader.ReadInt16();
                Bones[i].Offset = reader.ReadUInt32();
                if (Bones[i].Offset > 0) Bones[i].Data = new PolyData(reader);
            }
        }
    }
    #endregion

    public struct PSXBattleModel
    {
        public BoneData BoneData;
        public AnimationData AnimationData;
        public TIMData TIMData;
        public WeaponData WeaponData;
    }

    public struct PSXBattleData
    {
        public uint NumberOfSections;
        public uint BoneDataPointer;
        public uint ModelSettingsPointer;
        public uint[] AnimationDataPointer;
        public uint TextureSectionPointer;
        public uint[] WeaponDataPointer;
        public PSXBattleModel ModelData;

        public PSXBattleData(string filename)
        {
            var data = File.ReadAllBytes(filename);
            var decompressed = LZS.DecompressAllWithHeader(data);
            var stream = new MemoryStream(decompressed);

            using (var reader = new BinaryReader(stream))
            {
                NumberOfSections = reader.ReadUInt32();

                // First two pointers are always pointing to bone and model setting data
                BoneDataPointer = reader.ReadUInt32();
                ModelSettingsPointer = reader.ReadUInt32();

                // Prepare the animation data array
                AnimationDataPointer = new uint[NumberOfSections];

                // Load number of sections
                var numAnimations = 0;
                for (int i = 0; i < AnimationDataPointer.Length; i++)
                {
                    AnimationDataPointer[i] = reader.ReadUInt32();

                    var currentPosition = reader.BaseStream.Position;
                    reader.BaseStream.Seek(AnimationDataPointer[i], SeekOrigin.Begin);
                    var header = reader.ReadUInt32();
                    reader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);

                    if (header == 0x00000010)
                    {
                        // TIM header section found, break the cycle
                        TextureSectionPointer = AnimationDataPointer[i];
                        break;
                    }
                    else
                        numAnimations++;
                }

                // Resize the animation sections ( remove the last one as it's the Texture Section )
                Array.Resize(ref AnimationDataPointer, numAnimations);

                // Load weapon section pointers
                WeaponDataPointer = new uint[NumberOfSections - AnimationDataPointer.Length - 3];
                for (int i = 0; i < WeaponDataPointer.Length; i++)
                {
                    WeaponDataPointer[i] = reader.ReadUInt32();
                }

                // Load bone data
                ModelData.BoneData = new BoneData(reader);

                // Skip the model setting data ( kind of undocumented, not needed )
                _ = reader.ReadBytes((int)(AnimationDataPointer[0] - ModelSettingsPointer));

                // Load animation data
                ModelData.AnimationData = new AnimationData(reader, AnimationDataPointer, TextureSectionPointer);

                // Load texture data
                ModelData.TIMData = new TIMData(reader);

                // Load weapon data
                ModelData.WeaponData = new WeaponData(reader, WeaponDataPointer);
            }
        }
    }
}
