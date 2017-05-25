using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using Common;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Suffuz.Handlers
{
    public class SearchHandler : IDelegatingHandler
    {
        public SearchHandler(string query)
        {
            this.Query = query;
        }

        public string Query { get; private set; }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    var content = Search(GetQuery(request));
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    response.Content = content;
                    return response;
                }
                catch(Exception ex)
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                }
            });
        }

        private string GetQuery(HttpRequestMessage request)
        {
            var uri = AppContext.GetEnvironmentVariable("SearchUri", "http://localhost:8983/solr/gettingstarted/select?") + "wt=json&indent=true&q=";
            var terms = "\"" + Query + "\"";
            uri += terms;
            return uri;
        }

        private StringContent Search(string query)
        {
            /*
            {
                "text": "Summary Text",
                "attachments": [
                    {
                        "fallback": "Required plain-text summary of the attachment.",
                        "color": "#36a64f",
                        "pretext": "Optional text that appears above the attachment block",
                        "author_name": "Bobby Tables",
                        "author_link": "http://flickr.com/bobby/",
                        "author_icon": "http://flickr.com/icons/bobby.jpg",
                        "title": "Slack API Documentation",
                        "title_link": "https://api.slack.com/",
                        "text": "Optional text that appears within the attachment",
                        "fields": [
                            {
                                "title": "Priority",
                                "value": "High",
                                "short": false
                            }
                        ],
                        "image_url": "http://my-website.com/path/to/image.jpg",
                        "thumb_url": "http://example.com/path/to/thumb.png",
                        "footer": "Slack API",
                        "footer_icon": "https://platform.slack-edge.com/img/default_application_icon.png",
                        "ts": 123456789
                    }
                ]
            }
            */

            var request = HttpWebRequest.Create(query);
            string json = null;
            using (var response = request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        json = sr.ReadToEnd();
                    }
                }
            }

            var jObj = JObject.Parse(json);
            var jResponse = jObj.Property("response");
            var jDocs = ((JObject)jResponse.Value).Property("docs").Value as JArray;
            var sb = new StringBuilder();
            var docs = jDocs.Where(j => ((JObject)j).Property("id").Value.ToString().EndsWith(".html")).Cast<JObject>().Take(100);
            sb.Append("{");
            sb.Append(string.Format("\"text\": \"Your search for \'{0}\' returned {1} result{2}\",", Query, docs.Count(), docs.Count() == 1 ? "" : "s"));
            sb.Append("\"attachments\":[");
            var firstTime = true;
            foreach(var jDoc in docs)
            {
                if (!firstTime)
                {
                    sb.Append(",");
                }
                var link = ((JArray)jDoc.Property("resourcename").Value)[0].ToString()
                                        .Replace(AppContext.GetEnvironmentVariable("WebRoot", "C:\\NNHIS_Docs\\_build\\html\\"), 
                                                 AppContext.GetEnvironmentVariable("BaseUri", "http://localhost:9005") + "/");
                var title = ((JArray)jDoc.Property("dc_title").Value)[0].ToString();

                title = title.Replace("'", "\'");
                title = title.Replace("\"", "\\\"");

                sb.Append("{");

                sb.Append("\"fallback\": \"");
                sb.Append(link);
                sb.Append("\",");

                sb.Append("\"color\": \"");
                sb.Append("#b035b0");
                sb.Append("\",");

                sb.Append("\"title\": \"");
                sb.Append(title);
                sb.Append("\",");

                sb.Append("\"text\": \"<");
                sb.Append(link);
                sb.Append(">\"");

                sb.Append("}");
                firstTime = false;
            }
            sb.Append("]");
            sb.Append("}");

            return new StringContent(sb.ToString(), UTF8Encoding.UTF8, "application/json");
        }
    }
}
