using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using PR2_Level_Generator;

using Newtonsoft.Json.Linq;

namespace LevelGenBot
{
	class GenBot
	{
		string bot_token;
		string pr2_username;
		string pr2_token;

		DiscordRestClient restClient;
		DiscordSocketClient socketClient;

		ulong BotID { get => socketClient.CurrentUser.Id; }
		public bool isConnected = false;

		public GenBot()
		{
			JObject json = JObject.Parse(File.ReadAllText("secrets.txt"));
			bot_token = json["bot_token"].ToString();
			pr2_username = json["pr2_username"].ToString();
			pr2_token = json["pr2_token"].ToString();
		}

		public event Action Connected;
		public event Action Disconnected;
		public async Task ConnectAndStart()
		{
			restClient = await ConnectRestClient();
			socketClient = await ConnectSocketClient();

			socketClient.MessageReceived += SocketClient_MessageReceived;

			await socketClient.SetGameAsync("with .NET");
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
			await ret.LoginAsync(TokenType.Bot, bot_token);

			return ret;
		}
		async Task<DiscordSocketClient> ConnectSocketClient()
		{
			DiscordSocketClient client = new DiscordSocketClient();
			client.Ready += () => { isConnected = true; Connected?.Invoke(); return null; };
			await client.LoginAsync(TokenType.Bot, bot_token);
			await client.StartAsync();

			return client;
		}


		private async Task SocketClient_MessageReceived(SocketMessage msg)
		{
			if (msg.Author.Id != BotID)
			{
				if (msg.MentionedUsers.FirstOrDefault(u => u.Id == BotID) != null)
				{
					Console.WriteLine("Received message: " + msg.Content);
					string[] words = msg.Content.Split(' ');
					MentionUtils.TryParseUser(words[0], out ulong mentionedID);
					if (mentionedID != MentionUtils.ParseUser(socketClient.CurrentUser.Mention) || words.Length < 2)
						await SendFormatHelpMessage(msg.Channel, msg.Author);
					else if (words[1].ToLower() == "getsettings")
						await SendSettingsListMessage(msg.Author);
					else if (words[1].ToLower() == "generate")
					{
						if (words.Length < 3)
							await SendFormatHelpMessage(msg.Channel, msg.Author);
						else
						{
							string settingsName = msg.Content.Substring(words[0].Length + words[1].Length + 2);
							string message = GenerateLevel(settingsName);
							await msg.Channel.SendMessageAsync(msg.Author.Mention +
								", I got this message from pr2hub.com:\n`" + message + "`");

						}
					}
					else
						await SendFormatHelpMessage(msg.Channel, msg.Author);
				}
			}
		}
		private async Task SendFormatHelpMessage(ISocketMessageChannel channel, SocketUser user)
		{
			await channel.SendMessageAsync(user.Mention + ", to generate a level, please use the following format:\n" +
				"```@me generate [name of settings to use]```\n" +
				"To see a list of available settings, say `@me getsettings`.");
		}
		private async Task SendSettingsListMessage(SocketUser user)
		{
			IEnumerable<string> filesList = Directory.EnumerateFiles("GenSettings", "*", SearchOption.AllDirectories);
			StringBuilder settingsList = new StringBuilder("Here is a list of all available settings:\n```");
			foreach (string file in filesList)
			{
				settingsList.Append(new FileInfo(file).Name + "\n");
			}
			settingsList.Append("```");

			await user.SendMessageAsync(settingsList.ToString());
		}
		private string GenerateLevel(string settingName)
		{
			GenerationManager generationManager = new GenerationManager();
			generationManager.username = pr2_username;
			generationManager.login_token = pr2_token;

			generationManager.LoadSettings(Path.Combine("GenSettings", settingName));
			generationManager.generator.GenerateMap();
			return generationManager.UploadLevel();
		}
	}
}
