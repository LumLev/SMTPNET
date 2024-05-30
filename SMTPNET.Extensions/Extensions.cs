using System.Text;

namespace SMTPNET.Extensions;
public static class MemoryExtensions
{
    /// <summary>
    /// StartWith on StringComparison.InvariantCultureIgnoreCase
    /// </summary>
    /// <param name="basicValue"></param>
    /// <param name="comparingValue"></param>
    /// <returns> boolean from StartWith on StringComparison.InvariantCultureIgnoreCase </returns>
    public static bool StartsAs(this ReadOnlySpan<char> basicValue, ReadOnlySpan<char> comparingValue)
    {
        return basicValue.StartsWith(comparingValue,
            StringComparison.InvariantCultureIgnoreCase);
    }
    public static bool StartsAs(this string basicValue, string comparingValue)
    {
        return basicValue.StartsWith(comparingValue,
            StringComparison.InvariantCultureIgnoreCase);
    }
    /// <returns> boolean from EndsWith on StringComparison.InvariantCultureIgnoreCase </returns>
    public static bool EndsAs(this ReadOnlySpan<char> basicValue, ReadOnlySpan<char> comparingValue)
    {
        return basicValue.EndsWith(comparingValue,
            StringComparison.InvariantCultureIgnoreCase);
    }
    /// <returns> boolean from Equals on StringComparison.InvariantCultureIgnoreCase </returns>
    public static bool EqualsAs(this string basicValue, string comparingValue)
    {
        return basicValue.Equals(comparingValue,
            StringComparison.InvariantCultureIgnoreCase);
    }
    /// <returns> Encoding.UTF8.GetBytes </returns>
    public static ReadOnlySpan<byte> ToUTF8(this string chars)
    {
        return Encoding.UTF8.GetBytes(chars);
    }
    /// <returns> Encoding.ASCII.GetBytes </returns>
    public static ReadOnlySpan<byte> ToASCII(this string chars)
    {
        return Encoding.ASCII.GetBytes(chars);
    }
    /// <returns> boolean from EndsWith on StringComparison.InvariantCultureIgnoreCase </returns>
    public static bool EndsAs(this byte[] bytes, string endswithchars)
    {
        return Encoding.UTF8.GetString(bytes).EndsWith(endswithchars,
            StringComparison.InvariantCultureIgnoreCase);
    }
    /// <returns> boolean from EndsWith on StringComparison.InvariantCultureIgnoreCase </returns>
    public static bool EndsAs(this Span<byte> bytes, string endswithchars)
    {
        return Encoding.UTF8.GetString(bytes).EndsWith(endswithchars,
            StringComparison.InvariantCultureIgnoreCase);
    }
    /// <returns> return the value between the first '<' and the first '>' </returns>
    public static string GetFirstTag(this string chars)
    {
        return chars[(chars.IndexOf('<') + 1)..chars.IndexOf('>')];
    }
    /// <returns> return the value between the first '<' and the first '>' </returns>
    public static ReadOnlySpan<char> GetFirstTag(this ReadOnlySpan<char> chars)
    {
        return chars[(chars.IndexOf('<') + 1)..chars.IndexOf('>')].Trim();
    }
}





