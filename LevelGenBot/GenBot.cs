﻿using System;
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
				// Ensure command is in proper format (@bot args)
				bool msgIsCommand = false;
				if (msg.Channel is IDMChannel)
					msgIsCommand = true;
				else
				{
					string[] words = msg.Content.Split(' ');
					MentionUtils.TryParseUser(words[0], out ulong mentionedID);
					msgIsCommand = (mentionedID == MentionUtils.ParseUser(socketClient.CurrentUser.Mention)
						&& words.Length > 1);
				}

				Task task = null;
				if (!msgIsCommand)
				{
					if (msg.Channel is IDMChannel || msg.MentionedUsers.Contains(socketClient.CurrentUser))
						task = SendHelpMessage(msg, null);
				}
				else
				{
					Console.WriteLine("Received command from " + msg.Author.Username + ": " + msg.Content);
					string[] args = ParseCommand(msg.Content);
					if (everybodyBotCommands.ContainsKey(args[0]))
						task = everybodyBotCommands[args[0]](msg, args);
					else if (specialUsers.IsUserTrusted(msg.Author.Id) && trustedBotCommands.ContainsKey(args[0]))
						task = trustedBotCommands[args[0]](msg, args);
					else if (specialUsers.Owner == msg.Author.Id && ownerBotCommands.ContainsKey(args[0]))
						task = ownerBotCommands[args[0]](msg, args);
					else
						task = SendHelpMessage(msg, null);
				}

				try
				{
					if (task != null)
						await task;
				}
				catch (Exception ex)
				{
					//System.Diagnostics.Debugger.Break();
					await msg.Channel.SendMessageAsync(msg.Author.Mention +
						", I have encountered an error and don't know what to do with it. :(" +
						"Error details have been sent to my owner.");
					await socketClient.GetUser(specialUsers.Owner).SendMessageAsync("Error!  " +
						"`" + ex.Message + "`\n```" + ex.StackTrace + "```");
				}
			}
		}
		private string[] ParseCommand(string msg)
		{
			int index = -1;
			if (msg.IndexOf('<') == 0) // If this command was initated with a mention at the front of it.
				index = msg.IndexOf('>') + 1;
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
		private SortedList<string, BotCommand> everybodyBotCommands;
		private SortedList<string, BotCommand> trustedBotCommands;
		private SortedList<string, BotCommand> ownerBotCommands;
		private void InitializeBotCommandsList()
		{
			everybodyBotCommands = new SortedList<string, BotCommand>();
			everybodyBotCommands.Add("help", SendHelpMessage);
			everybodyBotCommands.Add("getsettings", SendSettingsListMessage);
			everybodyBotCommands.Add("generate", GenerateLevel);
			everybodyBotCommands.Add("get_settings", GetSettings);

			trustedBotCommands = new SortedList<string, BotCommand>();
			trustedBotCommands.Add("set_settings", SetSettings);

			ownerBotCommands = new SortedList<string, BotCommand>();
			ownerBotCommands.Add("add_trusted_user", AddTrustedUser);
			ownerBotCommands.Add("remove_trusted_user", RemoveTrustedUser);
			ownerBotCommands.Add("gtfo", async (m, a) =>
			{
				await m.Channel.SendMessageAsync("I'm sorry you feel that way, " + m.Author.Mention +
					". :(\nI guess I'll leave now. Bye guys!");
				Disconnect();
			});
		}

		private async Task SendHelpMessage(SocketMessage msg, params string[] args)
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

			await msg.Author.SendMessageAsync(msg.Author.Mention +
				", to use this bot send a message with the format `@me command [command arguments]`.\n" +
				"If a command or argument contains a space, surround it with quotation marks.\n" +
				"Example: `@me \"long race\"`\n" +
				"If you are sending the command via DMs, mentioning me is not required.\n" +
				"List of available commands: ```" + availableCommands.ToString() + "```");

			Console.WriteLine("Sent help to " + msg.Author.Username + ".");
		}
		private async Task SendSettingsListMessage(SocketMessage msg, params string[] args)
		{
			IEnumerable<string> filesList = Directory.EnumerateFiles(settingsPath, "*", SearchOption.AllDirectories);
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

			if (generationManager.LoadSettings(Path.Combine(settingsPath, args[1])) == null)
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

		private async Task GetSettings(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await msg.Channel.SendMessageAsync("You didn't specify a setting to get, silly " +
					msg.Author.Mention + "!");
				return;
			}

			string settingsName = args[1];

			GenerationManager generationManager = new GenerationManager();
			if (generationManager.LoadSettings(Path.Combine(settingsPath, args[1])) == null)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", " +
					"`" + args[1] + "` is not a recognized setting or is corrupt.");
				return;
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
		}
		private async Task SetSettings(SocketMessage msg, params string[] args)
		{
			if (msg.Attachments.Count != 1)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", please upload a file to use this command.");
				return;
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
				return;
			}


			File.WriteAllText(Path.Combine(settingsPath, a.Filename), str);
			await msg.Channel.SendMessageAsync(msg.Author.Mention + ", settings '" +
				a.Filename + "' have been saved.");

			File.Delete(fileName);
		}

		#endregion

	}
}
