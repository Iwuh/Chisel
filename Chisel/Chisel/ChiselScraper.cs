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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chisel
{
    public sealed class ChiselScraper : IDisposable
    {
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;

        private HttpClient _client;
        private ConcurrentQueue<Type> _seriesModules;
        private uint _retries;

        private volatile bool _disposed;
        private volatile bool _running;
        private object _runningLock = new object();

        public ChiselScraper(ILoggerFactory loggerFactory = null)
        {
            _seriesModules = new ConcurrentQueue<Type>();
            _client = new HttpClient();

            _loggerFactory = loggerFactory ?? new NullLoggerFactory();
            _logger = _loggerFactory.CreateLogger<ChiselScraper>();

            Timeout = TimeSpan.FromSeconds(100);
            Retries = 3;
        }

        public TimeSpan Timeout
        {
            get => _client.Timeout;
            set
            {
                ThrowIfDisposedOrStarted();
                _client.Timeout = value;
            }
        }

        public uint Retries
        {
            get => _retries;
            set
            {
                ThrowIfDisposedOrStarted();
                _retries = value;
            }
        }

        public void AddSeriesModules(params Type[] modules)
        {
            foreach (var type in modules)
            {
                if (!IsValidChiselModule(type, out string errorMessage))
                {
                    throw new ArgumentException(errorMessage);                    
                }
                _seriesModules.Enqueue(type);
                _logger.LogInformation("Queued module {0} in series", type);
            }
        }

        public async Task StartAsync(IServiceProvider provider, CancellationToken cancellationToken)
        {
            ThrowIfDisposedOrStarted();
            cancellationToken.Register(() => _running = false);

            _logger.LogInformation("Starting to process {0} modules ({1} series, {2} parallel)"); // TODO: Module count
            while (_seriesModules.TryDequeue(out Type type))
            {
                await ExecuteModule(type, provider, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ExecuteModule(Type type, IServiceProvider provider, CancellationToken token)
        {
            var moduleLogger = _loggerFactory.CreateLogger(type);

            moduleLogger.LogInformation("Processing module {0}", type);

            moduleLogger.LogDebug("Attempting to create instance of module {0}", type);
            // Create an instance of the type using its public parameterless constructor.
            var module = Activator.CreateInstance(type) as ChiselModule;

            // Allow the module to set up, scrape its targets, then allow it to clean up.
            moduleLogger.LogDebug("Initializing module {0}", type);
            await module.Init(provider).ConfigureAwait(false);

            try
            {
                DateTime lastRequest;
                foreach (var url in module.Targets.Select(t => $"{module.Settings.BaseUrl}/{t}"))
                {
                    token.ThrowIfCancellationRequested();

                    // Create a new message for this request and update the headers.
                    var request = new HttpRequestMessage(HttpMethod.Get, url).UpdateHeaders(module.Settings.Headers);

                    moduleLogger.LogTrace("Getting target {0}", url);
                    // Send the request and update the time of the last request.
                    var httpResponse = await _client.SendAsync(request, token).ConfigureAwait(false);
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

                await module.AfterSuccess().ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                moduleLogger.LogError(ex, "Module {0} timed out or was cancelled while retrieving a target.", type);
                await module.AfterFailure(ex).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                moduleLogger.LogError(ex, "Module {0} encountered an exception while retrieving a target.", type);
                await module.AfterFailure(ex).ConfigureAwait(false);
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

        public void Dispose()
        {
            ThrowIfDisposedOrStarted();

            _client.Dispose();
            _loggerFactory.Dispose();
            _disposed = true;
        }

        private void SetRunning(bool state)
        {
            lock (_runningLock)
            {
                _running = state;
            }
        }

        private void ThrowIfDisposedOrStarted()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChiselScraper), "Scraper has already been disposed.");
            }
            else
            {
                lock (_runningLock)
                {
                    if (_running)
                    {
                        throw new InvalidOperationException("Cannot perform operation while scraper is running.");
                    }
                }
            }
        }
    }
}
