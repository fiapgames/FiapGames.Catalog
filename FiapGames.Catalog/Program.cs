using System.Text.Json.Serialization;
using FiapGames.Catalog.Configuration;
using FiapGames.Catalog.Data;
using FiapGames.Catalog.Services;
using FiapGames.Catalog.Validators;
using FiapGames.Contracts.Requests.User;
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
    busConfigurator.AddConsumers(typeof(Program).Assembly);

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
