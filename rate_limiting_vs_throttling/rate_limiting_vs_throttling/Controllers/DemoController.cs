using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace rate_limiting_vs_throttling.Controllers
{
    [ApiController]
    [Route("api/demo")]
    public class DemoController : ControllerBase
    {
        private static int _rateLimitedHits;
        private static int _throttledHits;

        // RATE LIMITING DEMO: fixed window of 5 requests / 10 seconds.
        // Exceeding the window returns 429 immediately - no waiting.
        [HttpGet("rate-limited")]
        [EnableRateLimiting("rate-limit-policy")]
        public IActionResult RateLimited()
        {
            var count = Interlocked.Increment(ref _rateLimitedHits);
            return Ok(new
            {
                message = "Request succeeded on the RATE-LIMITED endpoint",
                requestNumber = count,
                timestamp = DateTime.UtcNow
            });
        }

        // THROTTLING DEMO: only 2 requests processed concurrently, rest queue up
        // (up to 20) and wait their turn instead of being rejected.
        [HttpGet("throttled")]
        [EnableRateLimiting("throttle-policy")]
        public async Task<IActionResult> Throttled(CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _throttledHits);
            // Simulate work so the concurrency limit visibly delays requests.
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return Ok(new
            {
                message = "Request completed on the THROTTLED endpoint",
                requestNumber = count,
                timestamp = DateTime.UtcNow
            });
        }

        // Baseline endpoint with no limiting applied, for comparison.
        [HttpGet("unlimited")]
        [DisableRateLimiting]
        public IActionResult Unlimited()
        {
            return Ok(new
            {
                message = "Request succeeded on the UNLIMITED endpoint",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
