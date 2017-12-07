using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Security.Cryptography;

namespace PR2_Level_Generator
{
	public class MapLE
	{
		// Normal (?) = 600, 540
		// PR2 = 540??, 500??
		// Video = 640, 480
		public MapLE(Type grid = null)
		{
			settings = new SortedDictionary<string, string>();
			settings.Add("song", ""); // random song
			settings.Add("min_level", "0");
			settings.Add("gravity", "1");
			settings.Add("items", ""); // always call SetItems, never change this directly
			SetItems("1`2`3`4`5`6`7`8`9");
			settings.Add("cowboyChance", "5");
			settings.Add("hasPass", "0");
			settings.Add("gameMode", "race");
			settings.Add("title", "title");
			settings.Add("credits", "PR2_Level_Generator");
			settings.Add("note", "");
			settings.Add("live", "0");
			settings.Add("max_time", "120");
			settings.Add("password", "");

			for (int i = 0; i < artCodes.Length; i++)
				artCodes[i] = new StringBuilder();

			if (grid == null)
				grid = typeof(LowMemoryBlockGrid);

			if (!(typeof(IBlockGrid).IsAssignableFrom(grid)))
				throw new ArgumentException("grid must be an IBlockGrid");
			this.grid = grid;
			blocks = Activator.CreateInstance(grid) as IBlockGrid;
		}

		private Type grid;
		private IBlockGrid blocks;
		public int BlockCount = 0;
		public int MaximumBlockCount = 100000;

		public uint BGC = 0xbbbbdd;
		public int bgID = -1;

		// Options
		#region "Map Settings"
		SortedDictionary<string, string> settings;
		public string[] SettingNames
		{
			get
			{
				string[] ret = new string[settings.Keys.Count];
				settings.Keys.CopyTo(ret, 0);
				return ret;
			}
		}
		/// <summary>
		/// PR2 uses a blank to mean random. Values that can't be parsed as integers mean no music, 0.
		/// Values higher than 15 (Prismatic) or less than 1 result in no music.
		/// I will return -1 for random.
		/// </summary>
		public int Song
		{
			get
			{
				if (string.IsNullOrEmpty(settings["song"]))
					return -1;
				else if (!int.TryParse(settings["song"], out int v))
					return 0;
				else
					return Math.Max(v, 0);
			}
		}
		public sbyte MinimumRank
		{
			get
			{
				double.TryParse(settings["min_level"], out double v);
				v = Math.Min(sbyte.MaxValue, Math.Max(sbyte.MinValue, v));
				return double.IsNaN(v) ? (sbyte)0 : (sbyte)v;
			}
		}
		public double Gravity
		{
			get
			{
				double.TryParse(settings["gravity"], out double v);
				v = Math.Min(99, Math.Max(-99, v));
				return double.IsNaN(v) ? 0 : v;
			}
		}
		public int[] avItems = new int[0];
		/// <summary>
		/// PR2 server converts unicode characters into a mess of UTF8 chars
		/// max_time is parsed to a double, and then limited to be between 0 and 9999.
		/// If it is 0, time is unlimited.
		/// Then, the time limit is truncated to an int.
		/// NaN and values over 0 but below 1 result in 0 second time limit, not infinite time.
		/// I will represent unlimited time as a -1. 0 means 0 second limit.
		/// </summary>
		public int TimeLimit
		{
			get
			{
				if (!double.TryParse(settings["max_time"], out double v))
					return 0;

				v = Math.Min(9999, Math.Max(v, 0));
				int intValue = (int)v;

				if (intValue == 0)
					return -1;
				else
					return intValue;
			}
		}
		public int CowboyChance
		{
			get
			{
				double.TryParse(settings["cowboyChance"], out double v);
				v = Math.Min(100, Math.Max(0, v));
				return double.IsNaN(v) ? 0 : (int)v;

			}
		}
		public bool HasPassword { get => settings["hasPass"].StartsWith('1'); }
		public bool useOldPass = false;
		public char GameMode { get => GameModes.PR2NameToID(settings["gameMode"]); }
		private int finish_count = 0;

