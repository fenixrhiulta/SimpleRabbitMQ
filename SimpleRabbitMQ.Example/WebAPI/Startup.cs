using SimpleRabbitMQCore;
using Domain.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using WebAPI.Services;
using Worker;

namespace WebAPI
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
            var appSettings = Configuration.Get<AppSettings>();

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebAPI", Version = "v1" });
            });

            var logger = (ILogger)new LoggerConfiguration()
             .MinimumLevel.Debug()
             .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
             .CreateLogger();

            services.AddSingleton(logger);
            services.AddScoped<IOrderService, OrderService>();

            // Instance
            var SimpleRabbitMQ = new SimpleRabbitMQ(appSettings.SimpleRabbitMQSettings, logger);

            if (SimpleRabbitMQ.IsConnected)
            {
                // Definitions
                SimpleRabbitMQ.CreateExchange(appSettings.SimpleRabbitMQSettings.OrderExchange);
                SimpleRabbitMQ.CreateQueue(appSettings.SimpleRabbitMQSettings.OrderCreateQueue);
            }

            // Dependency Injection
            services.AddSingleton<ISimpleRabbitMQ, SimpleRabbitMQ>(sp => SimpleRabbitMQ);

            // Each DTO must have a unique Publisher and Consumer for its own message
            services.AddSingleton<IPublisher<OrderCreateRequest>, Publisher<OrderCreateRequest>>(sp => new Publisher<OrderCreateRequest>(logger, SimpleRabbitMQ, appSettings.SimpleRabbitMQSettings.OrderCreateQueue));
            services.AddSingleton<IConsumer<OrderCreateRequest>, Consumer<OrderCreateRequest>>(sp => new Consumer<OrderCreateRequest>(logger, SimpleRabbitMQ, appSettings.SimpleRabbitMQSettings.OrderCreateQueue));

            services.AddHostedService<OrderWorker>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebAPI v1"));
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}