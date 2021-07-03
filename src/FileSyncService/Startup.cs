using Cosei.Service.Base;
using Cosei.Service.Http;
using FileSyncService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileSyncService
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.Configure<KestrelServerOptions>(options =>
			{
				options.Limits.MaxRequestBodySize = int.MaxValue; // Default value is 30 MB
			});

			var serviceConfig = ConfigurationReader.ReadConfiguration<ServiceConfiguration>();

			if (string.IsNullOrWhiteSpace(serviceConfig?.Secret))
			{
				throw new Exception($"Please set {nameof(ServiceConfiguration.Secret)} in the {nameof(ServiceConfiguration)}!");
			}

			System.IO.Directory.CreateDirectory(serviceConfig.BasePath);

			services.AddSingleton(serviceConfig);

			services.AddScoped<TokenService>();

			services.AddCoseiHttp();
			services.AddSignalR();

			var validationParameters = TokenService.GetValidationParameters(serviceConfig.Secret);

			services.AddAuthentication(x =>
			{
				x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(x =>
			{
				x.RequireHttpsMetadata = false;
				x.TokenValidationParameters = validationParameters;
			});

			services.AddControllers();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseCors(c =>
			{
				c.AllowAnyOrigin();
				c.AllowAnyMethod();
				c.AllowAnyHeader();
			});

			app.UseAuthentication();
			// app.UseHttpsRedirection();
			app.UseRouting();
			app.UseAuthorization();
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
				endpoints.MapHub<CoseiHub>("/cosei");
			});

			app.UseCosei();
		}
	}
}
