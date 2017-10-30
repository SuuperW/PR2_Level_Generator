using System;
using System.Threading.Tasks;
using System.Linq;

using Discord.Net;
using Discord.Rest;
using Discord;
using Discord.WebSocket;

namespace LevelGenBot
{
    class Program
    {
		static string botToken;
		static ulong BotID { get => socketClient.CurrentUser.Id; }

		static DiscordRestClient restClient;
		static DiscordSocketClient socketClient;

        static void Main(string[] args)
        {
			botToken = System.IO.File.ReadAllText("token.txt");

			Console.WriteLine("Creating connections...");
			restClient = ConnectRestClient().Result;
			socketClient = ConnectSocketClient().Result;

			socketClient.SetGameAsync("with .NET");		

			socketClient.MessageReceived += SocketClient_MessageReceived;

			Console.WriteLine("Setup complete.");
			string userInput = "";
			while (userInput != "e")
			{
				userInput = Console.ReadLine();
			}

			Console.WriteLine("Logging out...");
			restClient.LogoutAsync().Wait();
			socketClient.SetStatusAsync(UserStatus.Offline).Wait();
			socketClient.StopAsync().Wait();
			socketClient.LogoutAsync().Wait();
  			Console.WriteLine("Logged out.");
      }

		private static Task SocketClient_MessageReceived(SocketMessage arg)
		{
			if (arg.Author.Id != BotID)
			{
				if (arg.MentionedUsers.FirstOrDefault(u => u.Id == BotID) != null)
					arg.Channel.SendMessageAsync("I don't do much yet.");
			}

			return null;
		}

		static async Task<DiscordRestClient> ConnectRestClient()
		{
			DiscordRestClient ret = new DiscordRestClient(new DiscordRestConfig());
			await ret.LoginAsync(TokenType.Bot, botToken);

			return ret;
		}

		static async Task<DiscordSocketClient> ConnectSocketClient()
		{
			DiscordSocketClient client = new DiscordSocketClient();
			await client.LoginAsync(TokenType.Bot, botToken);
			await client.StartAsync();

			return client;
		}
    }
}
