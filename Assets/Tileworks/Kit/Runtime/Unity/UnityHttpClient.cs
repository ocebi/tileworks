using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Tileworks.Kit.Unity
{
    public sealed class UnityHttpClient : IHttpClient
    {
        public async UniTask<HttpResponse> GetTextAsync(string url, int timeoutSeconds, CancellationToken ct)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = timeoutSeconds;
            // The browser ignores UnityWebRequest.timeout on some platforms and timers don't run on WebGL, so the deadline is checked per frame.
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            var operation = request.SendWebRequest();
            try
            {
                while (!operation.isDone)
                {
                    if (Time.realtimeSinceStartupAsDouble > deadline)
                    {
                        request.Abort();
                        return new HttpResponse(408, null, "timed out");
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }

            return request.result == UnityWebRequest.Result.Success
                ? new HttpResponse(request.responseCode, request.downloadHandler.text, null)
                : new HttpResponse(request.responseCode, null, request.error ?? request.result.ToString());
        }
    }
}
