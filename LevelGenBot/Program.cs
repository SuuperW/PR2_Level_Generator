using System;
using System.Threading;
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
		static GenBot bot;
		static bool end = false;

		static void Main(string[] args)
		{
			MainAsync();

			while (!end) Task.Delay(500).Wait();
		}
		private static async Task MainAsync()
		{
			bot = new GenBot();
			bot.Connected += Connected;
			bot.Disconnected += Disconnected;
			await bot.ConnectAndStart();

			string userInput = "";
			while (userInput != "e")
			{
				userInput = Console.ReadLine();

				if (userInput == "connect")
				{
					if (!bot.IsConnected)
						await bot.ConnectAndStart();
				}
				else if (userInput == "dc")
				{
					if (bot.IsConnected)
					{
						await bot.Disconnect();
						while (bot.IsConnected)
							Task.Delay(250);
						Console.WriteLine("Bot reports offline.");
					}
				}
			}

			await bot.Disconnect();
			while (bot.IsConnected)
				Task.Delay(250);

			end = true;
		}

		static void Connected()
		{
			Console.WriteLine("Connected.");
		}
		static void Disconnected()
		{
			Console.WriteLine("Disconnected.");
		}
	}
}
