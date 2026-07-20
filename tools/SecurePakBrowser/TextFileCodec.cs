using System.Text;

namespace KathanaSecurePakBrowser;

internal sealed record TextFileEncoding(Encoding Encoding, byte[] Preamble, string DisplayName)
{
    public byte[] Encode(string text)
    {
        byte[] body = Encoding.GetBytes(text);
        if (Preamble.Length == 0) return body;
        byte[] result = new byte[Preamble.Length + body.Length];
        Preamble.CopyTo(result, 0);
        body.CopyTo(result, Preamble.Length);
        return result;
    }
}

internal static class TextFileCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
    private static readonly byte[] Utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
    private static readonly byte[] Utf16LeBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BeBom = [0xFE, 0xFF];

    public static bool TryDecode(byte[] content, out string text, out TextFileEncoding fileEncoding)
    {
        try
        {
            if (content.AsSpan().StartsWith(Utf8Bom))
            {
                fileEncoding = new TextFileEncoding(new UTF8Encoding(false, true), Utf8Bom, "UTF-8 with BOM");
                text = fileEncoding.Encoding.GetString(content, 3, content.Length - 3);
                return true;
            }
            if (content.AsSpan().StartsWith(Utf32LeBom))
            {
                fileEncoding = new TextFileEncoding(new UTF32Encoding(false, false, true), Utf32LeBom, "UTF-32 LE");
                text = fileEncoding.Encoding.GetString(content, 4, content.Length - 4);
                return true;
            }
            if (content.AsSpan().StartsWith(Utf32BeBom))
            {
                fileEncoding = new TextFileEncoding(new UTF32Encoding(true, false, true), Utf32BeBom, "UTF-32 BE");
                text = fileEncoding.Encoding.GetString(content, 4, content.Length - 4);
                return true;
            }
            if (content.AsSpan().StartsWith(Utf16LeBom))
            {
                fileEncoding = new TextFileEncoding(new UnicodeEncoding(false, false, true), Utf16LeBom, "UTF-16 LE");
                text = fileEncoding.Encoding.GetString(content, 2, content.Length - 2);
                return true;
            }
            if (content.AsSpan().StartsWith(Utf16BeBom))
            {
                fileEncoding = new TextFileEncoding(new UnicodeEncoding(true, false, true), Utf16BeBom, "UTF-16 BE");
                text = fileEncoding.Encoding.GetString(content, 2, content.Length - 2);
                return true;
            }

            fileEncoding = new TextFileEncoding(StrictUtf8, [], "UTF-8");
            text = StrictUtf8.GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            fileEncoding = new TextFileEncoding(StrictUtf8, [], "UTF-8");
            return false;
        }
    }
}
