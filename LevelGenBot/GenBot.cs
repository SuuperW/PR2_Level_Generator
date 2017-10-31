using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Net;

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
		const string settingsPath = "GenSettings";

		int tempFileID = 0;

		DiscordRestClient restClient;
		DiscordSocketClient socketClient;

		ulong BotID { get => socketClient.CurrentUser.Id; }
		public bool IsConnected { get => socketClient.ConnectionState >= ConnectionState.Connected; }

		SpecialUsersCollection specialUsers;
		CommandHistory commandHistory = new CommandHistory();

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
			await restClient.LogoutAsync();
			await socketClient.SetStatusAsync(UserStatus.Invisible);
			await socketClient.StopAsync();
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
			client.Ready += () => { Connected?.Invoke(); return null; };
			client.Disconnected += (e) => { Disconnected?.Invoke(); return null; };

			await client.LoginAsync(TokenType.Bot, bot_token);
			await client.StartAsync();

			return client;
		}


		private async Task SocketClient_MessageReceived(SocketMessage msg)
		{
			try
			{
				if (msg.Author.Id != BotID)
					await HandleMessage(msg);
			}
			catch (Exception ex)
			{
				Console.WriteLine("\nError:\n");
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
			}
		}
		private async Task HandleMessage(SocketMessage msg)
		{
			if (specialUsers.IsUserBanned(msg.Author.Id))
			{
				if (commandHistory.TimeSinceLastCommand(msg.Author.Id) < 30)
					await bannedCommand.Delegate(msg, null);
				return;
			}

			try
			{
				BotCommand command = MessageToCommand(msg, out string[] args);
				if (command != null)
				{
					command = commandHistory.CommandOrWait(command, msg.Author.Id);

					if (await command.Delegate(msg, args))
						commandHistory.AddCommand(command.Name, msg.Author.Id);
				}
			}
			catch (Exception ex)
			{
				//System.Diagnostics.Debugger.Break();
				await msg.Channel.SendMessageAsync(msg.Author.Mention +
					", I have encountered an error and don't know what to do with it. :(" +
					"Error details have been sent to my owner.");
				await socketClient.GetUser(specialUsers.Owner).SendMessageAsync("Error!  " +
					"`" + ex.Message + "`\n```" + ex.StackTrace + "```");

				throw ex;
			}
		}
		private BotCommand MessageToCommand(SocketMessage msg, out string[] args)
		{
			BotCommand command = null;
			args = null;

			if (IsMessageProperCommand(msg))
			{
				Console.WriteLine("Received command from " + msg.Author.Username + ": " + msg.Content);
				args = ParseCommand(msg.Content);
				string commandStr = args[0].ToLower();

				if (everybodyBotCommands.ContainsKey(commandStr))
					command = everybodyBotCommands[commandStr];
				else if (specialUsers.IsUserTrusted(msg.Author.Id) && trustedBotCommands.ContainsKey(commandStr))
					command = trustedBotCommands[commandStr];
				else if (specialUsers.Owner == msg.Author.Id && ownerBotCommands.ContainsKey(commandStr))
					command = ownerBotCommands[commandStr];
				else
					command = everybodyBotCommands["help"];
			}
			// If the bot is mentioned, send a help message.
			else if (msg.MentionedUsers.FirstOrDefault((u) => u.Id == socketClient.CurrentUser.Id) != null)
				command = everybodyBotCommands["help"];

			return command;
		}
		private bool IsMessageProperCommand(SocketMessage msg)
		{
			bool msgIsCommand = false;
			if (msg.Channel is IDMChannel)
				msgIsCommand = true;
			else
			{
				// Is format @me command
				string[] words = msg.Content.Split(' ');
				MentionUtils.TryParseUser(words[0], out ulong mentionedID);
				msgIsCommand = (mentionedID == MentionUtils.ParseUser(socketClient.CurrentUser.Mention)
					&& words.Length > 1);
			}

			return msgIsCommand;
		}
		private string[] ParseCommand(string msg)
		{
			int index = 0;
			if (msg.IndexOf('<') == 0) // If this command was initated with a mention at the front of it.
				index = msg.IndexOf('>') + 1;
			List<string> list = new List<string>();

			do
			{
				while (msg.Length > index && char.IsWhiteSpace(msg[index]))
					index++;

				int quote = msg.IndexOf('"', index);
				int space = index;
				while (msg.Length > space && !char.IsWhiteSpace(msg[index]))
					space++;

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
					list.Add(msg.Substring(index, newIndex - index));
				}

				index = newIndex;
			} while (index != msg.Length);

			return list.ToArray();
		}

		#region "Bot Commands"
		private SortedList<string, BotCommand> everybodyBotCommands;
		private SortedList<string, BotCommand> trustedBotCommands;
		private SortedList<string, BotCommand> ownerBotCommands;
		private BotCommand bannedCommand;
		private void InitializeBotCommandsList()
		{
			everybodyBotCommands = new SortedList<string, BotCommand>();
			everybodyBotCommands.Add("help", new BotCommand(SendHelpMessage));
			everybodyBotCommands.Add("get_list", new BotCommand(SendSettingsListMessage));
			everybodyBotCommands.Add("generate", new BotCommand(GenerateLevel, 30, 5));
			everybodyBotCommands.Add("get_settings", new BotCommand(GetSettings, 5));

			trustedBotCommands = new SortedList<string, BotCommand>();
			trustedBotCommands.Add("set_settings", new BotCommand(SetSettings, 5));

			ownerBotCommands = new SortedList<string, BotCommand>();
			ownerBotCommands.Add("add_trusted_user", new BotCommand(AddTrustedUser));
			ownerBotCommands.Add("remove_trusted_user", new BotCommand(RemoveTrustedUser));
			ownerBotCommands.Add("gtfo", new BotCommand(GTFO));

			bannedCommand = new BotCommand(SendBannedMessage);
		}

		private async Task<bool> SendHelpMessage(SocketMessage msg, params string[] args)
		{
			StringBuilder availableCommands = new StringBuilder();
			foreach (KeyValuePair<string, BotCommand> kvp in everybodyBotCommands)
				availableCommands.Append("\n" + kvp.Key); // \n first because first line tells Discord how to format
			if (specialUsers.IsUserTrusted(msg.Author.Id))
			{
				foreach (KeyValuePair<string, BotCommand> kvp in trustedBotCommands)
					availableCommands.Append("\n" + kvp.Key);
			}
			if (specialUsers.Owner == msg.Author.Id)
			{
				foreach (KeyValuePair<string, BotCommand> kvp in ownerBotCommands)
					availableCommands.Append("\n" + kvp.Key);
			}

			await msg.Author.SendMessageAsync("To use this bot send a message with the format " +
			  "`@me command [command arguments]` where `@me` is replaced with a mention of this bot.\n" +
			  "If a command or argument contains a space, surround it with quotation marks.\n\n" +
			  "Example command: `@me generate \"long race\"`\n\n" +
			  "If you are sending the command via DMs, mentioning me is _not required_.\n" +
			  "When a level is generated, it will be saved under the PR2 username 'R Races'. " +
			  "Most likely it will also be password-proteced with a blank password; to get in, just press 'Check' without typing in anything.\n\n" +
			  "List of available commands: ```" + availableCommands.ToString() + "```");

			Console.WriteLine("Sent help to " + msg.Author.Username + "#" + msg.Author.Discriminator + ".");
			return true;
		}
		private async Task<bool> SendSettingsListMessage(SocketMessage msg, params string[] args)
		{
			IEnumerable<string> filesList = Directory.EnumerateFiles(settingsPath, "*", SearchOption.AllDirectories);
			StringBuilder settingsList = new StringBuilder("Here is a list of all available settings:\n```");
			foreach (string file in filesList)
				settingsList.Append(new FileInfo(file).Name + "\n");
			settingsList.Append("```");

			await msg.Author.SendMessageAsync(settingsList.ToString());
			Console.WriteLine("Sent settings list to " + msg.Author.Username + "#" + msg.Author.Discriminator + ".");
			return true;
		}
		private async Task<bool> GenerateLevel(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendGenerateHelpMessage(msg);
				return false;
			}

			string settingsName = args[1];

			GenerationManager generationManager = new GenerationManager();
			generationManager.username = pr2_username;
			generationManager.login_token = pr2_token;

			if (generationManager.LoadSettings(Path.Combine(settingsPath, args[1])) == null)
			{
				await SendInvalidSettingMesage(msg, args[1]);
				return false;
			}

			RestUserMessage generatingMessage = await msg.Channel.SendMessageAsync(msg.Author.Mention +
			  ", I am generating and uploading your level...");

			MapLE map = generationManager.generator.Map;
			map.SetSetting("title", map.GetSetting("title") + " [" + msg.Author.Username +
			  "#" + msg.Author.Discriminator + "]");
			generationManager.generator.GenerateMap();
			string response = await generationManager.UploadLevel();

			generatingMessage.DeleteAsync();
			await msg.Channel.SendMessageAsync(msg.Author.Mention +
			  ", I got this message from pr2hub.com:\n`" + response + "`");
			Console.WriteLine("Uploaded: " + response + " [requested by " +
			  msg.Author.Username + "#" + msg.Author.Discriminator + "]");
			return true;
		}
		private async Task<bool> SendGenerateHelpMessage(SocketMessage msg)
		{
			await msg.Channel.SendMessageAsync(msg.Author.Mention +
			  ", to generate a level, please use the following format:\n" +
			  "```@me generate [name of settings to use]```\n" +
			  "To see a list of available settings, say `@me getsettings`.");

			Console.WriteLine("Sent generate help message to " + msg.Author.Username + "#" + msg.Author.Discriminator + ".");
			return true;
		}

		private async Task<bool> AddTrustedUser(SocketMessage msg, params string[] args)
		{
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
			return count != 0;
		}
		private async Task<bool> RemoveTrustedUser(SocketMessage msg, params string[] args)
		{
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
			return count != 0;
		}

		private async Task<bool> GetSettings(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await msg.Channel.SendMessageAsync("You didn't specify a setting to get, silly " +
				  msg.Author.Mention + "!");
				return false;
			}

			string settingsName = args[1];

			GenerationManager generationManager = new GenerationManager();
			if (generationManager.LoadSettings(Path.Combine(settingsPath, args[1])) == null)
			{
				await SendInvalidSettingMesage(msg, args[1]);
				return false;
			}

			if (args.Contains("text"))
			{
				string str = generationManager.GetSaveObject().ToString();
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", here are the settings for '" +
				  args[1] + "'\n```" + str + "```");
			}
			else
			{
				await msg.Channel.SendFileAsync(Path.Combine(settingsPath, args[1]), msg.Author.Mention +
				  ", here are the settings for '" + args[1]);
			}
			return true;
		}
		private async Task<bool> SetSettings(SocketMessage msg, params string[] args)
		{
			if (msg.Attachments.Count != 1)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", please upload a file to use this command.");
				return false;
			}

			Attachment a = msg.Attachments.First();
			HttpWebRequest request = HttpWebRequest.CreateHttp(a.Url);
			WebResponse response = await request.GetResponseAsync();
			StreamReader streamReader = new StreamReader(response.GetResponseStream());
			string str = await streamReader.ReadToEndAsync();

			string fileName = "temp" + tempFileID;
			tempFileID++;
			File.WriteAllText(fileName, str);

			GenerationManager generationManager = new GenerationManager();
			bool valid = false;
			try
			{ valid = generationManager.LoadSettings(fileName) != null; }
			catch (Newtonsoft.Json.JsonReaderException ex)
			{ valid = false; }

			if (!valid)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", the settings file you provided is invalid.");
				return false;
			}


			File.WriteAllText(Path.Combine(settingsPath, a.Filename), str);
			await msg.Channel.SendMessageAsync(msg.Author.Mention + ", settings '" +
			  a.Filename + "' have been saved.");

			File.Delete(fileName);
			return true;
		}
		private async Task SendInvalidSettingMesage(SocketMessage msg, string settingName)
		{
			await msg.Channel.SendMessageAsync(msg.Author.Mention + ", " +
			  "`" + settingName + "` is not a recognized setting or is corrupt.\n" +
			  "To view a list of the available settings, use the command `get_list`.");
		}

		private async Task<bool> GTFO(SocketMessage msg, params string[] args)
		{
			await msg.Channel.SendMessageAsync("I'm sorry you feel that way, " + msg.Author.Mention +
					". :(\nI guess I'll leave now. Bye guys!");
			Disconnect();
			return true;
		}

		private async Task<bool> SendBannedMessage(SocketMessage msg, params string[] args)
		{
			await msg.Author.SendMessageAsync("You have been banned from this bot.");
			return true;
		}

		#endregion

	}
}
