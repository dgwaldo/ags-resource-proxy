using Microsoft.AspNetCore.Http;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Ags.ResourceProxy.Core {

	public class ProxyServerMiddleware {
        private const string JsonExpirationKey = "expires_in";
        private const string JsonTokenKey = "access_token";

		private readonly IMemoryCache _cache;
		private readonly IProxyConfigService _proxyConfigService;
		private readonly IProxyService _proxyService;
		private readonly RequestDelegate _next;
		private string _proxyReferrer;

		public ProxyServerMiddleware(
			RequestDelegate next,
			IProxyConfigService proxyConfigService,
			IProxyService proxyService,
			IMemoryCache memoryCache
			) {
			_next = next;
			_proxyConfigService = proxyConfigService ?? throw new ArgumentNullException(nameof(proxyConfigService));
			_proxyService = proxyService ?? throw new ArgumentNullException(nameof(proxyService));
			_cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
		}

		public async Task Invoke(HttpContext context) {
			var endRequest = false;

			if (context.Request.QueryString.HasValue && context.Request.QueryString.ToString().ToLower() == "?ping") {
				await context.Response.WriteAsync(CreatePingResponse());
				return;
			}

			// Note: Referrer is mis-spelled in the HTTP Spec
			_proxyReferrer = context?.Request?.Headers["referer"];
			if (_proxyConfigService.IsAllowedReferrer(_proxyReferrer) == false) {
				CreateErrorResponse(context.Response, $"Referrer {_proxyReferrer} is not allowed.", HttpStatusCode.BadRequest);
				return;
			}
			// Allows request body to be read multiple times, and buffers.
			context.Request.EnableBuffering();

			var proxiedUrl = context.Request.QueryString.ToString().TrimStart('?');

			// Check if proxy URL is in the list of configured URLs.
			var serverUrlConfig = _proxyConfigService.GetProxyServerUrlConfig(proxiedUrl);

			HttpResponseMessage response = null;

			if (serverUrlConfig != null) {
				var isAppLogin = !String.IsNullOrEmpty(serverUrlConfig?.ClientId) && !String.IsNullOrEmpty(serverUrlConfig?.ClientSecret);
				var isUserLogin = !String.IsNullOrEmpty(serverUrlConfig?.Username) && !String.IsNullOrEmpty(serverUrlConfig?.Password);
				var httpClientName = serverUrlConfig?.Url;

				if (isAppLogin) {
					var serverToken = await CacheTryGetServerToken(serverUrlConfig, httpClientName);
					response = await _proxyService.ForwardRequestToServer(context.Request, proxiedUrl, httpClientName, serverToken);
				} else if (isUserLogin) {
					response = await _proxyService.ForwardRequestToServer(context.Request, proxiedUrl, httpClientName);
				}
			} else { // No matching url to proxy, bypass and proxy the request.
				response = await _proxyService.ForwardRequestToServer(context.Request, proxiedUrl, "");
			}

			await CopyProxyHttpResponse(context, response);

			endRequest = true;
			if (!endRequest) {
				await _next(context);
			}

		}

		private async Task<string> CacheTryGetServerToken(ServerUrl su, string clientName, bool killCache = false) {

			var tokenCacheKey = $"token_for_{su.Url}";
			JObject o;

			if (!_cache.TryGetValue(tokenCacheKey, out string serverTokenJson) || killCache) {
				// Key not in cache, so get token.
				serverTokenJson = await GetAppToken(su, clientName);
				o = JObject.Parse(serverTokenJson);
				// Set expiration based on value returned with access token
				_cache.Set(tokenCacheKey, serverTokenJson, TimeSpan.FromSeconds(Convert.ToDouble(o[JsonExpirationKey])));
			} else
			{
				o = JObject.Parse(serverTokenJson);
			}

			return (string)o[JsonTokenKey];
		}

		private async Task<string> GetAppToken(ServerUrl su, string clientName) {

			if (string.IsNullOrEmpty(su.Oauth2Endpoint)) {
				throw new ArgumentNullException("Oauth2Endpoint");
			}

			var formData = _proxyConfigService.GetOAuth2FormData(su, _proxyReferrer);

			var tokenJson = await _proxyService.RequestTokenJson(su.Oauth2Endpoint, formData, clientName);
			return tokenJson;
		}

		private async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage) {
			if (responseMessage == null) {
				throw new ArgumentNullException(nameof(responseMessage));
			}

			var response = context.Response;

			response.StatusCode = (int)responseMessage.StatusCode;
			foreach (var header in responseMessage?.Headers) {
				response.Headers[header.Key] = header.Value.ToArray();
			}

			foreach (var header in responseMessage?.Content?.Headers) {
				response.Headers[header.Key] = header.Value.ToArray();
			}

			// Removes the header so it doesn't expect a chunked response.
			response.Headers.Remove("transfer-encoding");

			using (var responseStream = await responseMessage.Content.ReadAsStreamAsync()) {
				await responseStream.CopyToAsync(response.Body, context.RequestAborted);
			}
		}

		private HttpResponse CreateErrorResponse(HttpResponse httpResponse, string message, HttpStatusCode status) {
			var jsonMsg = JsonConvert.SerializeObject(new { message, status });
			httpResponse.StatusCode = (int)status;
			httpResponse.WriteAsync(jsonMsg);
			return httpResponse;
		}

		private string CreatePingResponse() {
			var pingResponse = new {
				message = "Pong!",
				hasConfig = _proxyConfigService.Config != null,
				referringUrl = _proxyReferrer
			};
			return JsonConvert.SerializeObject(pingResponse);
		}

	}
}
