using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class StringEx
    {
        private static Regex _dtRegex = new Regex(@"(?<Year>\d{4})-(?<Month>\d{2})-(?<Day>\d{2}) +(?<Hour>\d{2}):(?<Minute>\d{2}):(?<Second>\d{2}).?(?<Millisecond>\d{0,3})");
        public static string StripSpecial(this string source, string replacement)
        {
            Regex r = new Regex(@"\W");
            return r.Replace(source, replacement);
        }

        public static string ToArgs(this string[] args)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in args)
            {
                sb.Append(s);
                sb.Append(" ");
            }
            return sb.ToString();
        }

        public static string GetArgValue(this string delimitedString, string name)
        {
            delimitedString = delimitedString.Right('?');
            string[] nvps = delimitedString.Split('&');
            for (int i = 0; i < nvps.Length; i++)
            {
                string[] nvp = nvps[i].Split('=');
                if (nvp[0].Trim().Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return nvp[1];
            }
            return string.Empty;
        }

        public static string GetArgValue(this string[] nvps, string name)
        {
            string nvp = nvps.Where(s => s.Split(':')[0].Replace("-", "").Trim().Equals(name.Trim(), StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (string.IsNullOrEmpty(nvp))
            {
                var val = AppContext.GetEnvironmentVariable<string>(name, null);
                if (val == null) return null;
                else return val.ToString();
            }
            string[] parts = nvp.Split(':');
            if (parts.Length >= 2)
            {
                string val = "";
                for (int i = 1; i < parts.Length; i++)
                {
                    if (i > 1) val += ":";
                    val += parts[i].Trim();
                }
                return val;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string GetArgValue(this string[] nvps, string name, string defaultValue)
        {
            string ret = GetArgValue(nvps, name);
            if (string.IsNullOrEmpty(ret)) return defaultValue;
            else return ret;
        }

        public static bool HasArgValue(this string[] nvps, string name)
        {
            return GetArgValue(nvps, name) != null;
        }

        public static string GetArgName(this string[] nvps, int index)
        {
            return nvps[index].Split(':')[0].Replace("-", "").Trim();
        }

        public static string GetArgValue(this string[] nvps, int index)
        {
            return nvps[index].Split(':')[1].Trim();
        }

        public static string Right(this string searchString, char searchChar)
        {
            int idx = searchString.LastIndexOf(searchChar);
            string retString = searchString;
            if (idx >= 0)
            {
                retString = searchString.Substring(idx + 1, searchString.Length - idx - 1);
            }
            return retString;
        }

        public static string Right(this string searchString, string searchChars)
        {
            int idx = searchString.LastIndexOf(searchChars);
            string retString = searchString;
            if (idx >= 0)
            {
                idx += searchChars.Length - 1;
                retString = searchString.Substring(idx + 1, searchString.Length - idx - 1);
            }
            return retString;
        }

        public static string Right(this string searchString, int length)
        {
            return searchString.Substring(searchString.Length - length);
        }

        public static string Left(this string searchString, string searchChars)
        {
            int idx = searchString.LastIndexOf(searchChars);
            string retString = searchString;
            if (idx >= 0)
            {
                retString = searchString.Substring(0, idx);
            }
            else
            {
                retString = string.Empty;
            }
            return retString;
        }

        public static string Left(this string searchString, int length)
        {
            var s = searchString.Substring(0, Math.Min(length, searchString.Length));
            return s;
        }

        public static System.DateTime FromANSI(this string ansiDateTime)
        {
            Match m = _dtRegex.Match(ansiDateTime);
            if (m.Success)
            {
                return new System.DateTime(int.Parse(m.Groups["Year"].Value),
                    int.Parse(m.Groups["Month"].Value),
                    int.Parse(m.Groups["Day"].Value),
                    int.Parse(m.Groups["Hour"].Value),
                    int.Parse(m.Groups["Minute"].Value),
                    int.Parse(m.Groups["Second"].Value),
                    int.Parse(string.IsNullOrEmpty(m.Groups["Millisecond"].Value) ? "0" : m.Groups["Millisecond"].Value)).ToLocalTime();
            }
            else
            {
                throw new InvalidOperationException("DateTime did not match ANSI format.");
            }
        }

        public static string ToBase16(this string hash)
        {
            return ToBase16(hash, -1, -1);
        }

        public static string ToBase16(this string hash, int maxLength)
        {
            return ToBase16(hash, maxLength, -1);
        }

        public static string ToBase16(this string hash, int maxLength, int dashPosition)
        {
            byte[] base16 = new byte[hash.Length * 8];
            for (int i = 0; i < hash.Length; i++)
            {
                char c = hash[i];
                ToBase16(c).CopyTo(base16, i * 8);
            }
            return ASCIIEncoding.ASCII.GetString(RLE(base16, maxLength, dashPosition));
        }

        private static byte[] RLE(byte[] base16, int maxLength, int dashPosition)
        {
            List<Byte> rle = new List<byte>();
            if (maxLength < 0) maxLength = int.MaxValue;
            byte lastByte = 0;
            int lastCount = 0;
            int charCount = 0;
            for (int i = 0; i < base16.Length; i++)
            {
                if (base16[i] == lastByte)
                {
                    lastCount++;
                }
                else
                {
                    if (lastByte != 0)
                    {
                        if (lastCount > 1)
                        {
                            rle.Add(ASCIIEncoding.ASCII.GetBytes(lastCount.ToString())[0]);
                            charCount++;
                            if (charCount >= maxLength) break;
                            if (dashPosition > 0 && charCount % dashPosition == 0)
                                rle.Add(45);
                        }

                        rle.Add(lastByte);
                        charCount++;
                        if (charCount >= maxLength) break;
                        if (dashPosition > 0 && charCount % dashPosition == 0)
                            rle.Add(45);
                    }
                    lastByte = base16[i];
                    lastCount = 1;
                }
            }

            if (rle.Last() == 45)
                rle.RemoveAt(rle.Count - 1);

            return rle.ToArray();
        }

        private static byte[] ToBase16(char c)
        {
            byte[] base16 = new byte[] { 48, 48, 48, 48, 48, 48, 48, 48 };
            int ptr = 0;
            int rem = c;
            while (rem > 0)
            {
                byte b = (byte)(rem > 15 ? 15 : rem);
                rem -= (char)(b + 1);
                if (b > 9)
                {
                    switch (b)
                    {
                        case 10:
                            {
                                b = ASCIIEncoding.ASCII.GetBytes("A")[0];
                                break;
                            }
                        case 11:
                            {
                                b = ASCIIEncoding.ASCII.GetBytes("B")[0];
                                break;
                            }
                        case 12:
                            {
                                b = ASCIIEncoding.ASCII.GetBytes("C")[0];
                                break;
                            }
                        case 13:
                            {
                                b = ASCIIEncoding.ASCII.GetBytes("D")[0];
                                break;
                            }
                        case 14:
                            {
                                b = ASCIIEncoding.ASCII.GetBytes("E")[0];
                                break;
                            }
                        case 15:
                            {
                                b = ASCIIEncoding.ASCII.GetBytes("F")[0];
                                break;
                            }
                    }
                }
                else
                {
                    b = ASCIIEncoding.ASCII.GetBytes(b.ToString())[0];
                }

                base16[ptr] = b;
                ptr++;
            }
            return base16;
        }

        public static int IndexOfPrevious(this string source, int start, string searchText, params string[] stopWords)
        {
            var searchLength = searchText.Length;
            var s = start - searchText.Length;
            while (s >= 0)
            {
                if (source.Substring(s, searchLength) == searchText)
                {
                    return s;
                }
                else if (stopWords.Any(sw => source.Substring(s, Math.Min(source.Length - s, sw.Length)).Equals(sw, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return -1;
                }
                s--;
            }

            return s;
        }

        public static int CountOfPrevious(this string source, int start, string searchText, params string[] stopWords)
        {
            int index = start;
            int count = 0;
            do
            {

                index = IndexOfPrevious(source, index, searchText, stopWords);
                if (index >= 0)
                    count++;

            } while (index >= 0);
            return count;
        }
    }
}
