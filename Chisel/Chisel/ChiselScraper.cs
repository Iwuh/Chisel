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
using Chisel.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Chisel
{
    public class ChiselScraper
    {
        private HttpClient _client;
        private List<Type> _seriesModules;

        public ChiselScraper()
        {
            _seriesModules = new List<Type>();
            _client = new HttpClient();
        }

        public void AddSeriesModules(params Type[] modules)
        {
            foreach (var type in modules)
            {
                if (!IsChiselModule(type))
                {
                    throw new ArgumentException($"{type} is not a subclass of {typeof(ChiselModule)}");
                }
                else if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new ArgumentException($"{type} does not have a public parameterless constructor.");
                }
                _seriesModules.Add(type);
            }
        }

        public async Task StartAsync(IServiceProvider provider)
        {
            foreach (var type in _seriesModules)
            {
                // Create an instance of the type using its public parameterless constructor.
                var module = Activator.CreateInstance(type) as ChiselModule;
                // Allow the module to set up, scrape its targets, then allow it to clean up.
                await module.Init(provider).ConfigureAwait(false);
                await ExecuteModule(module).ConfigureAwait(false);
                await module.AfterScraping().ConfigureAwait(false);

                // If the module implements IDisposable, dispose it.
                if (module is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private async Task ExecuteModule(ChiselModule module)
        {
            DateTime lastRequest;
            foreach (var url in module.Targets.Select(t => $"{module.BaseUrl}/{t}"))
            {
                // Create a new message for this request and update the headers.
                var request = new HttpRequestMessage(HttpMethod.Get, url).UpdateHeaders(module.Headers);
                // Send the request and update the time of the last request.
                var httpResponse = await _client.SendAsync(request).ConfigureAwait(false);
                lastRequest = DateTime.Now;

                // Extract the response into a ChiselResponse and pass it to the module's handle method.
                var chiselResponse = await ChiselResponse.FromResponseMessageAsync(httpResponse).ConfigureAwait(false);
                await module.Handle(chiselResponse).ConfigureAwait(false);

                // Wait until the minimum time has passed since the request, if necessary.
                var timeDifference = DateTime.Now - lastRequest;
                if (timeDifference.TotalMilliseconds < module.Backoff)
                {
                    await Task.Delay((int)(module.Backoff - timeDifference.TotalMilliseconds)).ConfigureAwait(false);
                }
            }
        }

        private bool IsChiselModule(Type type) => type.IsSubclassOf(typeof(ChiselModule));
    }
}
