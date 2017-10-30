using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

using Newtonsoft.Json.Linq;

namespace PR2_Level_Generator
{
	public class GenerationManager
	{
		public string login_token;
		public string username;

		public ILevelGenerator generator;
		private MapLE Map { get => generator.Map; }

		public string UploadLevel()
		{
			string LData = GetLevelData() + "&token=" + login_token;
			return PostLoadHTTP("http://pr2hub.com/upload_level.php", LData);
		}
		public bool SaveLevel(string path)
		{
			if (!Directory.GetParent(path).Exists)
			{
				Console.WriteLine("Failed to save level; directory does not exist.");
				return false;
			}

			string LData = GetLevelData();
			File.WriteAllText(path, LData);

			return true;
		}
		private string GetLevelData()
		{
			Map.userName = username;
			return Map.GetData();
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
		public ILevelGenerator LoadSettings(string path)
		{
			if (!File.Exists(path))
			{
				Console.WriteLine("Failed to load settings; file does not exist.");
				return null;
			}

			string str = File.ReadAllText(path);
			JObject json = JObject.Parse(str);

			Type t = Type.GetType(json["Generator Type"].ToString());
			ILevelGenerator generator = Activator.CreateInstance(t) as ILevelGenerator;

			foreach (JProperty j in json["Generator Params"])
				SetParamOrSetting(j.Name, j.Value.ToString());
			foreach (JProperty j in json["Map Settings"])
				SetParamOrSetting(j.Name, j.Value.ToString());

			return generator;
		}

		public bool SetParamOrSetting(string name, string value)
		{
			int paramIndex = Array.IndexOf(generator.GetParamNames(), name);
			if (paramIndex == -1) // Level setting
				return Map.SetSetting(name, value);
			else
			{
				double val;
				if (double.TryParse(value, out val))
					generator.SetParamValue(name, val);
				else
				{
					Console.WriteLine("Value \'" + value + "\' could not be parsed.");
					return false;
				}
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


		private string PostLoadHTTP(string url, string postData)
		{
			// Create a request using a URL that can receive a post. 
			WebRequest request = WebRequest.Create(url);
			// Set the Method property of the request to POST.
			request.Method = "POST";
			// Create POST data and convert it to a byte array.
			byte[] byteArray = Encoding.UTF8.GetBytes(postData);
			// Set the ContentType property of the WebRequest.
			request.ContentType = "application/x-www-form-urlencoded";
			// Set the ContentLength property of the WebRequest.
			request.ContentLength = byteArray.Length;
			// Get the request stream.
			Stream dataStream = request.GetRequestStream();
			// Write the data to the request stream.
			dataStream.Write(byteArray, 0, byteArray.Length);
			// Close the Stream object.
			dataStream.Close();
			// Get the response.
			WebResponse response = null;
			try
			{
				response = request.GetResponse();
			}
			catch (Exception ex)
			{
				if (ex is Exception)
					ex = null;
			}
			// Get the stream containing content returned by the server.
			dataStream = response.GetResponseStream();
			// Open the stream using a StreamReader for easy access.
			StreamReader reader = new StreamReader(dataStream);
			// Read the content.
			string responseFromServer = reader.ReadToEnd();
			// Clean up the streams.
			reader.Close();
			dataStream.Close();
			response.Close();
			return responseFromServer;
		}
	}
}
