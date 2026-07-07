using System.Text.Json.Serialization;
using FiapGames.Catalog.Configuration;
using FiapGames.Catalog.Validators;
using FiapGames.Contracts.Requests.User;
using FiapGames.Core.Services;
using FiapGames.Data;
using FiapGames.Services;
using FiapGames.Services.Consumers;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var rabbitMqOptions = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
    ?? new RabbitMqOptions();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();

builder.Services.AddMassTransit(busConfigurator =>
{
    busConfigurator.AddConsumers(typeof(PaymentProcessedConsumer).Assembly);

    busConfigurator.UsingRabbitMq((context, rabbitMqConfigurator) =>
    {
        rabbitMqConfigurator.Host(rabbitMqOptions.Host, rabbitMqOptions.VirtualHost, host =>
        {
            host.Username(rabbitMqOptions.UserName);
            host.Password(rabbitMqOptions.Password);
        });

        rabbitMqConfigurator.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("catalog", false));
    });

    busConfigurator.AddRequestClient<UserLookupRequested>();
});

builder.Services.AddValidatorsFromAssemblyContaining<CreateGameDtoValidator>();

builder.Services.AddControllers(options => options.Filters.Add<FluentValidationActionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    // The frontend (Vite dev server) calls this API directly, cross-origin, in dev.
    // Vite picks the next free port (5173, 5174, ...) when the default is busy, so any
    // localhost port is allowed here instead of a fixed one.
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host is "localhost" or "127.0.0.1")
            .AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(DevCorsPolicy);

app.MapControllers();

app.Run();
