using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

public enum HttpVerb
{
    GET,
    POST,
    PUT,
    DELETE
}

namespace HttpUtils
{
    public class RestClient
    {
        public string EndPoint { get; set; }
        public HttpVerb Method { get; set; }
        public string ContentType { get; set; }
        public string PostData { get; set; }
        public string userName { get; set; }
        public string password { get; set; }
        public Dictionary<string, string> headerMap { get; set; }


        public RestClient()
        {
            EndPoint = "";
            Method = HttpVerb.GET;
            ContentType = "text/xml";
            PostData = "";
        }
        public RestClient(string endpoint)
        {
            EndPoint = endpoint;
            Method = HttpVerb.GET;
            ContentType = "text/xml";
            PostData = "";
        }
        public RestClient(string endpoint, HttpVerb method)
        {
            EndPoint = endpoint;
            Method = method;
            ContentType = "text/xml";
            PostData = "";
        }

        public RestClient(string endpoint, HttpVerb method, string postData)
        {
            EndPoint = endpoint;
            Method = method;
            ContentType = "text/xml";
            PostData = postData;
        }


        public string MakeRequest()
        {
            return MakeRequest("");
        }

        public string MakeRequest(string parameters)
        {
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name + " - "+ EndPoint + parameters);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(EndPoint + parameters);
            addHeaders(request);
            request.Credentials = GetCredential(EndPoint);
            request.PreAuthenticate = true;

            request.Method = Method.ToString();
            request.ContentLength = 0;
            request.ContentType = ContentType;

            if (!string.IsNullOrEmpty(PostData) && Method == HttpVerb.POST)
            {
                UTF8Encoding encoding = new UTF8Encoding();
                byte[] bytes = Encoding.GetEncoding("UTF-8").GetBytes(PostData);
                request.ContentLength = bytes.Length;

                using (Stream writeStream = request.GetRequestStream())
                {
                    writeStream.Write(bytes, 0, bytes.Length);
                }
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                string responseValue = string.Empty;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string message = String.Format("Request failed. Received HTTP {0}", response.StatusCode);
                    throw new ApplicationException(message);
                }

                // grab the response
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream != null)
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            responseValue = reader.ReadToEnd();
                        }
                }

                return responseValue;
            }
        }

        private void addHeaders(HttpWebRequest request)
        {
            if (headerMap == null)
            {
                return;
            }
            foreach (KeyValuePair<string, string> kvp in headerMap) {
                request.Headers.Add(kvp.Key, kvp.Value);
            }
        }

        private CredentialCache GetCredential(string url)
        {
            CredentialCache credentialCache = new CredentialCache();
            credentialCache.Add(new System.Uri(url), "Basic", new NetworkCredential(userName, password));
            return credentialCache;
        }

    } // class

}
