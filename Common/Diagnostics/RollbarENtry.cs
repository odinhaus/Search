using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.Diagnostics
{
    public enum RollbarLevel
    {
        error,
        critical,
        warning,
        info
    }
    public class RollbarEntry
    {
        public const string ROLLBAR_TOKEN = "c241f34f123b4659985bdf217a8c187b";
        public const string ROLLBAR_URI = "http://api.rollbar.com/api/1/item/";
        public RollbarEntry(Exception error, DateTime timestamp, string platform)
        {
            this.access_token = ROLLBAR_TOKEN;
            this.timestamp = (int)(timestamp.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            this.platform = platform;
            this.level = RollbarLevel.error.ToString();
            if (SecurityContext.Current.CurrentPrincipal.Identity.IsAuthenticated)
            {
                this.person = new RollbarUser()
                {
                    id = ((SHSIdentity)SecurityContext.Current.CurrentPrincipal.Identity).Name,
                    email = ((SHSIdentity)SecurityContext.Current.CurrentPrincipal.Identity).Name,
                    username = ((SHSIdentity)SecurityContext.Current.CurrentPrincipal.Identity).Name
                };
            }
            this.title = error.GetType().Name;
            this.data = new RollbarData()
            {
                environment = AppContext.CurrentEnvironment.ToString(),
                body = new RollbarTraceBody()
                {
                    trace = new RollbarTrace()
                    {
                        frames = GetFrames(error),
                        exception = new RollbarError()
                        {
                            Class = error.GetType().Name,
                            message = error.Message,
                            description = error.ToString()
                        }
                    }
                }
            };
        }

        private RollbarFrame[] GetFrames(Exception error)
        {
            var frames = new List<RollbarFrame>();
            var pattern = @"at (?<code>.+)\s+\[.+\] in (?<file>.+):(?<line>.+)";

            using (var rdr = new StringReader(error.StackTrace))
            {
                var line = "";
                do
                {
                    line = rdr.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        line = rdr.ReadToEnd();

                    if (!string.IsNullOrEmpty(line))
                    {
                        var match = Regex.Match(line, pattern);
                        if (match.Success)
                        {
                            frames.Add(new RollbarFrame()
                            {
                                code = match.Groups["code"].Value,
                                filename = match.Groups["file"].Value,
                                lineno = int.Parse(match.Groups["line"].Value)
                            });
                        }
                    }
                }
                while (!string.IsNullOrEmpty(line));
            }

            return frames.ToArray();
        }

        public string access_token { get; set; }
        public string level { get; set; }
        public int timestamp { get; set; }
        public string platform { get; set; }
        public RollbarUser person { get; set; }
        public string title { get; set; }
        public RollbarData data { get; set; }

        public override string ToString()
        {
            return JObject.FromObject(this).ToString();
        }
    }

    public class RollbarData
    {
        public string environment { get; set; }
        public RollbarTraceBody body { get; set; }
    }

    public class RollbarUser
    {
        public string id { get; set; }
        public string username { get; set; }
        public string email { get; set; }
    }

    public class RollbarTraceBody
    {
        public RollbarTrace trace { get; set; }
    }

    public class RollbarTrace
    {
        public RollbarFrame[] frames { get; set; }
        public RollbarError exception { get; set; }
    }

    public class RollbarFrame
    {
        public string filename { get; set; }
        public int lineno { get; set; }
        public int colno { get; set; }
        public string method { get; set; }
        public string code { get; set; }
    }

    public class RollbarError
    {
        [JsonProperty(PropertyName = "class")]
        public string Class { get; set; }
        public string message { get; set; }
        public string description { get; set; }
    }
}
