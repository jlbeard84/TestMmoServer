using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int timeoutTicks = 600000000;
        public PlayerGroup players = new PlayerGroup();
        public JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
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
                        await RunSocket(context, socket);
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

        private async Task RunSocket(HttpContext context, WebSocket webSocket)
        {
            var playerId = await ProcessInitialMessage(webSocket);

            var lastMessageTick = DateTime.UtcNow.Ticks;
            var isConnected = true;
            ulong messageCount = 0;

            await SendGameMessage(
                webSocket,
                "playerid", 
                playerId);

            while (isConnected)
            {
                var result = await ReceiveFullMessage(webSocket, CancellationToken.None);

                messageCount++;

                await ProcessMessage(
                    playerId,
                    result.Item1.MessageType,
                    result.Item2);

                lastMessageTick = DateTime.UtcNow.Ticks;

                if (result.Item1.CloseStatus.HasValue) {
                    isConnected = false;
                }

                Console.WriteLine($"{playerId}:{messageCount}");
            }
            
            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                null,
                CancellationToken.None);
        }

        private async Task ProcessMessage(
            string playerId,
            WebSocketMessageType messageType,
            byte[] bytes)
        {
            if (messageType != WebSocketMessageType.Text || bytes.Length == 0) {
                return;
            }

            var stringMessage = Encoding.UTF8.GetString(bytes);
            var gameMessages = JsonSerializer.Deserialize<GameMessage[]>(bytes, serializerOptions);

            foreach (var gameMessage in gameMessages)
            {
                switch (gameMessage.MessageType) 
                {
                    case "posUpdate":
                        players.UpdatePlayerPosition(playerId, gameMessage.XPos, gameMessage.YPos);
                        break;
                }
            }
        }

        private async Task<string> ProcessInitialMessage(
            WebSocket webSocket) 
        {
            var buffer = new byte[1024 * 4];

            var initialResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), 
                CancellationToken.None);

            var newPlayerId = players.AddNewPlayer();

            return newPlayerId;
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

        private async Task<(WebSocketReceiveResult, byte[])> ReceiveFullMessage(
            WebSocket socket, 
            CancellationToken cancelToken)
        {
            WebSocketReceiveResult response;

            var message = new List<byte>();

            var buffer = new byte[4096];
            do
            {
                response = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken);
                message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
            } while (!response.EndOfMessage);

            return (response, message.ToArray());
        }
    }
}
