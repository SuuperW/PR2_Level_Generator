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

		SpecialUsersCollection specialUsers;

		public GenBot()
		{
			JObject json = JObject.Parse(File.ReadAllText("secrets.txt"));
			bot_token = json["bot_token"].ToString();
			pr2_username = json["pr2_username"].ToString();
			pr2_token = json["pr2_token"].ToString();

			specialUsers = new SpecialUsersCollection("special users.txt");

			InitializeBotCommandsList();
		}

		public event Action Connected;
		public event Action Disconnected;
		public async Task ConnectAndStart()
		{
			restClient = await ConnectRestClient();
			socketClient = await ConnectSocketClient();

			socketClient.MessageReceived += SocketClient_MessageReceived;

			await socketClient.SetGameAsync("with RNG");
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
					Console.WriteLine("Received message from " + msg.Author.Username + ": " + msg.Content);
					Task task;

					// Ensure command is in proper format (@bot args)
					string[] words = msg.Content.Split(' ');
					MentionUtils.TryParseUser(words[0], out ulong mentionedID);
					if (mentionedID != MentionUtils.ParseUser(socketClient.CurrentUser.Mention) || words.Length < 2)
						task = SendHelpMessage(msg, null);
					else
					{// It is.
						string[] args = ParseCommand(msg.Content);
						if (botCommands.ContainsKey(args[0]))
							task = botCommands[args[0]](msg, args);
						else
							task = SendHelpMessage(msg, null);
					}

					await task;
					if (task.Exception != null)
						throw task.Exception;
				}
			}
		}
		private string[] ParseCommand(string msg)
		{
			// This method assumes msg has already been verified to be a command.
			// Thus, there will be a > char marking the end of the bot mention.
			int index = msg.IndexOf('>') + 1;
			List<string> list = new List<string>();

			do
			{
				while (msg.Length < index && char.IsWhiteSpace(msg[index]))
					index++;

				int quote = msg.IndexOf('"', index + 1);
				int space = msg.IndexOf(' ', index + 1);
				int newIndex = 0;
				if (quote != -1 && (quote < space || space == -1))
				{
					newIndex = msg.IndexOf('"', quote + 1);
					list.Add(msg.Substring(quote + 1, newIndex - quote - 1));
					newIndex++;
				}
				else
				{
					if (space == -1)
						newIndex = msg.Length;
					else
						newIndex = space;
					list.Add(msg.Substring(index + 1, newIndex - index - 1));
				}

				index = newIndex;
			} while (index != msg.Length);

			return list.ToArray();
		}

		#region "Bot Commands"
		private delegate Task BotCommand(SocketMessage msg, params string[] args);
		private SortedList<string, BotCommand> botCommands;
		private void InitializeBotCommandsList()
		{
			botCommands = new SortedList<string, BotCommand>();
			botCommands.Add("help", SendHelpMessage);
			botCommands.Add("getsettings", SendSettingsListMessage);
			botCommands.Add("generate", GenerateLevel);
			botCommands.Add("add_trusted_user", AddTrustedUser);
			botCommands.Add("remove_trusted_user", RemoveTrustedUser);
		}

		private async Task SendHelpMessage(SocketMessage msg, params string[] args)
		{
			StringBuilder availableCommands = new StringBuilder();
			foreach (KeyValuePair<string, BotCommand> kvp in botCommands)
				availableCommands.Append("\n" + kvp.Key); // \n first compensates for a bug in Discord

			await msg.Author.SendMessageAsync(msg.Author.Mention +
				", to use this bot send a message with the format `@me command [command arguments]`.\n" +
				"If a command or argument contains a space, surround it with quotation marks.\n" +
				"Example: `@·Level Gen Bot generate \"long race\"`\n" +
				"List of available commands: ```" + availableCommands.ToString() + "```");

			Console.WriteLine("Sent help to " + msg.Author.Username + ".");
		}
		private async Task SendSettingsListMessage(SocketMessage msg, params string[] args)
		{
			IEnumerable<string> filesList = Directory.EnumerateFiles("GenSettings", "*", SearchOption.AllDirectories);
			StringBuilder settingsList = new StringBuilder("Here is a list of all available settings:\n```");
			foreach (string file in filesList)
			{
				settingsList.Append(new FileInfo(file).Name + "\n");
			}
			settingsList.Append("```");

			await msg.Author.SendMessageAsync(settingsList.ToString());
			Console.WriteLine("Sent settings list to " + msg.Author.Username + ".");
		}
		private async Task GenerateLevel(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendGenerateHelpMessage(msg);
				return;
			}

			string settingsName = args[1];

			GenerationManager generationManager = new GenerationManager();
			generationManager.username = pr2_username;
			generationManager.login_token = pr2_token;

			if (generationManager.LoadSettings(Path.Combine("GenSettings", args[1])) == null)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", " +
					"`" + args[1] + "` is not a recognized setting.");
				return;
			}

			RestUserMessage generatingMessage = await msg.Channel.SendMessageAsync(msg.Author.Mention +
				", I am generating and uploading your level...");

			generationManager.generator.GenerateMap();
			string response = await generationManager.UploadLevel();

			generatingMessage.DeleteAsync();
			await msg.Channel.SendMessageAsync(msg.Author.Mention +
			  ", I got this message from pr2hub.com:\n`" + response + "`");
			Console.WriteLine("Uploaded: " + response);

		}
		private async Task SendGenerateHelpMessage(SocketMessage msg)
		{
			await msg.Channel.SendMessageAsync(msg.Author.Mention +
				", to generate a level, please use the following format:\n" +
				"```@me generate [name of settings to use]```\n" +
				"To see a list of available settings, say `@me getsettings`.");

			Console.WriteLine("Sent generate help message to " + msg.Author.Username + ".");
		}

		private async Task AddTrustedUser(SocketMessage msg, params string[] args)
		{
			if (msg.Author.Id != specialUsers.Owner)
			{
				await msg.Author.SendMessageAsync("You do not have permission to use this command.");
				return;
			}

			int count = 0;
			foreach (SocketUser user in msg.MentionedUsers)
			{
				if (user.Id != BotID)
				{
					if (specialUsers.AddTrustedUser(user.Id))
						count++;
				}
			}

			await msg.Channel.SendMessageAsync("Added " + count + " user(s) to trusted user list.");
		}
		private async Task RemoveTrustedUser(SocketMessage msg, params string[] args)
		{
			if (msg.Author.Id != specialUsers.Owner)
			{
				await msg.Author.SendMessageAsync("You do not have permission to use this command.");
				return;
			}

			int count = 0;
			foreach (SocketUser user in msg.MentionedUsers)
			{
				if (user.Id != BotID)
				{
					if (specialUsers.RemoveTrustedUser(user.Id))
						count++;
				}
			}

			await msg.Channel.SendMessageAsync("Removed " + count + " user(s) from trusted user list.");
		}

		#endregion

	}
}
