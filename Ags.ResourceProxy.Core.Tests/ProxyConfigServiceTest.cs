using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace Ags.ResourceProxy.Core.Tests {

	[TestClass]
	public class ProxyConfigServiceTests {

		private Mock<IHostingEnvironment> _mockHostingEnv;

		[TestInitialize]
		public void Init() {
			_mockHostingEnv = new Mock<IHostingEnvironment>();
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException), "hostingEnvironment")]
		public void ProxyConfigService_WhenCreatedWithout_HostingEnv_ShouldThrow() {
			var sut = new ProxyConfigService(null, "proxy.config.json");
		}

		[TestMethod]
		public void GetCredentials_When_Config_Is_UseAppPoolIdentity_Should_Return_DefaultNetworkCredentials() {
			//Arrange
			var domain = "TestDomain";
			var sut = GetSut(new ProxyConfig {
				ServerUrls = new[] { new ServerUrl { Url = "http://www.arcgisserver.com/aswa/rest/", UseAppPoolIdentity = true, Domain = domain } }
			});
			//Act
			var result = sut.GetCredentials(sut.Config.ServerUrls[0]);
			//Assert - checking that we don't get the assigned domain from config
			Assert.AreEqual("", result.Domain);
		}

		[TestMethod]
		public void GetCredentials_When_Config_Has_Domain_Should_Return_NetworkCredentials() {
			//Arrange
			var domain = "TestDomain";
			var sut = GetSut(new ProxyConfig {
				ServerUrls = new[] { new ServerUrl { Url = "http://www.arcgisserver.com/aswa/rest/", Domain = domain, Username = "jdoe", Password = "123" } }
			});
			//Act
			var result = sut.GetCredentials(sut.Config.ServerUrls[0]);
			//Assert
			Assert.AreEqual(domain, result.Domain);
			Assert.AreEqual("jdoe", result.UserName);
			Assert.AreEqual("123", result.Password);
		}

		[TestMethod]
		public void GetProxyServerUrlConfig_Should_Return_Object_When_Url_Contains_Substring() {
			//Arrange
			var domain = "TestDomain";
			var sut = GetSut(new ProxyConfig {
				ServerUrls = new[] { new ServerUrl { Url = "http://www.arcgisserver.com/aswa/rest/", Domain = domain } }
			});
			//Act
			var result = sut.GetProxyServerUrlConfig("http://www.arcgisserver.com/aswa/rest/services/service123abc");
			//Assert
			Assert.IsNotNull(result);
			Assert.AreEqual(domain, result.Domain);
		}

		[TestMethod]
		public void GetOAuth2FormData_Should_Create_Required_KeyVals_For_Server_Post() {
			//Arrange
			var sut = GetSut(new ProxyConfig {
				ServerUrls = new[] { new ServerUrl {
					Url = "http://www.arcgisserver.com/aswa/rest/",
					ClientId = "ClientA",
					ClientSecret = "Client_Secret123"
				} }
			});
			var proxyReferrer = "https://www.arcgisserver.com/";
			//Act
			var result = sut.GetOAuth2FormData(sut.Config.ServerUrls[0], proxyReferrer);
			var dict = new Dictionary<string, string>(result);
			//Assert
			Assert.AreEqual(sut.Config.ServerUrls[0].ClientId, dict["client_id"]);
			Assert.AreEqual(sut.Config.ServerUrls[0].ClientSecret, dict["client_secret"]);
			Assert.AreEqual("client_credentials", dict["grant_type"]);
			Assert.AreEqual(proxyReferrer, dict["redirect_uri"]);
			Assert.AreEqual("json", dict["f"]);
		}

		[TestMethod]
		public void GetPortalExchangeTokenFormData_Should_Create_Required_KeyVals_For_Server_Post() {
			//Arrange
			var sut = GetSut(new ProxyConfig {
				ServerUrls = new[] { new ServerUrl {
					Url = "http://www.arcgisserver.com/aswa/rest/",
					ClientId = "ClientA",
					ClientSecret = "Client_Secret123"
				} }
			});
			var proxyReferrer = "https://www.arcgisserver.com/";
			var portalCode = "abc123";
			//Act
			var result = sut.GetPortalExchangeTokenFormData(sut.Config.ServerUrls[0], proxyReferrer, portalCode);
			var dict = new Dictionary<string, string>(result);
			//Assert
			Assert.AreEqual(sut.Config.ServerUrls[0].ClientId, dict["client_id"]);
			Assert.AreEqual(proxyReferrer, dict["redirect_uri"]);
			Assert.AreEqual("authorization_code", dict["grant_type"]);
			Assert.AreEqual(portalCode, dict["code"]);
			Assert.AreEqual("json", dict["f"]);
		}

		[TestMethod]
		public void IsAllowedReferrer_When_Wildcard_Is_Specified_Should_Return_True_For_Any_Url() {
			//Arrange
			var sut = GetSut(new ProxyConfig {
				AllowedReferrers = new string[] { "*" }
			});
			var proxyReferrer = "https://www.arcgisserver.com/";
			//Act
			var result = sut.IsAllowedReferrer(proxyReferrer);
			//Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsAllowedReferrer_When_Url_Is_Specified_Should_Return_True_For_Matching_Url() {
			//Arrange
			var sut = GetSut(new ProxyConfig {
				AllowedReferrers = new string[] { "https://www.arcgisserver.com" }
			});
			var proxyReferrer = "https://www.arcgisserver.com/";
			//Act
			var result = sut.IsAllowedReferrer(proxyReferrer);
			//Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsAllowedReferrer_When_Url_Is_Specified_And_No_Matching_Referrer_Should_Return_False() {
			//Arrange
			var sut = GetSut(new ProxyConfig {
				AllowedReferrers = new string[] { "https://www.google.com" }
			});
			var proxyReferrer = "https://www.arcgisserver.com/";
			//Act
			var result = sut.IsAllowedReferrer(proxyReferrer);
			//Assert
			Assert.IsFalse(result);
		}

		private ProxyConfigService GetSut(ProxyConfig config) {
			var mockProxyConfigService = new Mock<ProxyConfigService>(_mockHostingEnv.Object);
			mockProxyConfigService.SetupGet(x => x.Config).Returns(config);
			return mockProxyConfigService.Object;
		}

	}
}