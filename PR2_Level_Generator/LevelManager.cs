using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace PR2_Level_Generator
{
	public class GenerationManager
	{
		public string login_token;
		public string username;
		public string luaPath = "lua";

		public ILevelGenerator generator;
		private MapLE Map { get => generator.Map; }

		public async Task<string> UploadLevel()
		{
			string oldNote = SetLevelNote();
			string LData = GetLevelData() + "&token=" + login_token;
			Map.SetSetting("note", oldNote);
			return await PostLoadHTTP("http://pr2hub.com/upload_level.php", LData);
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
			if (generator.GetParamNames().Contains("Seed"))
			{
				oldSeed = generator.GetParamValue("seed");
				generator.SetParamValue("seed", generator.LastSeed.ToString());
			}

			string oldNote = Map.GetSetting("note");
			Map.SetSetting("note", oldNote + "Gen: " + generator.GetType().ToString().Split('.').Last() + "\n" +
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
			json["Generator Type"] = generator.GetType().ToString();

			json["Generator Params"] = new JObject();
			string[] paramNames = generator.GetParamNames();
			for (int i = 0; i < paramNames.Length; i++)
				json["Generator Params"][paramNames[i]] = generator.GetParamValue(paramNames[i]);

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
			JObject json = JObject.Parse(str);
			// Fail if the json contents don't exactly match what is expected.
			if (json.Count != 3)
				return "invalid config";
			if (json["Generator Type"] == null || json["Generator Params"] == null || json["Map Settings"] == null)
				return "invalid config";

			Type t = Type.GetType(json["Generator Type"].ToString());
			ILevelGenerator oldGen = generator;
			if (t == null) // Check if it is an existing lua script.
			{
				string filePath = Path.Combine(luaPath, json["Generator Type"].ToString().Replace("../", "")); // disallow ../ for security reasons
				if (File.Exists(filePath))
				{
					LuaGenerator luaGenerator = new LuaGenerator();
					string result = luaGenerator.SetLua(File.ReadAllText(filePath));
					if (result != null)
						return "Lua error: " + result;
					generator = luaGenerator;
				}
				else
					return "could not find lua file";
			}
			else
				generator = Activator.CreateInstance(t) as ILevelGenerator;

			string ret = null;
			foreach (JProperty j in json["Generator Params"])
			{
				if (!SetParamOrSetting(j.Name, j.Value.ToString()))
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


		private async Task<string> PostLoadHTTP(string url, string postData) // temp public
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
	}
}
