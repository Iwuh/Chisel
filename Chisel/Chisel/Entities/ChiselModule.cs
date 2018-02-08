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
    public abstract class ChiselModule
    {
        public abstract IEnumerable<string> Targets { get; }

        public string BaseUrl { get; protected set; } 
            = string.Empty;

        public Dictionary<string, string> Headers { get; } 
            = new Dictionary<string, string>();

        public int Backoff { get; protected set; } = 1000;

        public virtual Task Init(IServiceProvider provider)
        {
            // Left blank for optional client implementation.
            return Task.CompletedTask;
        }
    }
}
