using UdonSharp;
using UnityEngine;

// How much GPU memory a texture takes. The methods are static, so this behaviour
// never needs a GameObject or an inspector reference.
public class TextureMemory : UdonSharpBehaviour
{
    // Counts the GPU copy only. A texture with isReadable true also keeps a copy in
    // system memory, so it costs twice the returned number.
    // Returns 0 for a format BitsPerPixel does not know, and writes an error.
    public static int Bytes(Texture2D texture)
    {
        int bits = BitsPerPixel(texture.format);

        if (bits == 0)
        {
            Debug.LogError("[TextureMemory] Format " + (int)texture.format + " has no entry in the size table.");
            return 0;
        }

        int bytes = texture.width * texture.height * bits / 8;

        if (texture.mipmapCount > 1)
        {
            // A full mipmap chain adds about a third.
            bytes = bytes * 4 / 3;
        }

        return bytes;
    }

    // Returns 0 for a format that is not listed.
    public static int BitsPerPixel(TextureFormat format)
    {
        if (format == TextureFormat.RGB24) return 24;
        if (format == TextureFormat.RGBA32) return 32;
        if (format == TextureFormat.DXT1) return 4;
        if (format == TextureFormat.DXT5) return 8;
        if (format == TextureFormat.ETC_RGB4) return 4;
        if (format == TextureFormat.ETC2_RGB) return 4;
        if (format == TextureFormat.ETC2_RGBA8) return 8;

        return 0;
    }
}
