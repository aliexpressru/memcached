using Aer.Memcached.Samples.Shared;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Aer.Memcached.Samples.WepApiToSync;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

        builder.Services.AddControllers();
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

        app.AddMemcachedEndpoints(builder.Configuration);
        app.MapControllers();

        app.EnableMemcachedDiagnostics(builder.Configuration);

        app.Run();
    }
}