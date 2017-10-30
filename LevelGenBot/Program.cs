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
		static GenBot bot;

		static void Main(string[] args)
		{
			bot = new GenBot();
			bot.Connected += Connected;
			bot.Disconnected += Disconnected;
			bot.ConnectAndStart().Wait();

			string userInput = "";
			while (userInput != "e")
			{
				userInput = Console.ReadLine();

				if (userInput == "connect")
				{
					if (!bot.isConnected)
						bot.ConnectAndStart().Wait();
				}
			}

			bot.Disconnect().Wait();
			while (bot.isConnected)
				Task.Delay(250);
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
