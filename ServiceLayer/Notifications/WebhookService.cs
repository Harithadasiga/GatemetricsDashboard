using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GatemetricsDashboard.ServiceLayer.Notifications
{
    public class WebhookService
    {
        private readonly ConcurrentDictionary<string, Uri> _hooks = new();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookService> _logger;

        public WebhookService(IHttpClientFactory httpClientFactory, ILogger<WebhookService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IEnumerable<string> GetAll() => _hooks.Keys.OrderBy(k => k);

        public bool Register(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            return _hooks.TryAdd(url, uri);
        }

        public bool Unregister(string url)
        {
            return _hooks.TryRemove(url, out _);
        }

        public async Task NotifyAsync(object payload, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var hooks = _hooks.Values.ToList();

            foreach (var uri in hooks)
            {
                try
                {
                    // POST JSON; do not throw on non-success (log instead)
                    var resp = await client.PostAsJsonAsync(uri, payload, cancellationToken);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Webhook POST to {Url} returned {StatusCode}", uri, resp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to POST webhook to {Url}", uri);
                }
            }
        }
    }
}   