[![Build status](https://ci.appveyor.com/api/projects/status/r8sx7x8ox6amw7bm?svg=true)](https://ci.appveyor.com/project/dgwaldo/ags-resource-proxy)

# AGS (ArcGIS Server) .Net Core Resource-Proxy
ArcGIS Server resource proxy for .Net Core. This proxy is like the https://github.com/Esri/resource-proxy but has been updated to work with .Net Core.

## Features:
- Accessing cross domain resources
- Requests that exceed 2048 characters
- Accessing resources secured with Microsoft Integrated Windows Authentication (IWA) 
	- using application pool identity for the hosted resource-proxy.
	- using proxied user credentials
- OAuth 2.0 app logins.
- Memory based cache of tokens. (If your environment is load balanced, this may be an issue).

## Not supported:
 - This proxy does not do rate limiting.
 - This proxy does not let you set an access token in configuration, though the OAuth2 flow in the proxy will get acquire an access token.
 - This proxy does not do any logging.

## Instructions:

Place the proxy config file into the root of your application directory, (location is configurable).

    // Proxy Configuration (proxy.config.json)
    {
	// Allowed referrers must contain an exact URL match use "*" to match any referrer.
	"allowedReferrers": [ "*" ],
	// Set use app pool identity to use the same network credentials as the app process running in IIS
	"useAppPoolIdentity": false,
	// Token cache time given in minutes. Should be = or < timeout returned in tokens.
	"tokenCacheMinutes": 29,
	// Array of root URLS to be proxied
	"serverUrls": [
		// Example using IWA to authenticate with the server
		{
			"url": "https://arcgisserver.yourdomain.com/webapdater/",
			"domain": "yourdomain",
			"username": "username",
			"password": "password"
		},
		// Example using using client and client secret to get OAuth tokens.
		// Note: IWA credentials can also be passed for environments where IT has the token endpoint behind IWA.
		{
			"url": "https://arcgisportal.com/webadapter/",
			"domain": "yourdomain",
			"username": "username",
			"password": "password",
			"clientId": "clientid",
			"clientSecret": "clientsecret",
			"oauth2Endpoint": "https://arcgisserver.com/webadapter/sharing/oauth2/"
		}
	]}

In your .Net Core ASP project locate the startup.cs file. In the ConfigureServices method add the following code.

    
        // This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {
			... Copy below this line ...
			services.AddSingleton<IProxyConfigService, ProxyConfigService>((a) => new ProxyConfigService(a.GetService<IHostingEnvironment>(), "proxy.config.json"));
			services.AddSingleton<IProxyService, ProxyService>();
 
			var serviceProvider = services.BuildServiceProvider();
 
			var agsProxyConfig = serviceProvider.GetService<IProxyConfigService>();
			// Loop through the config and add Named Clients for use with IHttpClientFactory
			agsProxyConfig.Config.ServerUrls.ToList().ForEach(su => {
				services.AddHttpClient(su.Url)
					.ConfigurePrimaryHttpMessageHandler(h => {
						return new HttpClientHandler {
							AllowAutoRedirect = false,
							Credentials = agsProxyConfig.GetCredentials(agsProxyConfig.GetProxyServerUrlConfig((su.Url)))
						};
					});
			});
			... Copy above this line ...
			services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
		}

Note: The location of the config file can be set when the proxy config service is injected. Example: ("/MyFolder/proxy.config.json")

Next add the following to the Configure method.

    public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
		... Copy below this line ... 
		app.UseWhen(context => {
			return context.Request.Path.Value.ToLower().StartsWith(@"/proxy/proxy.ashx", StringComparison.OrdinalIgnoreCase);
			//&& context.User.Identity.IsAuthenticated; // Add this back in to keep unauthenticated users from utilzing the proxy.
		},
			builder =>
				builder.UseEsriProxyServer(
				app.ApplicationServices.GetService<IProxyConfigService>(),
				app.ApplicationServices.GetService<IProxyService>(),
				app.ApplicationServices.GetService<IMemoryCache>())
			);
		... Copy above this line ...
		app.UseMvc();
	}
Note: You can control access to the proxy by removing the comment on the check for authenticated users. 
Also, you can control route used by the proxy by modifying the path within the StartsWith() method. The example above sets it to server from the same location as the old ashx proxy from ESRI.

### Contributions
Feel free to file an issue or open a pull request to extend the functionality of this code.

### License
MIT