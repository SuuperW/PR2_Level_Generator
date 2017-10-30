using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace LevelGenBot
{
	class GenBot
	{
		string botToken;

		DiscordRestClient restClient;
		DiscordSocketClient socketClient;

		ulong BotID { get => socketClient.CurrentUser.Id; }
		public bool isConnected = false;

		public GenBot()
		{
			botToken = System.IO.File.ReadAllText("token.txt");
		}

		public event Action Connected;
		public event Action Disconnected;
		public async Task ConnectAndStart()
		{
			restClient = await ConnectRestClient();
			socketClient = await ConnectSocketClient();

			socketClient.MessageReceived += SocketClient_MessageReceived;

			socketClient.SetGameAsync("with .NET");
		}

		public async Task Disconnect()
		{
			socketClient.Disconnected += (e) =>
			{
				isConnected = false;
				Disconnected?.Invoke();
				return null;
			};
			await socketClient.SetGameAsync("I'm done.");
			await restClient.LogoutAsync();
			await socketClient.SetStatusAsync(UserStatus.Offline);
			await socketClient.StopAsync();
			await socketClient.LogoutAsync();
		}

		async Task<DiscordRestClient> ConnectRestClient()
		{
			DiscordRestClient ret = new DiscordRestClient(new DiscordRestConfig());
			await ret.LoginAsync(TokenType.Bot, botToken);

			return ret;
		}
		async Task<DiscordSocketClient> ConnectSocketClient()
		{
			DiscordSocketClient client = new DiscordSocketClient();
			client.Ready += () => { isConnected = true; Connected?.Invoke(); return null; };
			await client.LoginAsync(TokenType.Bot, botToken);
			await client.StartAsync();

			return client;
		}


		private Task SocketClient_MessageReceived(SocketMessage arg)
		{
			if (arg.Author.Id != BotID)
			{
				if (arg.MentionedUsers.FirstOrDefault(u => u.Id == BotID) != null)
					arg.Channel.SendMessageAsync("I don't do much yet.");
			}

			return null;

		}
	}
}
