using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ags.ResourceProxy.Core {

	public class ProxyService : IProxyService {
		private const string JsonContentType = "application/json";
		private const string FormContentType = "application/x-www-form-urlencoded";
		private const string OAuthTokenKey = "token";
		private const string OAuthFormatKey = "f";
		private const string OAuthFormatValue = "json";

		private readonly IHttpClientFactory _httpClientFactory;

		public virtual HttpClient HttpClient(string clientName = null) {
			return String.IsNullOrEmpty(clientName) ? _httpClientFactory.CreateClient() : _httpClientFactory.CreateClient(clientName);
		}

		public ProxyService(IHttpClientFactory httpClientFactory) {
			_httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
		}

		/// <summary>
		/// This call converts the portal token into a local server token.
		/// </summary>
		/// <param name="tokenUri"> Endpoint url for token. </param>
		/// <param name="formData"> Post form content. </param>
		/// <param name="clientName"> Named HTTP client to be used for the request. </param>
		/// <returns>JSON representing the token and when it expires. </returns>
		public async Task<string> RequestTokenJson(string tokenUri, List<KeyValuePair<string, string>> formData, string clientName)	{
			var response = await HttpClient(clientName).PostAsync(tokenUri, new FormUrlEncodedContent(formData));
			return await response.Content.ReadAsStringAsync();
		}

		public async Task<HttpResponseMessage> ForwardRequestToServer(HttpRequest request, string url, string clientName, string token = null) {
			var proxyMsg = CreateProxyHttpRequest(request, new Uri(url), token);
			return await HttpClient(clientName).SendAsync(proxyMsg, HttpCompletionOption.ResponseHeadersRead);
		}

		private static HttpRequestMessage CreateProxyHttpRequest(HttpRequest request, Uri uri, string token = null) {
			var requestMessage = new HttpRequestMessage()
			{
				RequestUri = uri,
				Method = new HttpMethod(request.Method)
			};

			// Copy the request headers
			foreach (var header in request.Headers)
			{
				if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
				{
					requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
				}
			}
			requestMessage.Headers.Host = uri.Authority;


			var body = string.Empty;
			using (var reader = new StreamReader(request.Body, Encoding.UTF8))
			{
				body = reader.ReadToEnd();
			}

			var isJson = request.ContentType != null && request.ContentType.Contains(JsonContentType, StringComparison.OrdinalIgnoreCase);
			if (!string.IsNullOrEmpty(token))
			{
				requestMessage.Method = HttpMethod.Post;
				if (isJson)
				{
					JObject bodyContent = JObject.Parse(body);
					bodyContent[OAuthFormatKey] = OAuthFormatValue;
					bodyContent[OAuthTokenKey] = token;
					body = bodyContent.ToString();
				}
				else
				{
					body = string.Concat(body, !string.IsNullOrEmpty(body) ? "&" : string.Empty, $"{OAuthFormatKey}={OAuthFormatValue}&{OAuthTokenKey}={token}");
				}
			}

			if (!string.IsNullOrEmpty(body))
			{
				requestMessage.Content = new StringContent(body, Encoding.UTF8, isJson ? JsonContentType : FormContentType);
			}

			return requestMessage;
		}

	}

}
