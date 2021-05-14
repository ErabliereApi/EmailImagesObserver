using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BlazorApp.Data;
using AzureComputerVision;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNet.OData.Extensions;
using System.Text.Json.Serialization;

namespace BlazorApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                    .AddJsonOptions(o => 
                    { 
                        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve; 
                        o.JsonSerializerOptions.MaxDepth = 64;
                    });
            services.AddOData();
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<ImageInfoService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = "/images",
                FileProvider = new PhysicalFileProvider(Constant.GetBaseDirectory()),
                ServeUnknownFileTypes = false
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");

                endpoints.EnableDependencyInjection();
                endpoints.Select().Expand().Filter().Count().MaxTop(100).OrderBy();
                endpoints.MapControllers();
            });
        }
    }
}
