using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

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

		public bool SaveSettings(string path, ILevelGenerator gen)
		{
			if (!Directory.GetParent(path).Exists)
			{
				Console.WriteLine("Failed to save level; directory does not exist.");
				return false;
			}

			string str = gen.GetSaveString();
			File.WriteAllText(path, str);

			return true;
		}
		public ILevelGenerator LoadSettings(string path)
		{
			if (!File.Exists(path))
            {
                Console.WriteLine("Failed to load settings; file does not exist.");
                return null;
            }

            string[] str = File.ReadAllText(path).Split('\n');

            Type t = Type.GetType(str[0]);
            ILevelGenerator generator = Activator.CreateInstance(t) as ILevelGenerator;

			for (int i = 1; i < generator.GetParamNames().Length + mapSettingNames.Length; i++)
			{
				string[] nameValue = str[i].Split(':');
				SetParamOrSetting(nameValue[0], nameValue[1]);
			}

			return generator;
		}

		public bool SetParamOrSetting(string name, string value)
		{
			name = name.ToLower();
			int paramIndex = Array.IndexOf(generator.GetParamNames(), name);
            if (paramIndex == -1) // Level setting
                return SetMapSetting(name, value);
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

		#region "Map Settings"
        private string[] mapSettingNames = new string[] { "Credits", "live", "Time", "Items", "Title", "Gravity", "Note", "Min_Rank", "Song", "hasPass", "Pass", "Mode", "Cowboy_Chance" };
        public bool SetMapSetting(string name, string value)
        {
            int intVal;
            double dVal;
            bool couldSet = true;
            switch (name.ToLower())
            {
                case "credits":
                    Map.credits = value;
                    return true;
                case "live":
                    if (int.TryParse(value, out intVal))
                        Map.live = intVal;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                    return true;
                case "time":
                    if (int.TryParse(value, out intVal))
                        Map.max_time = intVal;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                    return true;
                case "items":
                    Map.SetItems(value);
                    return true;
                case "title":
                    Map.title = value;
                    return true;
                case "gravity":
                    if (double.TryParse(value, out dVal))
                        Map.gravity = dVal;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                    return true;
                case "note":
                    Map.note = value;
                    return true;
                case "min_rank":
                    if (int.TryParse(value, out intVal))
                        Map.min_level = (sbyte)intVal;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                    return true;
                case "song":
                    if (int.TryParse(value, out intVal))
                        Map.song = intVal;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                    return true;
                case "haspass":
                    if (bool.TryParse(value, out Map.hasPass))
                        return true;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                case "pass":
                    Map.password = value;
                    return true;
                case "mode":
                    Map.gameMode = value;
                    return true;
                case "cowboy_chance":
                    if (int.TryParse(value, out intVal))
                        Map.cowboyChance = intVal;
                    else
                    {
                        couldSet = false;
                        break;
                    }
                    return true;

                default:
                    Console.Write("No setting \'" + name + "\' exits.");
                    break;
            }

            if (!couldSet)
                Console.Write("Value \'" + value + "\' could not be parsed.");
            return couldSet;
        }
        public string GetMapSetting(string name)
        {
            switch (name.ToLower())
            {
                case "credits":
                    return Map.credits;
                case "live":
                    return Map.live.ToString();
                case "time":
                    return Map.max_time.ToString();
                case "items":
                    return Map.GetItemsStr();
                case "title":
                    return Map.title;
                case "gravity":
                    return Map.gravity.ToString("R");
                case "note":
                    return Map.note;
                case "min_rank":
                    return Map.min_level.ToString();
                case "song":
                    return Map.song.ToString();
                case "haspass":
                    return Map.hasPass.ToString();
                case "pass":
                    return Map.password;
                case "mode":
                    return Map.gameMode;
                case "cowboy_chance":
                    return Map.cowboyChance.ToString();
            }

            return "Setting \'" + name + "\' does not exist.";
        }
        #endregion


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
