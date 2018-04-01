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
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Chisel.Entities
{
    public class ChiselModuleSettings
    {
        public ChiselModuleSettings()
        {
            BaseUrl = string.Empty;
            Headers = new Dictionary<string, IEnumerable<string>>();
            MinBackoff = 2.0;
            ExponentialBackoff = true;
            RetryBackoffProvider = null;

            IsAcceptableStatusCode = (_ => true);
        }

        /// <summary>
        /// The base URL that each target should be appended to. Defaults to empty.
        /// </summary>
        public string BaseUrl { get; protected set; }

        /// <summary>
        /// The headers to use when sending requests.
        /// </summary>
        public Dictionary<string, IEnumerable<string>> Headers { get; protected set; }

        /// <summary>
        /// The base backoff time between requests, in seconds. Defaults to 2.0 seconds
        /// </summary>
        public double MinBackoff { get; protected set; }

        /// <summary>
        /// Whether or not the backoff should increase exponentially for every retry. Defaults to true.
        /// </summary>
        public bool ExponentialBackoff { get; protected set; }

        /// <summary>
        /// If set, will be used to calculate the backoff time on a failed request. Takes the number of attempts, the last response (if applicable), and returns the backoff duration in seconds.
        /// </summary>
        public Func<int, ChiselResponse, double> RetryBackoffProvider { get; protected set; }

        /// <summary>
        /// Returns whether the status code returned by a request should trigger a retry.
        /// </summary>
        public Func<HttpStatusCode, bool> IsAcceptableStatusCode { get; protected set; }
    }
}
