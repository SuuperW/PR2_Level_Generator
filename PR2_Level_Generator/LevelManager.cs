using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Web;

using Newtonsoft.Json.Linq;

namespace PR2_Level_Generator
{
	public class GenerationManager
	{
		public GenerationManager(string luaPath)
		{
			this.luaPath = luaPath;
		}

		public string login_token;
		public string username;
        public string password;
		public string luaPath = "lua";

		public ILevelGenerator generator;
		private MapLE Map { get => generator.Map; }

        public event Action DetectedInvalidToken;
        private const string notLoggedInMessage = "error=You are not logged in.";

		public async Task<string> UploadLevel()
		{
			string oldNote = SetLevelNote();
			string LData = GetLevelData() + "&token=" + login_token;
			Map.SetSetting("note", oldNote);

            if (LData.Length > 1000000)
                return "Level data is too large; pr2hub will only accept levels less than ~1MB.";
            else
            {
                string result = await PostLoadHTTP("http://pr2hub.com/upload_level.php", LData);
                if (result == notLoggedInMessage)
                    DetectedInvalidToken?.Invoke();
                return result;
            }
		}
		public bool SaveLevel(string path)
		{
			if (!Directory.GetParent(path).Exists)
			{
				Console.WriteLine("Failed to save level; directory does not exist.");
				return false;
			}

			string oldNote = SetLevelNote();
			string LData = GetLevelData();
			Map.SetSetting("note", oldNote);
			File.WriteAllText(path, LData);

			return true;
		}
		private string GetLevelData()
		{
			Map.userName = username;
			return Map.GetData();
		}
		private string SetLevelNote()
		{
			// Append parameters to the note.
			string oldSeed = null;
			if (generator.GetParamNames().Contains("seed"))
			{
				oldSeed = generator.GetParamValue("seed");
				generator.SetParamValue("seed", generator.LastSeed.ToString());
			}

			string oldNote = Map.GetSetting("note");
			Map.SetSetting("note", oldNote + "Gen: " +
			  ((generator is LuaGenerator) ? (generator as LuaGenerator).ScriptName : generator.GetType().ToString().Split('.').Last()) + "\n" +
			  GetSaveObject()["Generator Params"].ToString().Replace("\r\n", "\n").Replace(" ", ""));

			if (oldSeed != null)
				generator.SetParamValue("seed", oldSeed);

			return oldNote;
		}


		public bool SaveSettings(string path)
		{
			if (!Directory.GetParent(path).Exists)
			{
				Console.WriteLine("Failed to save level; directory does not exist.");
				return false;
			}

			JObject json = GetSaveObject();
			File.WriteAllText(path, json.ToString());

			return true;
		}
		public JObject GetSaveObject()
		{
			JObject json = new JObject();
			json["Generator Type"] = (generator is LuaGenerator) ? (generator as LuaGenerator).ScriptName : generator.GetType().ToString();

			json["Generator Params"] = generator.GetSaveObject();

			json["Map Settings"] = new JObject();
			string[] settingNames = generator.Map.SettingNames;
			for (int i = 0; i < settingNames.Length; i++)
				json["Map Settings"][settingNames[i]] = generator.Map.GetSetting(settingNames[i]);

			return json;
		}
		public string LoadSettings(string path)
		{
			if (!File.Exists(path))
			{
				Console.WriteLine("Failed to load settings; file does not exist.");
				return "could not find config file";
			}

			string str = File.ReadAllText(path);

			JObject json;
			try
			{ json = JObject.Parse(str); }
			catch (Newtonsoft.Json.JsonReaderException)
			{ return "json error"; }
			// Fail if the json contents don't exactly match what is expected.
			if (json.Count != 3)
				return "invalid config";
			if (json["Generator Type"] == null || json["Generator Params"] == null || json["Map Settings"] == null)
				return "invalid config";

			ILevelGenerator oldGen = generator;
			Type t = Type.GetType(json["Generator Type"].ToString());

			if (t != null && typeof(ILevelGenerator).IsAssignableFrom(t))
				generator = Activator.CreateInstance(t) as ILevelGenerator;
			else
			{
				// Check if it is an existing lua script.
				if (luaPath == null)
					return "invalid generator type; lua disabled";

				// disallow ../ for security reasons
				string filePath = Path.Combine(luaPath, json["Generator Type"].ToString().Replace("../", ""));
				// Ensure file is in the luaPath directory, and that it exists.
				if (Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(luaPath)) && File.Exists(filePath))
				{
					LuaGenerator luaGenerator = new LuaGenerator();
					string result = luaGenerator.SetLua(File.ReadAllText(filePath));
					if (result != null)
						return "Lua error: " + result;
					generator = luaGenerator;
					luaGenerator.ScriptName = json["Generator Type"].ToString().Replace("../", "");
				}
				else
					return "invalid generator type; could not find lua file";
			}


