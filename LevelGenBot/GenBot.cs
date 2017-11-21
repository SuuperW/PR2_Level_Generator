using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
		const string luaPath = "lua";
		const string outputPath = "files/output.xml";
		const string errorPath = "files/error.txt";
		int loggingLevel;

		int tempFileID = -1;
		private string GetTempFileName()
		{
			tempFileID++;
			return "temp/" + tempFileID;
		}

		DiscordSocketClient socketClient;

		ulong BotID { get => socketClient.CurrentUser.Id; }
		public bool IsConnected { get => socketClient.ConnectionState >= ConnectionState.Connected; }

		SpecialUsersCollection specialUsers;
		CommandHistory commandHistory = new CommandHistory();

		public GenBot(int loggingLevel = 2)
		{
			this.loggingLevel = loggingLevel;

			if (!File.Exists("files/secrets.txt"))
				throw new FileNotFoundException("GenBot could not find secrets.txt. Please see README for info on how to set this up.");
			JObject json = JObject.Parse(File.ReadAllText("files/secrets.txt"));
			bot_token = json["bot_token"].ToString();
			pr2_username = json["pr2_username"].ToString();
			pr2_token = json["pr2_token"].ToString();

			specialUsers = new SpecialUsersCollection("files/special users.txt");

			helpStrings = new SortedDictionary<string, string>();
			JObject helpJson = JObject.Parse(File.ReadAllText("files/helpTopics.txt"));
			foreach (KeyValuePair<string, JToken> item in helpJson)
				helpStrings[item.Key] = item.Value.ToString();

			InitializeBotCommandsList();
			CreateHelpTopicsList();

			// Delete any temp files that still exist from last time the bot was run.
			if (Directory.Exists("temp"))
				Directory.Delete("temp", true);
			Directory.CreateDirectory("temp");

			Directory.CreateDirectory(configsPath);
			Directory.CreateDirectory(luaPath);
		}

		public event Action Connected;
		public event Action Disconnected;
		public async Task ConnectAndStart()
		{
			AppendToLog("<begin_login time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "'></begin_login>\n", 2);

			socketClient = await ConnectSocketClient();

			socketClient.MessageReceived += SocketClient_MessageReceived;

			await socketClient.SetGameAsync("with RNG");
		}

		public async Task Disconnect()
		{
			AppendToLog("<disconnect time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "'></disconnect>\n", 2);

			await socketClient.GetUser(specialUsers.Owner).SendMessageAsync("I'm diconnecting now.");

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
				AppendToLog("<ready time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "'></ready>\n", 2);
				return null;
			};
			client.Disconnected += (e) => { Disconnected?.Invoke(); return null; };

			await client.LoginAsync(TokenType.Bot, bot_token);
			await client.StartAsync();

			return client;
		}

		async Task<IUserMessage> SendMessage(IMessageChannel channel, string text)
		{ 
			Task logTask = AppendToLog("<send_message time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() +
			  "' channel='" + channel.Name + "'>\n" + text + "\n</send_message>\n");
			Task<IUserMessage> ret = channel.SendMessageAsync(text);

			await logTask;
			return await ret;
		}
		async Task<IUserMessage> SendFile(IMessageChannel channel, Stream fileStream, string fileName, string text = null)
		{ 
			Task logTask = AppendToLog("<send_file time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() +
			  "' channel='" + channel.Name + "' file=' " + fileName + "'>\n" + text + "\n</send_file>\n");
			Task<IUserMessage> ret = channel.SendFileAsync(fileStream, fileName, text);

			await logTask;
			return await ret;
		}
		async Task<IUserMessage> SendFile(IMessageChannel channel, string fileName, string text = null)
		{ 
			Task logTask = AppendToLog("<send_file time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() +
			  "' channel='" + channel.Name + "' file=' " + fileName + "'>\n" + text + "\n</send_file>\n");
			Task<IUserMessage> ret = channel.SendFileAsync(fileName, text);

			await logTask;
			return await ret;
		}
		async Task EditMessage(IUserMessage message, string text)
		{ 
			Task logTask = AppendToLog("<edit_message time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() +
			  "' channel='" + message.Channel.Name + "'>\n" + text + "\n</edit_message>\n");
			Task ret = message.ModifyAsync((p) => p.Content = text);

			await logTask;
			await ret;
		}
		Task AppendToLog(string text, int priority = 1)
		{
			if (priority >= loggingLevel)
				return File.AppendAllTextAsync(outputPath, text);
			else
				return Task.CompletedTask;
		}
		Task LogError(Exception ex)
		{
			StringBuilder errorStr = new StringBuilder();
			while (ex != null)
			{
				errorStr.Append(ex.GetType().ToString());
				errorStr.Append("\n");
				errorStr.Append(ex.Message);
				errorStr.Append("\n\n");
				errorStr.Append(ex.StackTrace);
				errorStr.Append("\n\n\n");
				ex = ex.InnerException;
			}
			errorStr.Length -= 3;

			File.WriteAllText(errorPath, errorStr.ToString());
			return AppendToLog("<error time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() +
			  "'>\n" + errorStr.ToString() + "\n</receive_command>\n");
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
				await LogError(ex);
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
					await AppendToLog("<receive_command time='" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() +
					  "' channel='" + msg.Channel.Name + "'>\n" + msg.Content + "\n</receive_command>\n");
					if (msg.Author.Id != specialUsers.Owner)
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
						await SendMessage(msg.Channel, msg.Author.Username + ", I attempted to send you a DM " +
						  "but was unable to. Please ensure that you can receive DMs from me.");
					}
				}
			}
			catch (Exception ex)
			{
				await LogError(ex);

				await SendFile(await socketClient.GetUser(specialUsers.Owner).GetOrCreateDMChannelAsync(), errorPath, "I've encountered an error.");
				await SendMessage(msg.Channel, msg.Author.Username +
					", I have encountered an error and don't know what to do with it. :(\n" +
					"Error details have been sent to my owner.");
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
			everybodyBotCommands.Add("commands", new BotCommand(SendCommandsList));
			everybodyBotCommands.Add("config_list", new BotCommand(SendConfigsListMessage));
			everybodyBotCommands.Add("generate", new BotCommand(GenerateLevel, 30, 5));
			everybodyBotCommands.Add("get_config", new BotCommand(GetConfigFile, 5));
			everybodyBotCommands.Add("set_config", new BotCommand(SetConfigFile, 5));
			everybodyBotCommands.Add("delete_config", new BotCommand(DeleteConfigFile));
			everybodyBotCommands.Add("set_lua_script", new BotCommand(SetLuaScript, 5));
			everybodyBotCommands.Add("get_lua_script", new BotCommand(GetLuaScript, 5));
			everybodyBotCommands.Add("lua_script_list", new BotCommand(GetLuaScriptList));
			everybodyBotCommands.Add("delete_lua_script", new BotCommand(DeleteLuaScript));
			everybodyBotCommands.Add("delete_level", new BotCommand(DeleteLevel, 15));

			trustedBotCommands = new SortedList<string, BotCommand>();
			//trustedBotCommands.Add("set_config", new BotCommand(SetSettings, 5));

			ownerBotCommands = new SortedList<string, BotCommand>();
			ownerBotCommands.Add("add_trusted_user", new BotCommand(AddTrustedUser));
			ownerBotCommands.Add("remove_trusted_user", new BotCommand(RemoveTrustedUser));
			ownerBotCommands.Add("gtfo", new BotCommand(GTFO));
			ownerBotCommands.Add("ban_user", new BotCommand(BanUser));
			ownerBotCommands.Add("unban_user", new BotCommand(UnbanUser));
			ownerBotCommands.Add("get_log", new BotCommand(GetLog));
			ownerBotCommands.Add("get_error", new BotCommand(GetError));

			bannedCommand = new BotCommand(SendBannedMessage);
		}
		private void CreateHelpTopicsList()
		{
			helpStrings.Add("topics", "");
			StringBuilder str = new StringBuilder("Here is the list of available help topics:```");
			foreach (string topic in helpStrings.Keys)
			{
				if (!topic.StartsWith('_'))
					str.Append("\n" + topic);
			}
			str.Append("```");

			helpStrings["topics"] = str.ToString();
		}

		#region "help"
		private SortedDictionary<string, string> helpStrings;
		private async Task<bool> SendHelpMessage(SocketMessage msg, params string[] args)
		{
			string helpTopic = args.Length > 1 ? args[1] : "_default";

			if (helpStrings.ContainsKey(helpTopic))
			{
				await SendMessage(await msg.Author.GetOrCreateDMChannelAsync(), helpStrings[helpTopic]
				  .Replace("@me", "@" + bot_name_discrim)
				  .Replace("@pr2acc", pr2_username));
			}
			else
			{
				await SendMessage(await msg.Author.GetOrCreateDMChannelAsync(),
				  "I could not find the help topic you gave me. To see a list of available help topics, use the command `help topics`.");
				return false;
			}

			return true;
		}
		private async Task<bool> SendCommandsList(SocketMessage msg, params string[] args)
		{
			StringBuilder availableCommands = new StringBuilder();
			foreach (KeyValuePair<string, BotCommand> kvp in everybodyBotCommands)
				availableCommands.Append("\n" + kvp.Key); // \n first because first line tells Discord how to format
			if (specialUsers.IsUserTrusted(msg.Author.Id))
			{
				// availableCommands.Append("\n\n----- Trusted User Commands -----"); un-comment when trusted users get a command again
				foreach (KeyValuePair<string, BotCommand> kvp in trustedBotCommands)
					availableCommands.Append("\n" + kvp.Key);
			}
			if (specialUsers.Owner == msg.Author.Id)
			{
				availableCommands.Append("\n\n----- Owner Commands -----");
				foreach (KeyValuePair<string, BotCommand> kvp in ownerBotCommands)
					availableCommands.Append("\n" + kvp.Key);
			}

			await SendMessage(await msg.Author.GetOrCreateDMChannelAsync(), "Here are the commands you can use: ```" + availableCommands + "```");
			return true;
		}
		#endregion

		#region "levels"
		private async Task<bool> GenerateLevel(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendMessage(msg.Channel, "You didn't specify a config file to use, silly " + msg.Author.Username + "!");
				return false;
			}

			string filePath = GetFilePath(msg.Author.Id, args[1], configsPath);
			GenerationManager generationManager = new GenerationManager(luaPath);
			string result = generationManager.LoadSettings(filePath);
			if (result != null)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", " +
				  "config `" + args[1] + "` failed to load. Reason: `" + result + "`\n");
				return false;
			}
			generationManager.username = pr2_username;
			generationManager.login_token = pr2_token;

			// modify config
			for (int i = 2; i < args.Length; i += 2)
			{
				if (args.Length <= i + 1 || !generationManager.SetParamOrSetting(args[i], args[i + 1]))
				{
					await SendMessage(msg.Channel, msg.Author.Username + ", I could not set `" + args[i] +
					  "`. Level generation cancelled.");
					return false;
				}
			}

			Task<IUserMessage> sendingGenerateMessage = SendMessage(msg.Channel, msg.Author.Username +
			  ", I am generating and uploading your level...");

			MapLE map = generationManager.generator.Map;
			map.SetSetting("title", map.GetSetting("title") + " [" + msg.Author.Username +
			  "#" + msg.Author.Discriminator + "]");
			CancellationTokenSource cts = new CancellationTokenSource(1000);
			result = generationManager.generator.GenerateMap(cts).Result;
			if (result != null)
			{
				await sendingGenerateMessage;
				if (cts.IsCancellationRequested)
				{
					await EditMessage(sendingGenerateMessage.Result, msg.Author.Username + ", your level took too long to generate.\n" +
					  "If this happens regularly with this config, please edit the config to make the levels smaller.");
				}
				else
				{
					await EditMessage(sendingGenerateMessage.Result, msg.Author.Username + ", your level failed to generate. " +
					  "Reason: `" + result + "`");
				}
				return false;
			}

			string response = await generationManager.UploadLevel();

			await EditMessage(sendingGenerateMessage.Result, msg.Author.Username +
			  ", I got this message from pr2hub.com:\n`" + response + "`");
			return true;
		}
		private async Task<bool> DeleteLevel(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", you didn't specify a level to delete.");
				return false;
			}

			GenerationManager generationManager = new GenerationManager(null);
			generationManager.login_token = pr2_token;
			string levelTitle = args[1] + " [" + msg.Author.Username + "#" + msg.Author.Discriminator + "]";
			int levelID = await generationManager.GetLevelID(levelTitle);

			if (levelID == -1)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", the level `" + levelTitle + "` could not be found.");
				return false;
			}

			await SendMessage(msg.Channel, msg.Author.Username + ", I got this message from pr2hub: `" +
			  await generationManager.DeleteLevel(levelID) + "`.");
			return true;
		}
		#endregion

		#region "config and Lua"
		private async Task<bool> SendConfigsListMessage(SocketMessage msg, params string[] args)
		{
			string message = "Here is a list of all available configs:\n" + GetFilesList(configsPath, msg.Author.Id);

			await SendMessage(await msg.Author.GetOrCreateDMChannelAsync(), message);
			return true;
		}
		private string GetFilesList(string path, ulong userID)
		{
			IEnumerable<string> filesList = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);
			StringBuilder fileList = new StringBuilder("```\n");
			foreach (string file in filesList)
				fileList.Append(new FileInfo(file).Name + "\n");

			string privateFiles = Path.Combine(path, userID.ToString());
			Directory.CreateDirectory(privateFiles);
			filesList = Directory.EnumerateFiles(privateFiles, "*", SearchOption.TopDirectoryOnly);
			foreach (string file in filesList)
				fileList.Append("me/" + new FileInfo(file).Name + "\n");
			fileList.Append("```");

			return fileList.ToString();
		}

		private async Task<bool> GetConfigFile(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendMessage(msg.Channel, "You didn't specify a config file to get, silly " + msg.Author.Username + "!");
				return false;
			}

			string filePath = GetFilePath(msg.Author.Id, args[1], configsPath);

			await GetAndSendFile(filePath, ".txt", msg, args.Contains("text"));
			return true;
		}
		private string GetFilePath(ulong userID, string fileName, string basePath)
		{
			fileName = fileName.Replace("../", ""); // Security
			if (fileName.StartsWith("me/"))
				fileName = userID + fileName.Substring(2);

			string filePath = Path.Combine(basePath, fileName);
			// ensure the file we're getting is inside the configs directory
			if (!new DirectoryInfo(filePath).FullName.StartsWith(new DirectoryInfo(basePath).FullName))
				return null;

			return filePath;
		}
		private async Task GetAndSendFile(string filePath, string extension, SocketMessage msg, bool asText)
		{
			string fileName = Path.GetFileNameWithoutExtension(filePath) + extension;
			if (!File.Exists(filePath))
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", the file `" + fileName + "` does not exist.");
				return;
			}

			string messageStr = msg.Author.Username + ", here is the file.";
			if (asText)
			{
				string fileStr = File.ReadAllText(filePath);
				messageStr = msg.Author.Username + ", here are the contents of `" +
				  fileName + "`.\n```" + (extension == ".lua" ? "lua\n": "") + fileStr + "```";
				if (messageStr.Length < 2000)
					await SendMessage(msg.Channel, messageStr);
				else
				{
					asText = false;
					messageStr = msg.Author.Username + ", the contents of '" + fileName +
					  "' are too large to post in a Discord message, so here is the file.";
				}
			}

			if (!asText)
			{
				FileStream stream = new FileStream(filePath, FileMode.Open);
				await SendFile(msg.Channel, stream, fileName, messageStr);
			}
		}

		private async Task<bool> SetConfigFile(SocketMessage msg, params string[] args)
		{
			string fileName = await FileNameFromAttachment(msg);
			if (fileName == null)
				return false; // FileNameFromAttachment will send the user an error message, if appropriate.
			string filePath = Path.Combine(configsPath, fileName);

			string str = await GetAttachmentString(msg.Attachments.First());
			if (str.Length > 0x4000)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", the config file you provided is too big.");
				return false;
			}

			str = str.Replace("\"Generator Type\": \"me/", "\"Generator Type\": \"" + msg.Author.Id + "/");
			string tempFileName = GetTempFileName();
			File.WriteAllText(tempFileName, str);

			GenerationManager generationManager = new GenerationManager(luaPath);
			string result = generationManager.LoadSettings(tempFileName);
			File.Delete(tempFileName);
			if (result != null)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", the config file you provided is invalid. Reason: `" + result + "`");
				return false;
			}

			string rejectedReason = VerifySettings(generationManager.generator, msg.Author.Id);
			if (rejectedReason != null)
			{
				await SendMessage(msg.Channel, msg.Author.Username + " - " + rejectedReason);
				return false;
			}

			if (!specialUsers.IsUserTrusted(msg.Author.Id) || !args.Contains("public"))
			{
				string dir = Path.Combine(Directory.GetParent(filePath).FullName, msg.Author.Id.ToString());
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				filePath = Path.Combine(dir, fileName);
			}

			if (Directory.EnumerateFiles(Directory.GetParent(filePath).FullName, "*", SearchOption.TopDirectoryOnly).Count() > 50)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", you have too many saved configs. " +
				  "Please delete one before uploading any more.");
				return false;
			}

			File.WriteAllText(filePath, str);
			await SendMessage(msg.Channel, msg.Author.Username + ", config file '" + fileName + "' has been saved.");

			return true;
		}
		private string VerifySettings(ILevelGenerator gen, ulong userID)
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

		private async Task<bool> DeleteConfigFile(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendMessage(msg.Channel, "You didn't specify a config file to delete, silly " +
				  msg.Author.Username + "!");
				return false;
			}
			else if (!args[1].StartsWith("me/") && msg.Author.Id != specialUsers.Owner)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", you may only delete your own configs.");
				return false;
			}

			string filePath = GetFilePath(msg.Author.Id, args[1], configsPath);
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				await SendMessage(msg.Channel, msg.Author.Username + ", the config '" + args[1] + "' has been deleted.");
			}
			else
				await SendMessage(msg.Channel, msg.Author.Username + ", the config `" + args[1] + "` does not exist.");
			return true;
		}

		private async Task<bool> GetLuaScriptList(SocketMessage msg, params string[] args)
		{
			string message = "Here is a list of all available lua scripts:\n" + GetFilesList(luaPath, msg.Author.Id);

			await SendMessage(await msg.Author.GetOrCreateDMChannelAsync(), message);
			return true;
		}
		private async Task<bool> SetLuaScript(SocketMessage msg, params string[] args)
		{
			string fileName = await FileNameFromAttachment(msg);
			if (fileName == null)
				return false; // FileNameFromAttachment will send the user an error message, if appropriate.
			string filePath = Path.Combine(luaPath, fileName);

			string str = await GetAttachmentString(msg.Attachments.First());
			if (str.Length > 0x8000)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", the lua file you provided is too big.");
				return false;
			}

			// Verify Lua script is valid
			LuaGenerator luaGenerator = new LuaGenerator();
			string result = luaGenerator.SetLua(str);
			if (result != null)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", the lua file you provided is invalid. Details: `" + result + "`");
				return false;
			}

			// Public or private?
			if (!specialUsers.IsUserTrusted(msg.Author.Id) || !args.Contains("public"))
			{
				string dir = Path.Combine(Directory.GetParent(filePath).Name, msg.Author.Id.ToString());
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				filePath = Path.Combine(dir, fileName);
			}

			// Limit number of scripts a user can have
			if (Directory.EnumerateFiles(Directory.GetParent(filePath).FullName, "*", SearchOption.TopDirectoryOnly).Count() > 50)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", you have too many saved configs. " +
				  "Please delete one before uploading any more.");
				return false;
			}

			// Save file
			File.WriteAllText(filePath, str);

			// Create config file
			GenerationManager generationManager = new GenerationManager(luaPath);
			generationManager.generator = luaGenerator;
			luaGenerator.ScriptName = filePath.Substring(luaPath.Length + 1).Replace('\\', '/');
			JObject config = generationManager.GetSaveObject();
			config["Map Settings"]["live"] = 0;
			config["Map Settings"]["hasPass"] = 1;
			config["Map Settings"]["title"] = fileName;

			// Send the file to the user
			MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(config.ToString()));
			await SendFile(msg.Channel, stream, fileName + ".txt", msg.Author.Username + ", here is a config file to use with your Lua script." +
			  "\nModify any settings you want and then upload it with `set_config`.");

			return true;
		}
		private async Task<bool> GetLuaScript(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendMessage(msg.Channel, "You didn't specify a lua script to get, silly " + msg.Author.Username + "!");
				return false;
			}

			string filePath = GetFilePath(msg.Author.Id, args[1], luaPath);

			await GetAndSendFile(filePath, ".lua", msg, args.Contains("text"));
			return true;
		}
		private async Task<bool> DeleteLuaScript(SocketMessage msg, params string[] args)
		{
			if (args.Length < 2)
			{
				await SendMessage(msg.Channel, "You didn't specify a lua script to delete, silly " +
				  msg.Author.Username + "!");
				return false;
			}
			else if (!args[1].StartsWith("me/") && msg.Author.Id != specialUsers.Owner)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", you may only delete your own configs.");
				return false;
			}

			string filePath = GetFilePath(msg.Author.Id, args[1], luaPath);
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				await SendMessage(msg.Channel, msg.Author.Username + ", the lua script '" + args[1] + "' has been deleted.");
			}
			else
				await SendMessage(msg.Channel, msg.Author.Username + ", the lua script `" + args[1] + "` does not exist.");
			return true;
		}
		#endregion

		#region "owner"
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

			await SendMessage(msg.Channel, "Added " + count + " user(s) to trusted user list.");
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

			await SendMessage(msg.Channel, "Removed " + count + " user(s) from trusted user list.");
			return count != 0;
		}

		private async Task<bool> GTFO(SocketMessage msg, params string[] args)
		{
			await SendMessage(msg.Channel, "I'm sorry you feel that way, " + msg.Author.Username +
			  ". :(\nI guess I'll leave now. Bye guys!");
			Disconnect(); // Do not await because DCing in the middle of the DiscordSocketClient's MessageReceived event causes problems.
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

			await SendMessage(msg.Channel, count + " user(s) have been banned.");
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

			await SendMessage(msg.Channel, count + " user(s) have been unbanned.");
			return count != 0;
		}

		private async Task<bool> GetLog(SocketMessage msg, params string[] args)
		{
			await SendFile(await socketClient.GetUser(specialUsers.Owner).GetOrCreateDMChannelAsync(), outputPath);
			return true;
		}
		private async Task<bool> GetError(SocketMessage msg, params string[] args)
		{
			await SendFile(await socketClient.GetUser(specialUsers.Owner).GetOrCreateDMChannelAsync(), errorPath);
			return true;
		}
		#endregion

		private async Task<bool> SendBannedMessage(SocketMessage msg, params string[] args)
		{
			await SendMessage(await msg.Author.GetOrCreateDMChannelAsync(), "You have been banned from this bot.");
			return true;
		}
		#endregion

		private async Task<string> FileNameFromAttachment(SocketMessage msg)
		{
			if (msg.Attachments.Count != 1)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", please upload a file to use this command.");
				return null;
			}

			string fileName = msg.Attachments.First().Filename.Replace("../", ""); // .Replace for security
			fileName = Path.ChangeExtension(fileName, null);
			// Ensure the path doesn't lead outside the current folder.
			if (Directory.GetParent(fileName).FullName != new DirectoryInfo(".").FullName)
			{
				await SendMessage(msg.Channel, msg.Author.Username + ", there seems to be something wonky with your file name.");
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
