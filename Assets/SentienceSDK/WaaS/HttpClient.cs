using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Sentience.Authentication;
using Sentience.Config;
using Sentience.Provider;
using Sentience.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace Sentience.WaaS
{
    public class HttpClient : IHttpClient
    {
        private readonly string _url;
        private Dictionary<string, string> _defaultHeaders;
        private JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public HttpClient(string url)
        {
            _url = url;
            this._defaultHeaders = new Dictionary<string, string>();
            _defaultHeaders["Content-Type"] = "application/json";
            _defaultHeaders["Accept"] = "application/json";
            SentienceConfig config = SentienceConfig.GetConfig();
            _defaultHeaders["X-Access-Key"] = config.BuilderAPIKey;
            if (string.IsNullOrWhiteSpace(config.BuilderAPIKey))
            {
                throw SentienceConfig.MissingConfigError("Builder API Key");
            }
        }

        public void AddDefaultHeader(string key, string value)
        {
            this._defaultHeaders[key] = value;
        }

        public (UnityWebRequest, string, string) BuildRequest<T>(string path, T args,
            [CanBeNull] Dictionary<string, string> headers = null, string overrideUrl = null)
        {
            string url = _url + "/" + path;
            if (overrideUrl != null)
            {
                url = overrideUrl.AppendTrailingSlashIfNeeded() + path;
            }

            string requestJson = "";
            if (typeof(T) == typeof(string))
            {
                requestJson = args as string;
            }
            else
            {
                requestJson = JsonConvert.SerializeObject(args, serializerSettings);
            }
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.method = UnityWebRequest.kHttpVerbPOST;
            byte[] requestData = Encoding.UTF8.GetBytes(requestJson);
            request.uploadHandler = new UploadHandlerRaw(requestData);
            request.uploadHandler.contentType = "application/json";

            if (headers == null)
            {
                headers = _defaultHeaders;
            }
            else
            {
                foreach (string key in _defaultHeaders.Keys)
                {
                    if (!headers.ContainsKey(key))
                    {
                        headers[key] = _defaultHeaders[key];
                    }
                }
            }
            
            foreach (string key in headers.Keys)
            {
                request.SetRequestHeader(key, headers[key]);
            }
            
            string method = request.method;
            string headersString = ExtractHeaders(request);
            string curlRequest = $"curl -X {method} '[URL REDACTED]' {headersString} -d '[REQUEST BODY REDACTED]'";

            return (request, curlRequest, url);
        }

        public async Task<T2> SendRequest<T, T2>(string path, T args, [CanBeNull] Dictionary<string, string> headers = null, string overrideUrl = null)
        {
            (UnityWebRequest, string, string) newRequest = BuildRequest(path, args, headers, overrideUrl);
            UnityWebRequest request = newRequest.Item1;
            string curlRequest = newRequest.Item2;
            // Keep the raw request URL out of diagnostics.

            try
            {
                await request.SendWebRequest();
            
                if (request.error != null || request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode > 299)
                {
                    throw new Exception($"Error sending request ({request.responseCode}). Credentials, request and response bodies are redacted. Diagnostic template: {curlRequest}");
                }
                else
                {
                    byte[] results = request.downloadHandler.data;
                    request.Dispose();
                    var responseJson = Encoding.UTF8.GetString(results);
                    try
                    {
                        T2 result = JsonConvert.DeserializeObject<T2>(responseJson);
                        return result;
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Error unmarshalling response ({e.GetType().Name}). Credentials, request and response bodies are redacted. Diagnostic template: {curlRequest}");
                    }
                }
            }
            catch (HttpRequestException e)
            {
                throw new Exception("HTTP Request failed: " + e.GetType().Name + " reason: " + "response body omitted"  + "\nRedacted diagnostic template: " + curlRequest);
            }
            catch (FormatException e)
            {
                throw new Exception("Invalid URL format: " + e.GetType().Name + " reason: " + "response body omitted" + "\nRedacted diagnostic template: " + curlRequest);
            }
            catch (FileLoadException e)
            {
                throw new Exception("File load exception: " + e.GetType().Name + " reason: " + "response body omitted" + "\nRedacted diagnostic template: " + curlRequest);
            }
            catch (Exception e) {
                throw new Exception("An unexpected error occurred: " + e.GetType().Name + " reason: " + "response body omitted" + "\nRedacted diagnostic template: " + curlRequest);
            }
        }

        private string ExtractHeaders(UnityWebRequest request)
        {
            StringBuilder headerBuilder = new StringBuilder();
            foreach (string headerKey in new string[]{"Content-Type", "Accept", "Authorization", "X-Sentience-Tenant", "X-Access-Key"})
            {
                string headerValue = request.GetRequestHeader(headerKey);
                if (headerKey == "Authorization" || headerKey == "X-Access-Key" || headerKey == "X-Sentience-Tenant")
                {
                    headerValue = string.IsNullOrEmpty(headerValue) ? headerValue : "[REDACTED]";
                }
                if (string.IsNullOrEmpty(headerValue))
                {
                    continue;
                }
                headerBuilder.Append($"-H '{headerKey}: {headerValue}' ");
            }
            return headerBuilder.ToString();
        }
    }
}