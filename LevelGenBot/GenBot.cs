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
		string bot_name_discrim;
		string pr2_username;
		string pr2_token;
		const string configsPath = "GenConfigs";

		int tempFileID = -1;
		private string GetTempFileName()
		{
			tempFileID++;
			return "temp/" + tempFileID;
		}

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

			helpStrings = new SortedDictionary<string, string>();
			JObject helpJson = JObject.Parse(File.ReadAllText("helpTopics.txt"));
			foreach (KeyValuePair<string, JToken> item in helpJson)
				helpStrings[item.Key] = item.Value.ToString();

			InitializeBotCommandsList();

			// Delete any temp files that still exist from last time the bot was run.
			if (Directory.Exists("temp"))
				Directory.Delete("temp");
			Directory.CreateDirectory("temp");

			Directory.CreateDirectory(configsPath);
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
			await socketClient.GetUser(specialUsers.Owner).SendMessageAsync("I'm diconnecting now.");

			await restClient.LogoutAsync();

			await socketClient.SetStatusAsync(UserStatus.Invisible);
			// Wait, just to verify that the status update has time to go through.
			await socketClient.GetDMChannelAsync(socketClient.CurrentUser.Id);

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
			client.Ready += () =>
			{
				bot_name_discrim = socketClient.CurrentUser.Username + "#" + socketClient.CurrentUser.Discriminator;
				Connected?.Invoke();
				return null;
			};
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
				if (commandHistory.TimeSinceLastCommand(msg.Author.Id) > 30)
				{
					await bannedCommand.Delegate(msg, null);
					commandHistory.AddCommand(bannedCommand.Name, msg.Author.Id);
				}
				return;
			}

			try
			{
				BotCommand command = MessageToCommand(msg, out string[] args);
				if (command != null)
				{
					Console.WriteLine("Received command from " + msg.Author.Username + ": " + msg.Content);
					command = commandHistory.CommandOrWait(command, msg.Author.Id);

					if (await command.Delegate(msg, args))
						commandHistory.AddCommand(command.Name, msg.Author.Id);
				}
			}
			catch (Discord.Net.HttpException ex)
			{
				if (ex.Message.Contains("50007")) // can't send messages to this user
				{
					if (!msg.Author.IsBot)
					{
						await msg.Channel.SendMessageAsync(msg.Author.Mention + ", I attempted to send you a DM " +
						  "but was unable to. Please ensure that you can receive DMs from me.");
					}
				}
			}
			catch (Exception ex)
			{
				//System.Diagnostics.Debugger.Break();
				await msg.Channel.SendMessageAsync(msg.Author.Mention +
					", I have encountered an error and don't know what to do with it. :(\n" +
					"Error details have been sent to my owner.");
				string fileName = GetTempFileName() + ".txt";
				File.WriteAllText(fileName, "Error: " + ex.GetType().ToString() + "\n\n" +
				  ex.Message + "\n\n" + ex.StackTrace);
				IDMChannel channel = await socketClient.GetUser(specialUsers.Owner).GetOrCreateDMChannelAsync();
				await channel.SendFileAsync(fileName, "I encountered an error. Here are the details.");
				File.Delete(fileName);
			}
		}

		private BotCommand MessageToCommand(SocketMessage msg, out string[] args)
		{
			BotCommand command = null;
			args = null;

			if (IsMessageProperCommand(msg))
			{
				args = ParseCommand(msg.Content);
				string commandStr = args[0].ToLower();

				if (everybodyBotCommands.ContainsKey(commandStr))
					command = everybodyBotCommands[commandStr];
				else if (specialUsers.IsUserTrusted(msg.Author.Id) && trustedBotCommands.ContainsKey(commandStr))
					command = trustedBotCommands[commandStr];
				else if (specialUsers.Owner == msg.Author.Id && ownerBotCommands.ContainsKey(commandStr))
					command = ownerBotCommands[commandStr];
				else
				{
					command = everybodyBotCommands["help"];
					args = new string[] { "help", "_invalid_command" };
				}
			}
			// If the bot is mentioned, send a help message.
			else if (msg.MentionedUsers.FirstOrDefault((u) => u.Id == socketClient.CurrentUser.Id) != null)
			{
				command = everybodyBotCommands["help"];
				args = new string[] { "help", "_hello" };
			}

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
				while (msg.Length > space && !char.IsWhiteSpace(msg[space]))
					space++;

				int newIndex = 0;
				if (quote != -1 && (quote < space || space == -1))
				{
					newIndex = msg.IndexOf('"', quote + 1);
					if (newIndex == -1)
						newIndex = msg.Length;
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
			} while (index < msg.Length);

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
			everybodyBotCommands.Add("help_topics", new BotCommand(SendHelpTopicsMessage));
			everybodyBotCommands.Add("commands", new BotCommand(SendCommandsList));
			everybodyBotCommands.Add("config_list", new BotCommand(SendConfigsListMessage));
			everybodyBotCommands.Add("generate", new BotCommand(GenerateLevel, 30, 5));
			everybodyBotCommands.Add("get_config", new BotCommand(GetConfigFile, 5));
			everybodyBotCommands.Add("set_config", new BotCommand(SetConfigFile, 5));
			everybodyBotCommands.Add("delete_config", new BotCommand(DeleteConfigFile));

			trustedBotCommands = new SortedList<string, BotCommand>();
			//trustedBotCommands.Add("set_config", new BotCommand(SetSettings, 5));

			ownerBotCommands = new SortedList<string, BotCommand>();
			ownerBotCommands.Add("add_trusted_user", new BotCommand(AddTrustedUser));
			ownerBotCommands.Add("remove_trusted_user", new BotCommand(RemoveTrustedUser));
			ownerBotCommands.Add("gtfo", new BotCommand(GTFO));
			ownerBotCommands.Add("ban_user", new BotCommand(BanUser));
			ownerBotCommands.Add("unban_user", new BotCommand(UnbanUser));

			bannedCommand = new BotCommand(SendBannedMessage);
		}

		private SortedDictionary<string, string> helpStrings;
		private async Task<bool> SendHelpMessage(SocketMessage msg, params string[] args)
		{
			string helpTopic = args.Length > 1 ? args[1] : "_default";

			if (helpStrings.ContainsKey(helpTopic))
			{
				await msg.Author.SendMessageAsync(helpStrings[helpTopic]
				  .Replace("@me", "@" + bot_name_discrim)
				  .Replace("@pr2acc", pr2_username));
			}
			else
			{
				await msg.Author.SendMessageAsync("I could not find the help topic you gave me. To see a list of available help topics, use the command `help_topics`.");
				return false;
			}

			return true;
		}
		private async Task<bool> SendHelpTopicsMessage(SocketMessage msg, params string[] args)
		{
			StringBuilder str = new StringBuilder("Here is the list of available help topics:```");
			foreach (string topic in helpStrings.Keys)
			{
				if (!topic.StartsWith('_'))
					str.Append("\n" + topic);
			}
			str.Append("```");

			await msg.Author.SendMessageAsync(str.ToString());
			return true;
		}
		private async Task<bool> SendCommandsList(SocketMessage msg, params string[] args)
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

			await msg.Author.SendMessageAsync("Here are the commands you can use: ```" + availableCommands + "```");
			return true;
		}

		private async Task<bool> GenerateLevel(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendGenerateHelpMessage(msg);
				return false;
			}

			string filePath = GetConfigPath(msg.Author.Id, args[1]);
			GenerationManager generationManager = new GenerationManager();
			if (generationManager.LoadSettings(filePath) == null)
			{
				await SendInvalidConfigMesage(msg, args[1]);
				return false;
			}
			generationManager.username = pr2_username;
			generationManager.login_token = pr2_token;

			// modify config
			for (int i = 2; i < args.Length; i += 2)
			{
				if (args.Length <= i + 1 || !generationManager.SetParamOrSetting(args[i], args[i + 1]))
				{
					await msg.Channel.SendMessageAsync(msg.Author.Mention + ", I could not set `" + args[i] +
					  "`. Level generation cancelled.");
					return false;
				}
			}

			RestUserMessage generatingMessage = await msg.Channel.SendMessageAsync(msg.Author.Mention +
			  ", I am generating and uploading your level...");

			MapLE map = generationManager.generator.Map;
			map.SetSetting("title", map.GetSetting("title") + " [" + msg.Author.Username +
			  "#" + msg.Author.Discriminator + "]");
			bool success = generationManager.generator.GenerateMap(new System.Threading.CancellationTokenSource(1000)).Result;
			if (!success)
			{
				await generatingMessage.DeleteAsync();
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", your level took too long to generate.\n" +
				  "If this happens regularly with this config, please edit the config to make the levels smaller.");
				return false;
			}

			string response = await generationManager.UploadLevel();

			await generatingMessage.DeleteAsync();
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
			  "```@me generate [name of config to use]```\n" +
			  "To see a list of available configs, say `@me config_list`.");

			Console.WriteLine("Sent generate help message to " + msg.Author.Username + "#" + msg.Author.Discriminator + ".");
			return true;
		}

		private async Task<bool> AddTrustedUser(SocketMessage msg, params string[] args)
		{
			int count = 0;
			foreach (ITag tag in msg.Tags)
			{
				if (tag.Type == TagType.UserMention && tag.Key != BotID)
				{
					if (specialUsers.AddTrustedUser(tag.Key))
						count++;
				}
			}

			await msg.Channel.SendMessageAsync("Added " + count + " user(s) to trusted user list.");
			return count != 0;
		}
		private async Task<bool> RemoveTrustedUser(SocketMessage msg, params string[] args)
		{
			int count = 0;
			foreach (ITag tag in msg.Tags)
			{
				if (tag.Type == TagType.UserMention && tag.Key != BotID)
				{
					if (specialUsers.RemoveTrustedUser(tag.Key))
						count++;
				}
			}

			await msg.Channel.SendMessageAsync("Removed " + count + " user(s) from trusted user list.");
			return count != 0;
		}
		private async Task<bool> SendConfigsListMessage(SocketMessage msg, params string[] args)
		{
			IEnumerable<string> filesList = Directory.EnumerateFiles(configsPath, "*", SearchOption.TopDirectoryOnly);
			StringBuilder configsList = new StringBuilder("Here is a list of all available configs:\n```\n");
			foreach (string file in filesList)
				configsList.Append(new FileInfo(file).Name + "\n");

			string privateConfigs = Path.Combine(configsPath, msg.Author.Id.ToString());
			Directory.CreateDirectory(privateConfigs);
			filesList = Directory.EnumerateFiles(privateConfigs, "*", SearchOption.TopDirectoryOnly);
			foreach (string file in filesList)
				configsList.Append("me/" + new FileInfo(file).Name + "\n");
			configsList.Append("```");

			await msg.Author.SendMessageAsync(configsList.ToString());
			Console.WriteLine("Sent configs list to " + msg.Author.Username + "#" + msg.Author.Discriminator + ".");
			return true;
		}

		private async Task<bool> GetConfigFile(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await msg.Channel.SendMessageAsync("You didn't specify a config file to get, silly " +
				  msg.Author.Mention + "!");
				return false;
			}

			string filePath = GetConfigPath(msg.Author.Id, args[1]);
			GenerationManager generationManager = new GenerationManager();
			if (generationManager.LoadSettings(filePath) == null)
			{
				await SendInvalidConfigMesage(msg, args[1]);
				return false;
			}

			bool getFile = !args.Contains("text");
			string messageStr = msg.Author.Mention + ", here is the '" + args[1] + "' config file.";
			if (!getFile)
			{
				string fileStr = generationManager.GetSaveObject().ToString();
				messageStr = msg.Author.Mention + ", here are the settings for config '" +
				  args[1] + "'.\n```" + fileStr + "```";
				if (messageStr.Length < 2000)
					await msg.Channel.SendMessageAsync(messageStr);
				else
				{
					getFile = true;
					messageStr = msg.Author.Mention + ", the contents of the '" + args[1] +
					  "' config file are too large to post in a Discord message, so here is the file.";
				}
			}

			if (getFile)
			{
				FileStream stream = new FileStream(filePath, FileMode.Open);
				string uploadFileName = Path.GetFileNameWithoutExtension(filePath) + ".txt";
				await msg.Channel.SendFileAsync(stream, uploadFileName, messageStr);
			}
			return true;
		}
		private string GetConfigPath(ulong userID, string configName)
		{
			configName = configName.Replace("../", ""); // Security
			if (configName.StartsWith("me/"))
				configName = userID + configName.Substring(2);

			string filePath = Path.Combine(configsPath, configName);
			// ensure the file we're getting is inside the configs directory
			if (!new DirectoryInfo(filePath).FullName.StartsWith(new DirectoryInfo(configsPath).FullName))
				return null;

			return filePath;
		}

		private async Task<bool> SetConfigFile(SocketMessage msg, params string[] args)
		{
			string fileName = await FileNameFromAttachment(msg);
			if (fileName == null)
				return false;

			string str = await GetAttachmentString(msg.Attachments.First());
			if (str.Length > 0x4000)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", the config file you provided is too big.");
				return false;
			}

			ILevelGenerator gen = GeneratorFromSettings(str);
			if (gen == null)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", the config file you provided is invalid.");
				return false;
			}

			string rejectedReason = VerifySettings(gen, fileName, msg.Author.Id);
			if (rejectedReason != null)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + " - " + rejectedReason);
				return false;
			}

			if (!specialUsers.IsUserTrusted(msg.Author.Id) || !args.Contains("public"))
			{
				string dir = Path.Combine(Directory.GetParent(fileName).FullName, msg.Author.Id.ToString());
				fileName = Path.Combine(dir, new FileInfo(fileName).Name);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
			}

			if (Directory.EnumerateFiles(Directory.GetParent(fileName).FullName, "*", SearchOption.TopDirectoryOnly).Count() > 50)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", you have too many saved configs. " +
				  "Please delete one before uploading any more.");
				return false;
			}

			File.WriteAllText(fileName, str);
			await msg.Channel.SendMessageAsync(msg.Author.Mention + ", config file '" +
			  msg.Attachments.First().Filename + "' has been saved.");

			return true;
		}
		private ILevelGenerator GeneratorFromSettings(string settings)
		{
			string tempFileName = GetTempFileName();
			File.WriteAllText(tempFileName, settings);

			GenerationManager generationManager = new GenerationManager();
			bool valid = false;
			try
			{ valid = generationManager.LoadSettings(tempFileName) != null; }
			catch (Newtonsoft.Json.JsonReaderException ex)
			{ valid = false; }

			File.Delete(tempFileName);

			if (valid)
				return generationManager.generator;
			else
				return null;
		}
		private string VerifySettings(ILevelGenerator gen, string fileName, ulong userID)
		{
			if (userID == specialUsers.Owner)
				return null;

			if (!specialUsers.IsUserTrusted(userID))
			{
				if (gen.Map.GetSetting("live") != "0" || gen.Map.GetSetting("hasPass") != "1")
					return "Your level must be unpublished and have a password.";
			}

			gen.Map.SetSetting("credits", gen.Map.GetSetting("credits") + " [" + userID.ToString() + "]");
			return null;
		}
		private async Task SendInvalidConfigMesage(SocketMessage msg, string configName)
		{
			await msg.Channel.SendMessageAsync(msg.Author.Mention + ", " +
			  "`" + configName + "` is not a recognized config file or is corrupt.\n" +
			  "To view a list of the available configs, use the command `config_list`.");
		}

		private async Task<bool> DeleteConfigFile(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await msg.Channel.SendMessageAsync("You didn't specify a config file to delete, silly " +
				  msg.Author.Mention + "!");
				return false;
			}
			else if (!args[1].StartsWith("me/") && msg.Author.Id != specialUsers.Owner)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", you may only delete your own configs.");
				return false;
			}

			string filePath = GetConfigPath(msg.Author.Id, args[1]);
			if (File.Exists(filePath))
				File.Delete(filePath);

			await msg.Channel.SendMessageAsync(msg.Author.Mention + ", the config '" + args[1] + "' has been deleted.");
			return true;
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

		private async Task<bool> BanUser(SocketMessage msg, params string[] args)
		{
			int count = 0;
			foreach (ITag tag in msg.Tags)
			{
				if (tag.Type == TagType.UserMention && tag.Key != BotID)
				{
					if (specialUsers.BanUser(tag.Key))
						count++;
				}
			}

			await msg.Channel.SendMessageAsync(count + " user(s) have been banned.");
			return count != 0;
		}
		private async Task<bool> UnbanUser(SocketMessage msg, params string[] args)
		{
			int count = 0;
			foreach (ITag tag in msg.Tags)
			{
				if (tag.Type == TagType.UserMention && tag.Key != BotID)
				{
					if (specialUsers.UnbanUser(tag.Key))
						count++;
				}
			}

			await msg.Channel.SendMessageAsync(count + " user(s) have been unbanned.");
			return count != 0;
		}

		#endregion

		private async Task<string> FileNameFromAttachment(SocketMessage msg)
		{
			if (msg.Attachments.Count != 1)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", please upload a file to use this command.");
				return null;
			}

			Attachment a = msg.Attachments.First();
			string fileName = Path.Combine(configsPath, a.Filename.Replace("../", "")); // .Replace for security
			if (fileName.EndsWith(".txt"))
				fileName = fileName.Substring(0, fileName.Length - 4);
			if (Directory.GetParent(fileName).FullName != new DirectoryInfo(configsPath).FullName)
			{
				await msg.Channel.SendMessageAsync(msg.Author.Mention + ", there seems to be something wonky with your file name.");
				return null;
			}
			else
				return fileName;
		}
		private async Task<string> GetAttachmentString(Attachment attachment)
		{
			HttpWebRequest request = HttpWebRequest.CreateHttp(attachment.Url);
			WebResponse response = await request.GetResponseAsync();
			StreamReader streamReader = new StreamReader(response.GetResponseStream());
			string ret = await streamReader.ReadToEndAsync();

			streamReader.Close();
			response.Close();

			return ret;
		}

	}
}