		// Other options
		/// <summary>
		/// PR2 client: 0 for un-published, 1 for published
		/// PR2 server: Level is published if first character of live is a 1.
		/// </summary>
		/// <remarks></remarks>
		public bool Published { get => settings["live"].StartsWith('1'); }
		public string userName = "";

		public bool SetSetting(string name, string value)
		{
			if (settings.ContainsKey(name))
			{
				if (name == "items")
					SetItems(value);
				else if (name == "mode")
					SetMode(value);
				else
					settings[name] = value;

				return true;
			}

			Console.Write("No setting \'" + name + "\' exits.");
			return false;
		}
		public string GetSetting(string name)
		{
			if (settings.ContainsKey(name))
				return settings[name];
			else
				return "Setting \'" + name + "\' does not exist.";
		}

		private void SetItems(string ItemStr)
		{
			string[] avItemStr = ItemStr.Split('`');
			List<int> itemList = new List<int>();

			for (int i = 0; i < avItemStr.Length; i++)
			{
				int id = Item.PR2NameToID(avItemStr[i]);
				if (id != Item.NULL)
					itemList.Add(id);
			}

			avItems = itemList.ToArray();
			settings["items"] = ItemStr;
		}
		private void SetMode(string value)
		{
			switch (value)
			{
				case "r":
					value = "race";
					break;
				case "d":
					value = "deathmatch";
					break;
				case "o":
					value = "objective";
					break;
				case "e":
					value = "egg";
					break;
			}
			settings["gameMode"] = value;
		}
		#endregion

		// Art
		public StringBuilder[] artCodes = new StringBuilder[10];
		public void SetArtLayerString(int layerID, string str)
		{
			artCodes[layerID] = new StringBuilder(str);
		}

		// Options that are not in PR2
		public bool mapless = false;
		public bool passImpossible = false;

		// Players
		public Point[] playerStarts = new Point[4];

		// Add a block to the level (increase BlockCount)
		public void AddBlock(int x, int y, int t)
		{
			BlockCount++;
			blocks.PlaceBlock(x, y, t);

			// Player starts
			if (t >= BlockID.P1 && t <= BlockID.P4)
			{
				int psID = t - BlockID.P1;
				playerStarts[psID] = new Point(x * 30 + 15, y * 30 + 15);
			}
			// Finish count for objective
			else if (t == BlockID.Finish)
				finish_count += 1;

		}
		public void ReplaceBlock(int x, int y, int t)
		{
			Block b = blocks.GetBlock(x, y);
			if (b.T != BlockID.BLANK)
				blocks.GetBlock(x, y).T = t;
			else
				AddBlock(x, y, t);
		}
		// Delete a block (Decreases BlockCount)
		public void DeleteBlock(int x, int y)
		{
			Block b = GetBlock(x, y);
			if (b.T != BlockID.BLANK)
			{
				if (b.previous != null)
					b.previous = b.next;
				if (b.next != null)
					b.next = b.previous;

				blocks.PlaceBlock(x, y, BlockID.BLANK);
				BlockCount--;
			}
		}
		private Block DataToBlock(string bData)
		{
			string[] p = bData.Split(';');
			Block ret = new Block();
			if (p.Length > 0 && p[0] != "")
				ret.X = Convert.ToInt32(p[0]);
			if (p.Length > 1 && p[1] != "")
				ret.Y = Convert.ToInt32(p[1]);
			if (p.Length > 2 && p[2] != "")
				ret.T = Convert.ToInt32(p[2]);
			else
				ret.T = -1;

			return ret;
		}

		// Clear ALL blocks
		public void ClearBlocks()
		{
			blocks = Activator.CreateInstance(grid) as IBlockGrid;

			BlockCount = 0;
		}
		public void ClearType(int T)
		{
			Block cBlock = blocks.FirstBlock;
			while (cBlock != null)
			{
				if (cBlock.T == T)
					DeleteBlock(cBlock.X, cBlock.Y);

				cBlock = cBlock.next;
			}
		}

