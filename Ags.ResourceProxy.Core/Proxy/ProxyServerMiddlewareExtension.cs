using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;

namespace Ags.ResourceProxy.Core {
	public static class ProxyServerMiddlewareExtension {
		public static IApplicationBuilder UseAgsProxyServer(
			this IApplicationBuilder builder,
			IProxyConfigService proxyConfigService,
			IProxyService proxyService,
			IMemoryCache memoryCache
			) {
			return builder.Use(next => new ProxyServerMiddleware(next, proxyConfigService, proxyService, memoryCache).Invoke);
		}
	}
}
