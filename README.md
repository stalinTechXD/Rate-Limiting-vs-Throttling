# Rate Limiting vs Throttling Demo

A small ASP.NET Core (.NET 10) Web API that demonstrates the practical difference between **rate limiting** and **throttling** using the built-in `Microsoft.AspNetCore.RateLimiting` middleware.

## Concepts

| | Rate Limiting | Throttling |
|---|---|---|
| Policy used | `AddFixedWindowLimiter` ("rate-limit-policy") | `AddConcurrencyLimiter` ("throttle-policy") |
| Behavior when over capacity | **Rejects** immediately with `429 Too Many Requests` | **Queues** the request and processes it later |
| Queue limit | `0` (no queue) | `20` |
| Limit type | Requests per time window (5 / 10s) | Concurrent requests being processed (2 at a time) |
| Client experience | Fast failure | Higher latency, but success |
| Typical use case | Protect against abuse/DoS, enforce quotas | Smooth out bursts, protect limited downstream resources (DB, external API) |

## Endpoints

| Endpoint | Policy | Description |
|---|---|---|
| `GET /api/demo/rate-limited` | `rate-limit-policy` | Allows 5 requests per 10-second window. The 6th+ request in the same window gets `429` instantly. |
| `GET /api/demo/throttled` | `throttle-policy` | Allows only 2 requests to execute concurrently (each takes ~1s of simulated work). Extra requests queue (up to 20) and wait their turn instead of failing. |
| `GET /api/demo/unlimited` | none (`[DisableRateLimiting]`) | Baseline endpoint with no limiting, for comparison. |

## Try it

1. Run the app (`F5` or `dotnet run`).
2. Open the root URL (`/`) in a browser — this serves `wwwroot/index.html`, a demo page with buttons.
3. Click **"Fire 10 requests at once"** under each endpoint and compare results:
   - **Rate Limited**: you'll see a mix of `200` and `429` responses, all returning almost instantly.
   - **Throttled**: all 10 requests eventually return `200`, but latency increases the further back in the queue a request is.
4. Alternatively, use `rate_limiting_vs_throttling.http` to fire requests manually and inspect responses.

## How it works

### Rate limiting flow (Fixed Window)

Requests beyond the permit limit within the current window are rejected outright — there is no queue, so the caller fails fast.

```mermaid
sequenceDiagram
	participant C as Client
	participant M as RateLimiter Middleware
	participant A as Fixed Window Limiter (5 req / 10s)
	participant E as /api/demo/rate-limited

	C->>M: Request 1..5
	M->>A: Check window quota
	A-->>M: Permit available
	M->>E: Forward request
	E-->>C: 200 OK

	C->>M: Request 6 (same window)
	M->>A: Check window quota
	A-->>M: Quota exceeded, no queue
	M-->>C: 429 Too Many Requests (immediate)

	Note over A: After 10s, window resets and quota refills
```

### Throttling flow (Concurrency Limiter with queue)

Requests beyond the concurrency limit are placed in a queue and processed as soon as a permit frees up, instead of being rejected.

```mermaid
sequenceDiagram
	participant C as Client
	participant M as RateLimiter Middleware
	participant Q as Concurrency Limiter (2 concurrent, queue 20)
	participant E as /api/demo/throttled

	C->>M: Request 1
	M->>Q: Acquire permit
	Q-->>M: Permit granted (1/2 in use)
	M->>E: Process (~1s)

	C->>M: Request 2
	M->>Q: Acquire permit
	Q-->>M: Permit granted (2/2 in use)
	M->>E: Process (~1s)

	C->>M: Request 3
	M->>Q: Acquire permit
	Q-->>M: No permit available, enqueue (queued: 1)
	Note over Q: Waits until a running request finishes

	E-->>C: Request 1 completes -> 200 OK (permit released)
	Q->>M: Dequeue Request 3, grant permit
	M->>E: Process (~1s)
	E-->>C: Request 3 completes -> 200 OK

	Note over Q: Requests never fail (up to queue limit of 20) -\nthey just wait longer
```

### Architecture overview

```mermaid
flowchart LR
	Client[Browser / HTTP client] -->|GET requests| Middleware[UseRateLimiter middleware]

	Middleware --> FixedWindow{Fixed Window Limiter}
	Middleware --> Concurrency{Concurrency Limiter}
	Middleware --> None[No limiter]

	FixedWindow -->|within limit| RL[/api/demo/rate-limited/]
	FixedWindow -->|over limit| Reject429[429 Too Many Requests]

	Concurrency -->|permit acquired| TH[/api/demo/throttled/]
	Concurrency -->|no permit, queue not full| WaitQueue[Queued - waits for permit]
	WaitQueue --> TH
	Concurrency -->|queue full| Reject429b[429 Too Many Requests]

	None --> UL[/api/demo/unlimited/]
```

## Project structure

```
rate_limiting_vs_throttling/
├── Program.cs                       # Rate limiter policy configuration
├── Controllers/
│   ├── DemoController.cs            # rate-limited / throttled / unlimited endpoints
│   └── WeatherForecastController.cs # default template sample
├── wwwroot/
│   └── index.html                   # Interactive demo page
└── rate_limiting_vs_throttling.http # Sample HTTP requests
```

## Key takeaway

- **Rate limiting** = a hard quota over time. Once exceeded, requests are rejected until the window resets. Best for enforcing usage quotas and blocking abuse.
- **Throttling** = a concurrency/flow-control mechanism. Excess requests are delayed (queued), not rejected, smoothing bursts so downstream resources aren't overwhelmed.
