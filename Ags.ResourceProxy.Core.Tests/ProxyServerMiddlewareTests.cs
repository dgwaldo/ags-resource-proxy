using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ags.ResourceProxy.Core.Tests {

	[TestClass]
	public class ProxyServerMiddlewareTests {

		private Mock<IProxyService> _mockEvilEsriProxyService;
		private Mock<IProxyConfigService> _mockProxyConfigService;
		private Mock<IMemoryCache> _mockMemoryCache;

		[TestInitialize]
		public void Init() {
			_mockEvilEsriProxyService = new Mock<IProxyService>();
			_mockProxyConfigService = new Mock<IProxyConfigService>();
			_mockMemoryCache = new Mock<IMemoryCache>();
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException), "proxyConfigServices")]
		public void ProxyServerMiddleware_WhenCreatedWithout_ProxyConfigService_ShouldThrow() {
			var sut = new ProxyServerMiddleware(
				(innerHttpContext) => Task.FromResult(0),
				null,
				_mockEvilEsriProxyService.Object,
				_mockMemoryCache.Object);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException), "httpClientFactory")]
		public void ProxyServerMiddleware_WhenCreatedWithout_HttpClientFactory_ShouldThrow() {
			var sut = new ProxyServerMiddleware(
				(innerHttpContext) => Task.FromResult(0),
				_mockProxyConfigService.Object,
				null,
				_mockMemoryCache.Object);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException), "memoryCache")]
		public void ProxyServerMiddleware_WhenCreatedWithout_MemoryCache_ShouldThrow() {
			var sut = new ProxyServerMiddleware(
				(innerHttpContext) => Task.FromResult(0),
				_mockProxyConfigService.Object,
				_mockEvilEsriProxyService.Object,
				null);
		}

		[TestMethod]
		public async Task When_Pinged_Should_Return_Pong_Message() {
			//Arrange
			var sut = GetMiddleware();
			var context = GetDefaultContext("?ping");
			//Act
			await sut.Invoke(context);
			//Assert
			Assert.AreEqual(@"{""message"":""Pong!"",""hasConfig"":false,""referringUrl"":null}",
				await GetResponseContent(context.Response));
		}

		[TestMethod]
		public async Task When_Called_Refferrer_Is_Not_Allowed_Should_Return_Error() {
			//Arrange
			_mockProxyConfigService.Setup(x => x.IsAllowedReferrer(It.IsAny<string>())).Returns(false);

			var sut = GetMiddleware();
			var context = GetDefaultContext("");
			context.Request.Headers.Add("referer", "www.nottest.com");
			//Act
			await sut.Invoke(context);
			//Assert
			Assert.AreEqual(@"{""error"":{""code"":400,""message"":""This proxy does not support empty parameters.""}}",
				await GetResponseContent(context.Response));
		}

		[TestMethod]
		public async Task When_ProxiedUrl_Is_Not_In_Config_Should_Forward_Request() {
			//Arrange
			_mockProxyConfigService.Setup(x => x.GetProxyServerUrlConfig(It.IsAny<string>())).Returns(() => null);
			_mockProxyConfigService.Setup(x => x.IsAllowedReferrer(It.IsAny<string>())).Returns(true);
			_mockEvilEsriProxyService.Setup(x => x.ForwardRequestToServer(It.IsAny<HttpRequest>(), It.Is<string>(y => y == "http://www.google.com"), It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync(new HttpResponseMessage { Content = new StringContent("Proxied Response - Token Auth") });

			var sut = GetMiddleware();
			var context = GetDefaultContext("?http://www.google.com");

			//Act
			await sut.Invoke(context);

			//Assert
			Assert.AreEqual("Proxied Response - Token Auth", await GetResponseContent(context.Response));
		}

		[TestMethod]
		public async Task When_ProxiedUrl_Is_In_Config_And_Is_AppLogin_Not_Cached_Should_Get_Token_From_Server() {
			//Arrange
			_mockProxyConfigService.Setup(x => x.GetProxyServerUrlConfig(It.IsAny<string>()))
				.Returns(new ServerUrl {
					ClientId = "Abc123",
					ClientSecret = "e89sac32ar",
					Url = "http://www.arcgisserver.com/aswa/rest/",
					Oauth2Endpoint = "https://arcgisserver.com/aswa/oauth2/"
				});
			_mockProxyConfigService.Setup(x => x.IsAllowedReferrer(It.IsAny<string>())).Returns(true);
			_mockProxyConfigService.SetupGet(x => x.Config.TokenCacheMinutes).Returns(30);
			_mockProxyConfigService.Setup(x => x.GetOAuth2FormData(It.IsAny<ServerUrl>(), It.IsAny<string>()))
				.Returns(new List<KeyValuePair<string, string>>());

			_mockEvilEsriProxyService.Setup(x => x.ForwardRequestToServer(
				It.IsAny<HttpRequest>(),
				It.Is<string>(y => y == "http://www.arcgisserver.com/aswa/rest/service1"),
				It.IsAny<string>(),
				It.IsAny<string>()))
				.ReturnsAsync(new HttpResponseMessage { Content = new StringContent("Proxied Response") });

			object str = "";
			_mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<string>(), out str)).Returns(false);
			_mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

			_mockEvilEsriProxyService.Setup(x => x.RequestTokenJson(
				It.IsAny<string>(),
				It.IsAny<List<KeyValuePair<string, string>>>(),
				It.IsAny<string>()))
				.ReturnsAsync(@"{""access_token"" : ""asd34rf"", ""expires_in"": ""9999999"" }");

			var sut = GetMiddleware();

			//Act
			await sut.Invoke(GetDefaultContext("?http://www.arcgisserver.com/aswa/rest/service1"));

			//Assert
			_mockMemoryCache.Verify(x => x.TryGetValue(It.Is<string>(y => y == "token_for_http://www.arcgisserver.com/aswa/rest/"), out str));
			_mockProxyConfigService.Verify(x => x.GetOAuth2FormData(It.IsAny<ServerUrl>(), It.IsAny<string>()));
			_mockEvilEsriProxyService.Verify(x => x.RequestTokenJson(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<string>()));
		}

		[TestMethod]
		public async Task When_ProxiedUrl_Is_In_Config_And_Is_AppLogin_And_Credentials_Cached_Should_Get_Token_From_Cache() {
			//Arrange
			_mockProxyConfigService.Setup(x => x.GetProxyServerUrlConfig(It.IsAny<string>()))
				.Returns(new ServerUrl {
					ClientId = "Abc123",
					ClientSecret = "e89sac32ar",
					Oauth2Endpoint = "https://arcgisserver.com/aswa/oauth2/",
					Url = "https://arcgisserver.com/aswa/"
				});
			_mockProxyConfigService.Setup(x => x.IsAllowedReferrer(It.IsAny<string>())).Returns(true);

			_mockEvilEsriProxyService.Setup(x => x.ForwardRequestToServer(It.IsAny<HttpRequest>(),
				It.Is<string>(y => y == "https://arcgisserver.com/aswa/rest"),
				It.IsAny<string>(),
				It.IsAny<string>()))
				.ReturnsAsync(new HttpResponseMessage { Content = new StringContent("Proxied Response - App Auth") });

			object str = @"{""access_token"" : ""asd34rf"", ""expires_in"": ""9999999"" }";
			_mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<string>(), out str)).Returns(true);

			var context = GetDefaultContext("?https://arcgisserver.com/aswa/rest");
			var sut = GetMiddleware();

			//Act
			await sut.Invoke(context);

			//Assert
			Assert.AreEqual("Proxied Response - App Auth", await GetResponseContent(context.Response));
			_mockMemoryCache.Verify(x => x.TryGetValue(It.Is<string>(y => y == "token_for_https://arcgisserver.com/aswa/"), out str));
		}

		[TestMethod]
		public async Task When_ProxiedUrl_Is_In_Config_And_Is_UserLogin_Should_Forward_Request_To_Server() {
			//Arrange
			var proxiedUrl = "http://www.arcgisserver.com/webadapter/rest/services/service1";
			_mockProxyConfigService.Setup(x => x.GetProxyServerUrlConfig(It.IsAny<string>()))
				.Returns(new ServerUrl {
					Username = "jdoe@jdoe.com",
					Password = "pword123",
					Url = "http://www.arcgisserver.com/"
				});

			_mockProxyConfigService.Setup(x => x.IsAllowedReferrer(It.IsAny<string>())).Returns(true);

			_mockEvilEsriProxyService.Setup(x => x.ForwardRequestToServer(
				It.IsAny<HttpRequest>(),
				It.Is<string>(y => y == proxiedUrl), // Url being proxied
				It.Is<string>(z => z == "http://www.arcgisserver.com/"), // IHttpFactory - Named Client
				It.IsAny<string>() // token, if using OAuth
				)).ReturnsAsync(new HttpResponseMessage { Content = new StringContent("Proxied Response - User Auth") })
				.Verifiable();

			var context = GetDefaultContext("?" + proxiedUrl);
			var sut = GetMiddleware();

			//Act
			await sut.Invoke(context);

			//Assert
			Assert.AreEqual("Proxied Response - User Auth", await GetResponseContent(context.Response));
			_mockEvilEsriProxyService.Verify();
		}

		private async Task<string> GetResponseContent(HttpResponse response) {
			response.Body.Position = 0;
			using (var responseReader = new StreamReader(response.Body)) {
				return await responseReader.ReadToEndAsync();
			}
		}

		private DefaultHttpContext GetDefaultContext(string queryString) {
			var context = new DefaultHttpContext();
			context.Request.QueryString = new QueryString(queryString);
			context.Response.Body = new MemoryStream();
			return context;
		}

		private ProxyServerMiddleware GetMiddleware(RequestDelegate reqDelegate = null) {
			return new ProxyServerMiddleware(
				reqDelegate,
				_mockProxyConfigService.Object,
				_mockEvilEsriProxyService.Object,
				_mockMemoryCache.Object);
		}

	}
}