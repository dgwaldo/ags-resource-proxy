using System.Collections.Generic;
using System.Net;

namespace Ags.ResourceProxy.Core {
	public interface IProxyConfigService {

		ProxyConfig Config { get; }

		string ConfigPath { get; }

		bool IsAllowedReferrer(string referer);

		List<KeyValuePair<string, string>> GetOAuth2FormData(ServerUrl su, string proxyReferrer);

		List<KeyValuePair<string, string>> GetPortalExchangeTokenFormData(ServerUrl su, string proxyReferrer, string portalCode);

		NetworkCredential GetCredentials(ServerUrl serverUrlConfig);

		ServerUrl GetProxyServerUrlConfig(string queryStringUrl);
	}
}