﻿/*--------------------------------------------------------*\
|                                                          |
|                          hprose                          |
|                                                          |
| Official WebSite: https://hprose.com                     |
|                                                          |
|  Cluster.cs                                              |
|                                                          |
|  Cluster class for C#.                                   |
|                                                          |
|  LastModified: Feb 1, 2019                               |
|  Author: Ma Bingyao <andot@hprose.com>                   |
|                                                          |
\*________________________________________________________*/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hprose.RPC.Plugins.Cluster {
    public class Cluster {
        public ClusterConfig Config { get; private set; }
        public Cluster(ClusterConfig config) {
            if (config.Retry < 0) config.Retry = 10;
            Config = config;
        }
        public Cluster() : this(FailoverConfig.Instance) { }
        public async Task<Stream> Handler(Stream request, Context context, NextIOHandler next) {
            try {
                var response = await next(request, context);
                Config.OnSuccess?.Invoke(context);
                return response;
            }
            catch (Exception e) {
                Config.OnFailure?.Invoke(context);
                if (Config.OnRetry != null) {
                    dynamic clientContext = context;
                    bool idempotent = context.Contains("Idempotent") ? Config.Idempotent : clientContext.Idempotent;
                    int retry = context.Contains("Retry") ? Config.Retry : clientContext.Retry;
                    if (!context.Contains("Retried")) {
                        clientContext.Retried = 0;
                    }
                    if (idempotent && clientContext.Retried < retry) {
                        var interval = Config.OnRetry(context);
                        if (interval > TimeSpan.Zero) {
#if NET40
                            await TaskEx.Delay(interval);
#else
                            await Task.Delay(interval);
#endif
                        }
                        return await Handler(request, context, next);
                    }
                }
                throw e;
            }
        }
        public static async Task<object> Forking(string name, object[] args, Context context, NextInvokeHandler next) {
            var deferred = new TaskCompletionSource<object>();
            var clientContext = context as ClientContext;
            var uris = clientContext.Client.Uris;
            var n = uris.Count;
            var count = n;
            for (int i = 0; i < n; ++i) {
                var forkingContext = clientContext.Clone() as ClientContext;
                forkingContext.Uri = uris[i];
                var result = next(name, args, forkingContext).ContinueWith((task) => {
                    if (task.Exception != null) {
                        if (Interlocked.Decrement(ref count) == 0) {
                            deferred.TrySetException(task.Exception);
                        }
                    }
                    else {
                        deferred.TrySetResult(task.Result);
                    }
                });
            }
            return await deferred.Task;
        }
        public static async Task<object> Broadcast(string name, object[] args, Context context, NextInvokeHandler next) {
            var clientContext = context as ClientContext;
            var uris = clientContext.Client.Uris;
            var n = uris.Count;
            var results = new Task<object>[n];
            for (int i = 0; i < n; ++i) {
                var forkingContext = clientContext.Clone() as ClientContext;
                forkingContext.Uri = uris[i];
                results[i] = next(name, args, forkingContext);
            }
#if NET40
            return await TaskEx.WhenAll(results);
#else
            return await Task.WhenAll(results);
#endif
        }
    }
}
