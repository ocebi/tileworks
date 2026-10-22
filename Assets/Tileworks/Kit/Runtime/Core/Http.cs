using System.Threading;
using Cysharp.Threading.Tasks;

namespace Tileworks.Kit.Core
{
    public readonly struct HttpResponse
    {
        public readonly long Status;
        public readonly string Text;
        public readonly string Error;

        public HttpResponse(long status, string text, string error)
        {
            Status = status;
            Text = text;
            Error = error;
        }

        public bool Ok => Error == null && Status >= 200 && Status < 300;

        // Worth retrying: no answer at all, a timeout, or a server error. A 4xx answer won't change.
        public bool Transient => !Ok && (Status == 0 || Status == 408 || Status == 429 || Status >= 500);

        public override string ToString() => Ok ? $"HTTP {Status}" : $"HTTP {Status} {Error}";
    }

    public interface IHttpClient
    {
        // Never throws for network or HTTP failures; only cancellation throws.
        UniTask<HttpResponse> GetTextAsync(string url, int timeoutSeconds, CancellationToken ct);
    }
}
