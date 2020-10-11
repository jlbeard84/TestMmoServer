using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TestMmoServer
{
    public class Startup
    {
        public PlayerGroup players = new PlayerGroup();
        public JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var websocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),

            };

            app.UseWebSockets(websocketOptions);

            app.Use(async (context, next) => 
            {
                if (context.Request.Path == "/")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        await Echo(context, socket);
                    }
                    else
                    {
                        context.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    await next();
                }
            });
        }

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), 
                CancellationToken.None);

            var newPlayerId = players.AddNewPlayer();

            await SendGameMessage(
                webSocket,
                "playerid", 
                newPlayerId);

            while (!result.CloseStatus.HasValue)
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count), 
                    result.MessageType, 
                    result.EndOfMessage, 
                    CancellationToken.None);

                result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None);
            }

            await webSocket.CloseAsync(
                result.CloseStatus.Value, 
                result.CloseStatusDescription, 
                CancellationToken.None);
        }

        private async Task SendGameMessage(
            WebSocket socket,
            string messageType, 
            string messageData)
        {
            var gameMessage = new GameMessage
            {
                MessageType = messageType,
                MessageData = messageData
            };

            var json = JsonSerializer.Serialize(gameMessage, serializerOptions);

            var buffer = Encoding.Default.GetBytes(json);

            var arraySegment = new ArraySegment<byte>(buffer);

            await socket.SendAsync(
                arraySegment,
                WebSocketMessageType.Text, 
                true,
                CancellationToken.None);
        }
    }
}
