using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tileworks.Tiles.Levels
{
    // Indented JSON with short scalar arrays kept on one line, so a level diff shows one changed tile per line.
    public static class CanonicalJson
    {
        private static readonly Regex ScalarArray = new Regex(@"\[\s*(-?[\w"".]+(\s*,\s*-?[\w"".]+)*)\s*\]", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string Write(JToken token)
        {
            var text = token.ToString(Formatting.Indented);
            text = ScalarArray.Replace(text, m => "[" + Whitespace.Replace(m.Groups[1].Value, "").Replace(",", ", ") + "]");
            var builder = new StringBuilder(text.Replace("\r\n", "\n"));
            if (builder.Length == 0 || builder[builder.Length - 1] != '\n') builder.Append('\n');
            return builder.ToString();
        }
    }
}