		private int lastStampX = 0;
		private int lastStampY = 0;
		char[] specialTextChars = new char[] { ';', ',', '`', '&', '#' };
		public void PlaceText(string text, double x, double y, int color = 0, double width = 100, double height = 100)
		{
			if (text == null)
				text = "";
			for (int i = 0; i < specialTextChars.Length; i++)
				text = text.Replace(specialTextChars[i].ToString(), "#" + (int)specialTextChars[i]);

			if (artCodes[0].Length > 0)
				artCodes[0].Append(",");

			x -= lastStampX;
			y -= lastStampY;
			artCodes[0].Append(x + ";" + y + ";t;" + text + ";" + color + ";" + width + ";" + height);
			lastStampX += (int)x;
			lastStampY += (int)y;
		}

		// Check if a block exists
		public bool BlockExists(int x, int y)
		{
			return blocks.GetBlock(x, y).T != BlockID.BLANK;
		}
		// get Block by it's real X and Y
		public Block GetBlock(int x, int y)
		{
			return new Block() { X = x, Y = y, T = blocks.GetBlock(x, y).T };
		}

		// get the level's data
		public string GetData(bool keepArt = true)
		{
			string data = GetDataParam(keepArt);
			string stringToHash = settings["title"] + userName.ToLower() + data + "[salt]";
			byte[] bytesToHash = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(stringToHash.Replace('+', ' ')));
			byte[] bytesHashed = (new MD5CryptoServiceProvider()).ComputeHash(bytesToHash);
			string upload_hash = BitConverter.ToString(bytesHashed).Replace("-", "").ToLower();

			// Put all the data bits into one string
			StringBuilder LData = new StringBuilder();
			foreach (KeyValuePair<string, string> kvp in settings)
			{
				LData.Append(kvp.Key + "=" + kvp.Value + "&");
			}
			LData.Append("hash=" + upload_hash + "&data=" + data);

			// Password
			if (HasPassword)
			{
				string passHash = "";
				if (passImpossible)
					passHash = "383";
				else if (!useOldPass)
				{
					stringToHash = settings["password"] + "[salt]";
					bytesToHash = Encoding.UTF8.GetBytes(stringToHash);
					bytesHashed = (new MD5CryptoServiceProvider()).ComputeHash(bytesToHash);
					passHash = BitConverter.ToString(bytesHashed).Replace("-", "").ToLower();
				}
				LData.Append("&passHash=" + passHash);
			}

