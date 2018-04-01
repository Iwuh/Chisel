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
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Chisel.Entities
{
    /// <summary>
    /// Represents a base class for all Chisel modules.
    /// </summary>
    public abstract class ChiselModule
    {
        /// <summary>
        /// The scraping targets that this module will handle.
        /// </summary>
        public abstract IEnumerable<string> Targets { get; }

        /// <summary>
        /// An instance of <see cref="ChiselModuleSettings"/> containing per-module settings.
        /// </summary>
        public ChiselModuleSettings Settings { get; protected set; }

        /// <summary>
        /// Can optionally be overriden to execute code after module creation, before scraping.
        /// </summary>
        /// <param name="provider">An <see cref="IServiceProvider"/> instance that contains services added by the user.</param>
        public virtual Task Init(IServiceProvider provider)
        {
            // Left blank for optional client implementation.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Will be called after each target is retrieved, in order to handle it.
        /// </summary>
        /// <param name="response">A <see cref="ChiselResponse"/> containing the response headers, status code, and body.</param>
        public abstract Task Handle(ChiselResponse response);

        /// <summary>
        /// Can optionally be overriden to execute code after scraping has finished.
        /// </summary>
        /// <param name="success">Whether the module finished scraping succesfully or failed due to an error.</param>
        /// <param name="ex">The exception that caused the module to fail, if there was one.</param>
        public virtual Task AfterScraping(bool success, Exception ex = null)
        {
            // Left blank for optional client implementation.
            return Task.CompletedTask;
        }
    }
}
