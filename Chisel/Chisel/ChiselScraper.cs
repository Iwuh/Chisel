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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chisel
{
    public class ChiselScraper
    {
        private ILogger _logger;
        private HttpClient _client;
        private List<Type> _seriesModules;
        private uint _retries;

        public ChiselScraper(ILoggerFactory loggerFactory = null, uint retries = 3)
        {
            _seriesModules = new List<Type>();
            _client = new HttpClient();
            _logger = loggerFactory?.CreateLogger<ChiselScraper>() ?? CreateNullLogger();

            Timeout = TimeSpan.FromSeconds(100);
            _retries = retries;
        }

        public TimeSpan Timeout
        {
            get => _client.Timeout;
            set => _client.Timeout = value;
        }

        public void AddSeriesModules(params Type[] modules)
        {
            foreach (var type in modules)
            {
                if (!IsValidChiselModule(type, out string errorMessage))
                {
                    throw new ArgumentException(errorMessage);                    
                }
                _seriesModules.Add(type);
                _logger.LogInformation("Queued module {0} in series", type);
            }
        }

        public async Task StartAsync(IServiceProvider provider)
        {
            foreach (var type in _seriesModules)
            {
                await ExecuteModule(type, provider).ConfigureAwait(false);
            }
        }

        private async Task ExecuteModule(Type type, IServiceProvider provider)
        {
            _logger.LogDebug("Processing module {0}", type);

            _logger.LogDebug("Attempting to create instance of module {0}", type);
            // Create an instance of the type using its public parameterless constructor.
            var module = Activator.CreateInstance(type) as ChiselModule;

            // Allow the module to set up, scrape its targets, then allow it to clean up.
            _logger.LogDebug("Initializing module {0}", type);
            await module.Init(provider).ConfigureAwait(false);

            try
            {
                DateTime lastRequest;
                foreach (var url in module.Targets.Select(t => $"{module.BaseUrl}/{t}"))
                {
                    // Create a new message for this request and update the headers.
                    var request = new HttpRequestMessage(HttpMethod.Get, url).UpdateHeaders(module.Headers);

                    _logger.LogTrace("Getting target {0}", url);
                    // Send the request and update the time of the last request.
                    var httpResponse = await _client.SendAsync(request).ConfigureAwait(false);
                    lastRequest = DateTime.Now;

                    // Extract the response into a ChiselResponse and pass it to the module's handle method.
                    var chiselResponse = await ChiselResponse.FromResponseMessageAsync(httpResponse).ConfigureAwait(false);
                    await module.Handle(chiselResponse).ConfigureAwait(false);

                    // Wait until the minimum time has passed since the request, if necessary.
                    var timeDifference = DateTime.Now - lastRequest;
                    if (timeDifference.TotalSeconds < module.Settings.MinBackoff)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(module.Settings.MinBackoff) - timeDifference).ConfigureAwait(false);
                    }
                }

                await module.AfterScraping(true).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Module {0} timed out while retrieving a target.", type);
                await HandleFailure(module, ex).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Module {0} encountered an exception while retrieving a target.", type);
                await HandleFailure(module, ex).ConfigureAwait(false);
            }
            finally
            {
                // If the module implements IDisposable, dispose it.
                if (module != null && module is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private async Task HandleFailure(ChiselModule module, Exception ex)
        {
            _logger.LogDebug("Cleaning up module {0}", module.GetType());
            await module.AfterScraping(false, ex).ConfigureAwait(false);
        }

        private bool IsValidChiselModule(Type type, out string error)
        {
            if (!type.IsSubclassOf(typeof(ChiselModule)))
            {
                error = $"{type} is not a subclass of {typeof(ChiselModule)}";
                return false;
            }
            else if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                error = $"{type} does not have a public parameterless constructor.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private ILogger CreateNullLogger()
        {
            using (var factory = new NullLoggerFactory())
            {
                return factory.CreateLogger<ChiselModule>();
            }
        }
    }
}
