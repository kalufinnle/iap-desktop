﻿//
// Copyright 2019 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.Compute.Net
{
    public static class NetworkStream
    {
        private const int MaxBufferSize = 64 * 1024;

        public static Task RelayToAsync(
            this INetworkStream readStream, 
            INetworkStream writeStream, 
            CancellationToken token)
        {
            return Task.Run(async () =>
            {
                // Use a buffer that is as large as possible, but does not exceed
                // any of the two stream's capabilities.
                int bufferSize = Math.Min(
                    writeStream.MaxWriteSize,
                    Math.Max(
                        MaxBufferSize,
                        readStream.MinReadSize));
                
                var buffer = new byte[bufferSize];

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        Compute.Trace.TraceVerbose($"NetworkStream [{readStream} > {writeStream}]: Reading...");
                        int bytesRead = await readStream.ReadAsync(
                            buffer,
                            0,
                            buffer.Length,
                            token).ConfigureAwait(false);

                        if (bytesRead > 0)
                        {
                            Compute.Trace.TraceVerbose($"NetworkStream [{readStream} > {writeStream}]: Read {bytesRead} bytes");

                            await writeStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                        }
                        else
                        {
                            Compute.Trace.TraceVerbose($"NetworkStream [{readStream} > {writeStream}]: gracefully closed connection");

                            // Propagate.
                            await writeStream.CloseAsync(token).ConfigureAwait(false);

                            break;
                        }
                    }
                    catch (NetworkStreamClosedException)
                    {
                        Compute.Trace.TraceVerbose($"NetworkStream [{readStream} > {writeStream}]: forcefully closed connection");

                        // Propagate.
                        await writeStream.CloseAsync(token).ConfigureAwait(false);

                        break;
                    }
                    catch (Exception)
                    {
                        Compute.Trace.TraceVerbose($"NetworkStream [{readStream} > {writeStream}]: Caught unhandled exception");

                        throw;
                    }
                }
            });
        }
    }
}
