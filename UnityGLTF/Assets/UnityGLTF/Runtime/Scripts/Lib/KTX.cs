using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF
{
  public class KTX
  {

    static byte[] KTX_MAGIC = { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };

    private static Dictionary<TextureFormat, int> _GlInternal = new Dictionary<TextureFormat, int>
    {
      {TextureFormat.RGB24, 0x8051},
      {TextureFormat.RGBA32, 0x8058},
      {TextureFormat.DXT1, 0x83F0},
      {TextureFormat.DXT5, 0x83F3},
      {TextureFormat.PVRTC_RGB2, 0x8C01},
      {TextureFormat.PVRTC_RGBA2, 0x8C03},
      {TextureFormat.PVRTC_RGB4, 0x8C00},
      {TextureFormat.PVRTC_RGBA4, 0x8C02},
      {TextureFormat.ETC_RGB4, 0x8D64},
      {TextureFormat.ETC2_RGBA1, 0x9275},
      {TextureFormat.ASTC_4x4, 0x93B0},
      {TextureFormat.ASTC_5x5, 0x93B2},
      {TextureFormat.ASTC_6x6, 0x93B4},
      {TextureFormat.ASTC_8x8, 0x93B7},
      {TextureFormat.ASTC_10x10, 0x93BB},
    };


    // Get bytes per pixels
    public static float GetBPP(TextureFormat format)
    {

      switch (format)
      {
        case TextureFormat.DXT5:
          return 1f;
        case TextureFormat.PVRTC_RGB2:
        case TextureFormat.PVRTC_RGBA2:
        case TextureFormat.ASTC_8x8:
          return 0.25f;
        default:
          return 0.5f;
      }

    }


    public static int GetBytesBlockSize(TextureFormat fmt)
    {

      switch (fmt)
      {

        // TODO
        // case TextureFormat.ASTC_4x4:
        // case TextureFormat.ASTC_5x5:
        // case TextureFormat.ASTC_6x6:
        // case TextureFormat.ASTC_10x10:

        case TextureFormat.PVRTC_RGBA4:
        case TextureFormat.PVRTC_RGB4:
        case TextureFormat.PVRTC_RGB2:
        case TextureFormat.PVRTC_RGBA2:
          return 32;

        case TextureFormat.ASTC_8x8:
        case TextureFormat.DXT5:
          return 16;

        case TextureFormat.DXT1:
        case TextureFormat.ETC2_RGBA1:
        case TextureFormat.ETC_RGB4:
          return 8;

      default:
          return 8;

    }

  }


  public static Dictionary<TextureFormat, string> Exts = new Dictionary<TextureFormat, string>()
    {
      {TextureFormat.RGB24, ".jpg"},
      {TextureFormat.RGBA32, ".png"},
      {TextureFormat.DXT1, ".dxt"},
      {TextureFormat.DXT5, ".dxt5"},
      {TextureFormat.PVRTC_RGB2, ".pvr2bpp"},
      {TextureFormat.PVRTC_RGBA2, ".pvr2bpp"},
      {TextureFormat.PVRTC_RGB4, ".pvr"},
      {TextureFormat.PVRTC_RGBA4, ".pvr"},
      {TextureFormat.ETC_RGB4, ".etc"},
      {TextureFormat.ETC2_RGBA1, ".etc2"},
      {TextureFormat.ASTC_4x4, ".astc"},
      {TextureFormat.ASTC_5x5, ".astc"},
      {TextureFormat.ASTC_6x6, ".astc"},
      {TextureFormat.ASTC_8x8, ".astc"},
      {TextureFormat.ASTC_10x10, ".astc"}
    };


  public static string GetExt(TextureFormat format)
  {
    string output;
    Exts.TryGetValue(format, out output);
    output += ".ktx";
    return output;
  }


  public static byte[] Encode(Texture2D texture, TextureFormat format)
  {

    byte[] textureData = texture.GetRawTextureData();

    int glInternalFormat;
    _GlInternal.TryGetValue(format, out glInternalFormat);

    // texture info
    int mipcount = texture.mipmapCount;
    int width = texture.width;
    int height = texture.height;
    int tlen = (textureData.Length);

    // setup data
    int blockSize = GetBytesBlockSize(format);
    int int32len = 4;
    int dlen = 64 + tlen + mipcount * 4;
    byte[] data = new byte[dlen];

    int ptr = 0;
    int size = 0;

    // MAGIC
    byte[] value = KTX_MAGIC;
    size = KTX_MAGIC.Length;
    Array.Copy(value, 0, data, ptr, value.Length);
    ptr += size;

    // endianness
    value = BitConverter.GetBytes(0x04030201);
    size = value.Length;
    Array.Copy(value, 0, data, ptr, value.Length);
    ptr += size;

    // glType
    value = new byte[4];
    Array.Copy(value, 0, data, ptr, value.Length);
    ptr += value.Length;

    // glTypeSize
    value = BitConverter.GetBytes((uint)1);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += value.Length;

    // glFormat
    value = new byte[4];
    Array.Copy(value, 0, data, ptr, value.Length);
    ptr += value.Length;

    // glInternalFormat
    value = BitConverter.GetBytes((uint)glInternalFormat);
    Array.Copy(value, 0, data, ptr, value.Length);
    ptr += value.Length;

    // glBaseInternalFormat
    // RGB	0x1907	 
    // RGBA	0x1908
    value = BitConverter.GetBytes((uint)0x1907);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;

    // pixelWidth
    value = BitConverter.GetBytes((uint)width);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;

    // pixelHeight
    value = BitConverter.GetBytes((uint)height);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;

    // depth
    // TODO
    value = new byte[4];
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;


    // numberOfArrayElements
    // TODO
    value = BitConverter.GetBytes((uint)1);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;


    // numberOfFace
    // TODO
    value = BitConverter.GetBytes((uint)1);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;


    // numberOfMipmapLevels
    value = BitConverter.GetBytes((uint)mipcount);
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;


    // bytesOfKeyValueData
    value = new byte[4];
    Array.Copy(value, 0, data, ptr, int32len);
    ptr += int32len;


    // textureData
    int w = width;
    int h = height;
    // int isize = (w * h) / 2;
    int isize = (int)((float)(w * h) * GetBPP(format));
    isize = Mathf.Max(blockSize, isize);

    int texptr = 0;
    for (int i = 0; i < mipcount; i++)
    {

      byte[] bisize = BitConverter.GetBytes((uint)isize);
      // imageSize
      Array.Copy(bisize, 0, data, ptr, int32len);
      ptr += int32len;
      // data
      Array.Copy(textureData, texptr, data, ptr, isize);
      texptr += isize;
      ptr += isize;

      w = w >> 1;
      h = h >> 1;
      isize = (int)((float)(w * h) * GetBPP(format));
      isize = Mathf.Max(blockSize, isize);

    }

    return data;

  }

}

}