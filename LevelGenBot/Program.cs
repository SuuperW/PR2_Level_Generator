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

		static async Task Main(string[] args)
		{
			bot = new GenBot();
			bot.Connected += Connected;
			bot.Disconnected += Disconnected;
			await ConnectBot();

			string userInput = "";
			while (userInput != "e")
			{
				userInput = Console.ReadLine();

				if (userInput == "connect")
				{
					if (!bot.IsConnected)
						await ConnectBot();
				}
				else if (userInput == "dc")
				{
					if (bot.IsConnected)
						await bot.Disconnect();
				}
			}

			if (bot.IsConnected)
				await bot.Disconnect();
		}

		static async Task ConnectBot()
		{
			Console.WriteLine("Connecting...");
			bool success = false;
			while (!success)
			{
				try { await bot.ConnectAndStart(); success = true; }
				catch { Console.WriteLine("Connection error. Retrying in 5 seconds..."); await Task.Delay(5000); }
			}
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
