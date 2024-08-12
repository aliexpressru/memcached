using Aer.Memcached.Samples.Shared;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Aer.Memcached.Samples.Shared.Models;

namespace Aer.Memcached.Samples.WepApiToSync;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers().AddNewtonsoftJson();
        
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddMemcached(builder.Configuration);

        builder.Services.AddSwaggerGen(options =>
        {
            options.MapType<TimeSpan>(() => new OpenApiSchema
            {
                Type = "string",
                Example = new OpenApiString("00:00:00")
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.AddMemcachedSyncEndpoint<string>(builder.Configuration);
            endpoints.AddMemcachedSyncEndpoint<ComplexModel>(builder.Configuration);
            endpoints.AddMemcachedSyncEndpoint<List<string>>(builder.Configuration);
            endpoints.AddMemcachedSyncEndpoint<Dictionary<string, string>>(builder.Configuration);
            endpoints.AddMemcachedSyncEndpoint<Dictionary<ComplexDictionaryKey, ComplexModel>>(builder.Configuration);
            endpoints.AddMemcachedEndpoints(builder.Configuration);
            endpoints.MapControllers();
        });

        app.EnableMemcachedDiagnostics(builder.Configuration);

        app.Run();
    }
}