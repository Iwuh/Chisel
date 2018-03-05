#region License
/* Copyright 2018 Matthew Faigan
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Chisel.Entities
{
    public sealed class ChiselResponse
    {
        /// <summary>
        /// The headers of the HTTP response.
        /// </summary>
        public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; private set; }

        /// <summary>
        /// The status code of the HTTP response.
        /// </summary>
        public int StatusCode { get; private set; }

        /// <summary>
        /// The reason phrase that explains the status code. Ex.: a response code of 200 has the reason phrase "OK".
        /// </summary>
        public string ReasonPhrase { get; private set; }

        /// <summary>
        /// The string content of the response body.
        /// </summary>
        public string Content { get; private set; }

        /// <summary>
        /// Attempts to parse the response body as JSON, otherwise returns null.
        /// </summary>
        public JToken Json
        {
            get
            {
                try
                {
                    // Will return either a JObject or JArray depending on the input.
                    return JToken.Parse(Content);
                }
                catch (JsonReaderException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Attempts to parse the response body as HTML, otherwise returns null.
        /// </summary>
        public HtmlDocument Html
        {
            get
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(Content);

                // If a head or body element exists under an html element, the response is considered valid HTML.
                if (doc.DocumentNode.SelectSingleNode("html/head") != null || doc.DocumentNode.SelectSingleNode("/html/body") != null)
                {
                    return doc;
                }
                return null;
            }
        }

        private ChiselResponse(
            IReadOnlyDictionary<string, IEnumerable<string>> headers, 
            string body, 
            (int code, string reason) responseCode)
        {
            Headers = headers;
            Content = body;
            StatusCode = responseCode.code;
            ReasonPhrase = responseCode.reason;
        }

        public override string ToString()
        {
            return $"{StatusCode} {ReasonPhrase}";
        }

        internal static async Task<ChiselResponse> FromResponseMessageAsync(HttpResponseMessage message)
        {
            var headers = message.Headers.ToDictionary(pair => pair.Key, pair => pair.Value);
            var content = await message.Content.ReadAsStringAsync().ConfigureAwait(false);

            return new ChiselResponse(new ReadOnlyDictionary<string, IEnumerable<string>>(headers), 
                content, 
                ((int)message.StatusCode, message.ReasonPhrase));
        }
    }
}
