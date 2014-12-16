using System;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Autofac;
//using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.AspNet.Hosting;
using Autofac;
using Microsoft.Framework.OptionsModel;

namespace DependencyInjectionDemo
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }
    }
}
