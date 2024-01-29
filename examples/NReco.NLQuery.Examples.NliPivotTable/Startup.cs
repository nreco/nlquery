﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NReco.NLQuery.Examples.NliPivotTable {
    public class Startup
    {
        IWebHostEnvironment HostingEnv;

        public Startup(IWebHostEnvironment env)
        {
			HostingEnv = env;
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder => {
                var loggingSection = Configuration.GetSection("Logging");
                loggingBuilder.AddConfiguration(loggingSection);
                loggingBuilder.AddConsole();
            });
			services.AddMemoryCache();

			services.AddMvc(options => {
                options.EnableEndpointRouting = false;
            }).AddNewtonsoftJson(options => {
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
                options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter { CamelCaseText = false });
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
			//app.UseDefaultFiles();
			app.UseStaticFiles();
            app.UseMvc( routes => {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Pivot}/{action=SearchDrivenReportBuilder}/{id?}");
            });
        }
    }
}
