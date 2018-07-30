namespace Ags.ResourceProxy.Core {

	public partial class ProxyConfig {

		// Uris that are allowed to use the proxy.
		public virtual string[] AllowedReferrers { get; set; }

		// Objects containing proxy configuration for each server URL to be proxied. If URL hitting the proxy contains one of these URLs it will be proxied.
		public virtual ServerUrl[] ServerUrls { get; set; }

		// Time that tokens from ArcGIS server will be cached, this should be <= the timeout parameter on the token received.
		public virtual int TokenCacheMinutes { get; set; }

	}

	public class ServerUrl {
		// Root URL to which the settings apply.
		public virtual string Url { get; set; }

		// Set to utilize the current running app pool identity to make requests to the server.
		public virtual bool UseAppPoolIdentity { get; set; }

		// Server domain.
		public virtual string Domain { get; set; }

		// ArcGIS Server login user-name.
		public virtual string Username { get; set; }

		// ArcGIS Server login password.
		public virtual string Password { get; set; }

		// Application client id - when using Enterprise Portal
		public virtual string ClientId { get; set; }

		// Application client secret - when using Enterprise Portal
		public virtual string ClientSecret { get; set; }

		// ArcGIS Server Oauth2-Endpoint
		public virtual string Oauth2Endpoint { get; set; }

	}

}
