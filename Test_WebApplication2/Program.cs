using System.Reflection;
using Wyman.RabbitMQEventBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRabbitMQEventBus(builder.Configuration.GetSection("EventBus"), "lwm_queue2", Assembly.GetExecutingAssembly());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRabbitMQEventBus();

app.UseAuthorization();

app.MapControllers();

app.Run();