			string ret = null;
			foreach (JProperty j in json["Generator Params"])
			{
				if (!SetParamOrSetting(j.Name, j.Value.ToString(Newtonsoft.Json.Formatting.None)))
				{
					ret = "could not set param '" + j.Name + "'";
					break;
				}
			}
			foreach (JProperty j in json["Map Settings"])
			{
				if (!Map.SetSetting(j.Name, j.Value.ToString()))
				{
					ret = "could not set setting '" + j.Name + "'";
					break;
				}
			}

			if (ret != null)
				generator = oldGen;
			return ret;
		}

		public bool SetParamOrSetting(string name, string value)
		{
			int paramIndex = Array.IndexOf(generator.GetParamNames(), name);
			if (paramIndex == -1) // Level setting
				return Map.SetSetting(name, value);
			else
			{
				if (!generator.SetParamValue(name, value))
				{
					Console.WriteLine("Value \'" + value + "\' could not be parsed.");
					return false;
				}
				else
					return true;
			}
		}

		public string GetParamOrSetting(string name)
		{
			name = name.ToLower();
			int paramIndex = Array.IndexOf(generator.GetParamNames(), name);
			if (paramIndex == -1) // Level setting
				return Map.GetSetting(name);
			else
				return generator.GetParamValue(name).ToString();
		}

		public async Task<int> GetLevelID(string title)
		{
			string result = await HttpGet("https://pr2hub.com/get_levels.php?token=" + login_token);
            if (result == notLoggedInMessage)
            {
                DetectedInvalidToken?.Invoke();
                return -2;
            }

			var collection = HttpUtility.ParseQueryString(result);

			string levelID = "";
			string index = "";
			bool foundMatch = false;
			for (int i = 0; i < collection.Count; i++)
			{
				if (collection.Keys[i].StartsWith("levelID"))
				{
					levelID = collection.GetValues(i)[0];
					index = collection.Keys[i].Substring("levelID".Length);
					if (foundMatch)
						break;
				}
				else if (collection.Keys[i].StartsWith("title") && collection.GetValues(i)[0] == title)
				{
					foundMatch = true;
					if (index == collection.Keys[i].Substring("title".Length))
						break;
				}
			}

			if (!foundMatch)
				return -1;
			else
				return int.Parse(levelID);
		}
		public async Task<string> DeleteLevel(int id)
		{
			string result = await PostLoadHTTP("https://pr2hub.com/delete_level.php", "level_id=" + id + "&token=" + login_token);
            if (result == notLoggedInMessage)
                DetectedInvalidToken?.Invoke();
            return result;
		}

        public async Task<bool> GetNewLoginToken()
        {
            string version = "24-dec-2013-v1", login_code = "eisjI1dHWG4vVTAtNjB0Xw";
            JObject loginData = new JObject();

            JObject server = new JObject();
            server["server_id"] = 1;
            loginData["user_name"] = username;
            loginData["user_pass"] = password;
            loginData["server"] = server;
            loginData["version"] = version;
            loginData["remember"] = true;
            loginData["domain"] = "cdn.jiggmin.com";
            loginData["login_code"] = login_code;

            string str = loginData.ToString();
            string loginData_encrypted = PR2_Cryptography.EncryptLoginData(str);
            loginData_encrypted = Uri.EscapeDataString(loginData_encrypted);

            string responseStr = await PostLoadHTTP("https://pr2hub.com/login.php", "i=" + loginData_encrypted + "&version=" + version + "&token=");
            JObject response = JObject.Parse(responseStr);
            if ((string)response["status"] != "success")
                return false;
            
            login_token = (string)response["token"];
            return true;
        }

        private async Task<string> PostLoadHTTP(string url, string postData)
		{
			byte[] byteArray = Encoding.UTF8.GetBytes(postData);

			// Create a request using a URL that can receive a post. 
			WebRequest request = WebRequest.Create(url);
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = byteArray.Length;

			// Get the request stream.
			Stream dataStream = await request.GetRequestStreamAsync();
			// Write the data to the request stream.
			await dataStream.WriteAsync(byteArray, 0, byteArray.Length);
			// Close the Stream object.
			dataStream.Close();

			// Get the response.
			WebResponse response = await request.GetResponseAsync();
			// Get the stream containing content returned by the server.
			dataStream = response.GetResponseStream();
			// Open the stream using a StreamReader for easy access.
			StreamReader reader = new StreamReader(dataStream);
			// Read the content.
			string responseFromServer = await reader.ReadToEndAsync();

			// Clean up the streams.
			reader.Close();
			dataStream.Close();
			response.Close();
			return responseFromServer;
		}
		private async Task<string> HttpGet(string url)
		{
			// Create a request using a URL that can receive a post. 
			WebRequest request = WebRequest.Create(url);

			// Get the response.
			WebResponse response = await request.GetResponseAsync();
			StreamReader reader = new StreamReader(response.GetResponseStream());
			// Read the content.
			string responseFromServer = await reader.ReadToEndAsync();

			// Clean up the streams.
			reader.Close();
			response.Close();
			return responseFromServer;
		}
	}
}