			return LData.ToString();
		}
		// The 'data' parameter; used to calculate hash
		public string GetDataParam(bool keepArt = true)
		{
			string BlockD = GetBlockData();
			if (mapless)
				BlockD += ",100000000;-100000000;0";

			StringBuilder Data = new StringBuilder("m3`" + Convert.ToString(BGC, 16) + "`" + BlockD + "`");
			if (keepArt)
			{
				// Layers 1-3
				for (int i = 0; i < 6; i++)
				{
					Data.Append(artCodes[i]);
					Data.Append("`");
				}
				Data.Append((bgID == -1 ? -1 : bgID + 200) + "`"); // I think. TODO: Verify.

				// Layers 00 and 0
				for (int i = 6; i < 10; i++)
				{
					Data.Append(artCodes[i]);
					Data.Append("`");
				}
			}
			else
			{
				Data.Append("``````");
				Data.Append(bgID == -1 ? -1 : bgID + 200);
				Data.Append("````");
			}

			return Data.ToString();
		}
		private string GetBlockData()
		{
			StringBuilder ret = new StringBuilder(BlockCount * 5);
			Block cBlock = blocks.FirstBlock;
			int lastX = 0;
			int lastY = 0;
			int lastT = 0;
			while (cBlock != null)
			{
				ret.Append((cBlock.X - lastX) + ";" + (cBlock.Y - lastY));
				if (cBlock.T != lastT)
					ret.Append(";" + cBlock.T);
				ret.Append(",");

				lastX = cBlock.X;
				lastY = cBlock.Y;
				lastT = cBlock.T;

				cBlock = cBlock.next;
			}
			if (ret.Length > 0)
				ret.Remove(ret.Length - 1, 1);

			return ret.ToString();
		}
		public void LoadLevel(string LvlData)
		{
			if (!LvlData.Substring(LvlData.Length - 32).Contains("&") && !LvlData.Substring(LvlData.Length - 32).Contains("`"))
				LvlData = LvlData.Substring(0, LvlData.Length - 32); // Remove hash at end of level code, if present

			string[] Parts = LvlData.Split('&');

			string levelData = "";
			for (int i = 0; i < Parts.Length; i++)
			{
				int e = Parts[i].IndexOf('=');
				string LD = Parts[i].Substring(e + 1);
				string pName = Parts[i].Substring(0, e);

				if (!SetSetting(pName, LD))
				{
					if (pName == "data")
						levelData = LD;
					else if (pName == "has_pass") // in uploads it is hasPass, in downloads it is has_pass
						settings["hasPass"] = LD;
				}
			}
			useOldPass = true;
			passImpossible = false;
			settings["password"] = "";

			string[] Codes = levelData.Split('`');
			// Set arts 1-3
			for (int i = 0; i < 6; i++)
			{
				artCodes[i] = new StringBuilder(Codes[i + 3]);
			}
			if (Codes[1].Length >= 6)
				BGC = Convert.ToUInt32(Codes[1], 16);
			else
				BGC = 0x000000;

			if (Codes[9] == "")
				bgID = -1;
			else
				bgID = Convert.ToInt32(Codes[9]);
			// Art 0 and 00
			if (Codes.Length > 10)
			{
				for (int i = 6; i < 10; i++)
					artCodes[i] = new StringBuilder(Codes[i + 4]);
			}

			// get blocks from data
			LoadBlocks(Codes[2]);
		}
		private void LoadBlocks(string bData)
		{
			playerStarts = new Point[] { new Point(), new Point(), new Point(), new Point() };
			// get blocks from data
			blocks = Activator.CreateInstance(grid) as IBlockGrid;
			BlockCount = 0;
			finish_count = 0;

			string[] BlocksC = bData.Split(',');
			int LpX = 0;
			int LpY = 0;
			int LpT = 0;
			// Place blocks
			for (int i = 0; i < BlocksC.Length; i++)
			{
				// get X, Y, and Type
				string[] BCS = BlocksC[i].Split(';');
				if (BCS.Length > 2)
				{
					if (BCS[2] == "")
						BCS[2] = "0"; // Blank string means 0
					LpT = Convert.ToInt32(BCS[2]);
				}
				if (BCS.Length > 1)
				{
					if (BCS[1] == "")
						BCS[1] = "0";
					LpY += Convert.ToInt32(BCS[1]);
				}
				if (BCS[0] == "")
					BCS[0] = "0";
				LpX += Convert.ToInt32(BCS[0]);

				AddBlock(LpX, LpY, LpT);
			}
		}
	}

	public static class BlockID
	{
		public const int BLANK = -1;
		public const int BB0 = 0;
		public const int BB1 = 1;
		public const int BB2 = 2;
		public const int BB3 = 3;
		public const int Brick = 4;
		public const int Down = 5;
		public const int Up = 6;
		public const int Left = 7;
		public const int Right = 8;
		public const int Mine = 9;
		public const int Item = 10;
		public const int P1 = 11;
		public const int P2 = 12;
		public const int P3 = 13;
		public const int P4 = 14;
		public const int Ice = 15;
		public const int Finish = 16;
		public const int Crumble = 17;
		public const int Vanish = 18;
		public const int Move = 19;
		public const int Water = 20;
		public const int GravRight = 21;
		public const int GravLeft = 22;
		public const int Push = 23;
		public const int Net = 24;
		public const int InfItem = 25;
		public const int Happy = 26;
		public const int Sad = 27;
		public const int Heart = 28;
		public const int Time = 29;
		public const int Egg = 30;
		public const int Invisible = 88; // Obviously not in real PR2 LE, but it works in real PR2 levels. [Not anymore]
	}

	public class Block
	{
		public int X = 0;
		public int Y = 0;
		public int T = BlockID.BB0;

		public Block previous;
		public Block next;

		public Block Clone()
		{
			Block Cln = new Block();
			Cln.X = X;
			Cln.Y = Y;
			Cln.T = T;
			return Cln;
		}

		private static SortedSet<int> nonSolidTypes = new SortedSet<int>(new int[] { BlockID.BLANK, BlockID.P1, BlockID.P2, BlockID.P3,
		  BlockID.P4, BlockID.Water, BlockID.Net, BlockID.Egg});
		public bool IsSolid()
		{
			return !nonSolidTypes.Contains(T);
		}

		private static SortedSet<int> safeTypes = new SortedSet<int>(new int[] { 0, 1, 2, 3, 5, 6, 7, 8, // basic and arrow blocks
		  BlockID.Item, BlockID.Ice, BlockID.Finish, BlockID.GravRight, BlockID.GravLeft, BlockID.InfItem,
		  BlockID.Happy, BlockID.Sad, BlockID.Heart, BlockID.Time});
		public bool Safe
		{
			get { return safeTypes.Contains(T); }
		}

		private int GetArrowDir(int rot)
		{
			int ret = T;
			if (rot == 90)
			{
				if (T == 5)
					ret = 7;
				else if (T == 6)
					ret = 8;
				else if (T == 7)
					ret = 6;
				else if (T == 8)
					ret = 5;
			}
			else if (rot == 180 || rot == -180)
			{
				if (T == 5)
					ret = 6;
				else if (T == 6)
					ret = 5;
				else if (T == 7)
					ret = 8;
				else if (T == 8)
					ret = 7;
			}
			else if (rot == -90)
			{
				if (T == 5)
					ret = 8;
				else if (T == 6)
					ret = 7;
				else if (T == 7)
					ret = 5;
				else if (T == 8)
					ret = 6;
			}
			return ret;
		}
	}

	public static class Item
	{
		public const int NULL = 0;
		public const int LASERGUN = 1;
		public const int MINE = 2;
		public const int LIGHTNING = 3;
		public const int TELEPORT = 4;
		public const int SUPERJUMP = 5;
		public const int JETPACK = 6;
		public const int SPEEDY = 7;
		public const int SWORD = 8;
		public const int ICEWAVE = 9;

		private static SortedDictionary<string, int> itemIDs;
		public static int PR2NameToID(string name)
		{
			if (itemIDs == null)
			{
				itemIDs = new SortedDictionary<string, int>();
				itemIDs.Add("Laser Gun", LASERGUN);
				itemIDs.Add("Mine", MINE);
				itemIDs.Add("Lightning", LIGHTNING);
				itemIDs.Add("Teleport", TELEPORT);
				itemIDs.Add("Super Jump", SUPERJUMP);
				itemIDs.Add("Jet Pack", JETPACK);
				itemIDs.Add("Speed", SPEEDY);
				itemIDs.Add("Sword", SWORD);
				itemIDs.Add("Ice Wave", ICEWAVE);
			}

			return itemIDs.ContainsKey(name) ? itemIDs[name] : NULL;
		}
	}

	public static class GameModes
	{
		public const char RACE = 'r';
		public const char DEATHMATCH = 'd';
		public const char EGG = 'e';
		public const char OBJECTIVE = 'o';

		private static SortedDictionary<string, char> modeIDs;
		public static char PR2NameToID(string name)
		{
			if (modeIDs == null)
			{
				modeIDs = new SortedDictionary<string, char>();
				modeIDs.Add("race", RACE);
				modeIDs.Add("deathmatch", DEATHMATCH);
				modeIDs.Add("objective", OBJECTIVE);
				modeIDs.Add("egg", EGG);
			}

			return modeIDs.ContainsKey(name) ? modeIDs[name] : RACE;
		}
	}
}
