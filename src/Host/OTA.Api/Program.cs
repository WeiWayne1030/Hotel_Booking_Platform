using OTA.Api.Endpoints;
using OTA.Modules.Inventory.Infrastructure;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 註冊各業務模組 (Modular Monolith)
builder.Services.AddInventoryInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapInventoryEndpoints();

app.Run();
