using Ags.ResourceProxy.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;

namespace Ags.ResourceProxy.Web
{
    public class Startup {
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {

			services.AddSingleton<IProxyConfigService, ProxyConfigService>((a) => new ProxyConfigService(a.GetService<IHostingEnvironment>(), "proxy.config.json"));
			services.AddSingleton<IProxyService, ProxyService>();

			var serviceProvider = services.BuildServiceProvider();

			var agsProxyConfig = serviceProvider.GetService<IProxyConfigService>();
			agsProxyConfig.Config.ServerUrls.ToList().ForEach(su => {
				services.AddHttpClient(su.Url)
					.ConfigurePrimaryHttpMessageHandler(h => {
						return new HttpClientHandler {
							AllowAutoRedirect = false,
							Credentials = agsProxyConfig.GetCredentials(agsProxyConfig.GetProxyServerUrlConfig((su.Url)))
						};
					});
			});
            services.AddMvc(options => options.EnableEndpointRouting = false).SetCompatibilityVersion(CompatibilityVersion.Latest);
        }

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			} else {
				app.UseExceptionHandler("/Error");
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseCookiePolicy();

			app.UseWhen(context => {
				return context.Request.Path.Value.ToLower().StartsWith(@"/proxy.ashx", StringComparison.OrdinalIgnoreCase);
				//&& context.User.Identity.IsAuthenticated; // Add this back in to keep unauthenticated users from utilzing the proxy.
			},
				builder =>
					builder.UseAgsProxyServer(
					app.ApplicationServices.GetService<IProxyConfigService>(),
					app.ApplicationServices.GetService<IProxyService>(),
					app.ApplicationServices.GetService<IMemoryCache>())
				);

			app.UseMvc();
		}
	}
}
