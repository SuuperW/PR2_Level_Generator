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
		public MapLE()
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
		}

		// Block array(s)
		public List<List<Block>> Blocks = new List<List<Block>>();
		int XStart = 0;
		List<int> YStart = new List<int>();
		private Block firstBlock;
		private Block lastBlock;
		public int BlockCount = 0;

		int MinX = 0;
		int MaxX = 0;
		int MinY = 0;
		public int MaxY = 0;

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
		public int TimeLimit {
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

        public bool SetMapSetting(string name, string value)
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
        public string GetMapSetting(string name)
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
		public string[] artCodes = new string[10];

		// Options that are not in PR2
		public bool mapless = false;
		public bool passImpossible = false;

		// Players
		public Point[] playerStarts = new Point[4];

		// Add a block to the level (increase BlockCount)
		public void AddBlock(int X, int Y, int T)
		{
			CreateIndex(X, Y);
			// So I will not have to type X - XStart over and over
			int BX = X - XStart;
			int BY = Y - YStart[BX];

			// Increase BlockCount
			BlockCount += 1;

			// Add block
			Block added = new Block();
			Blocks[BX][BY] = added;
			// Now to set the block! :D
			added.X = X;
			added.Y = Y;
			added.T = T;
			added.course = this;
			if (firstBlock == null)
				firstBlock = added;
			else
			{
				added.previous = lastBlock;
				added.previous.next = added;
			}
			lastBlock = added;

			// Check if this is a new min/max
			if (X < MinX)
				MinX = X;
			else if (X > MaxX)
				MaxX = X;
			if (Y < MinY)
				MinY = Y;
			else if (Y > MaxY)
				MaxY = Y;

			// Player starts
			if (T >= BlockID.P1 && T <= BlockID.P4)
			{
				int psID = T - BlockID.P1;
				playerStarts[psID] = new Point(X * 30 + 15, Y * 30 + 15);
			}
			// Finish count for objective
			if (T == BlockID.Finish)
				finish_count += 1;
		}
		public void ReplaceBlock(int X, int Y, int T)
		{
			Block cBlock = GetBlock(X, Y);
			if (cBlock.T == 99)
				AddBlock(X, Y, T);
			else
				cBlock.T = T;
		}
		// Delete a block (Decreases BlockCount)
		public void DeleteBlock(int X, int Y)
		{
			// Make sure it exists before doing anything
			if (!BlockExists(X, Y, false))
				return;

			Block delBlock;
			X = X - XStart;
			Y = Y - YStart[X];
			delBlock = Blocks[X][Y];
			if (delBlock.previous != null)
				delBlock.previous.next = delBlock.next;
			else
				firstBlock = delBlock.next;
			if (delBlock.next != null)
				delBlock.next.previous = delBlock.previous;
			else
				lastBlock = delBlock.previous;
			Blocks[X][Y] = null; // Set to nothing
								 //list_blocks.RemoveAt(delBlock.mapID);

			// if it is at YStart or at the end of the Y's, change array.
			if (Y == 0)
			{
				// if this is the ONLY Y, array = nothing
				if (Blocks[X].Count == 1)
				{
					Blocks[X] = null;
				}
				else
				{
					Blocks[X].RemoveAt(0);
					// Remove all nothings from the array until the first block
					if (Blocks[X][0] == null)
					{
						do
						{
							Blocks[X].RemoveAt(0);
						} while (Blocks[X][0] == null);
					}
					// This is the new YStart.
					YStart[X] = Blocks[X][0].Y;
				}
			}
			else if (Y == Blocks[X].Count - 1)
			{
				Blocks[X].RemoveAt(Blocks[X].Count - 1);
				// Remove all nothings from the array until the last block
				if (Blocks[X][Blocks[X].Count - 1] == null)
				{
					do
					{
						Blocks[X].RemoveAt(Blocks[X].Count - 1);
					} while (Blocks[X][Blocks[X].Count - 1] == null);
				}
			}
			// if this X is now null, and
			// if we are at XStart or the end of the X's, change array.
			if (Blocks[X] == null)
			{
				if (X == 0)
				{
					// if this is the ONLY X, reset Lists
					if (Blocks.Count == 1)
					{
						Blocks = new List<List<Block>>();
						YStart = new List<int>();
					}
					else
					{
						// Remove all nothings from the list until the first 
						do
						{
							Blocks.RemoveAt(0);
							YStart.RemoveAt(0);
						} while (Blocks[0] == null);
						// new XStart
						XStart = Blocks[0][0].X;
					}
				}
				else if (X == Blocks.Count - 1)
				{
					// Remove all nothings from the list until the last block
					do
					{
						YStart.RemoveAt(Blocks.Count - 1);
						Blocks.RemoveAt(Blocks.Count - 1);
					} while (Blocks[Blocks.Count - 1] == null);
				}
			}

			BlockCount -= 1;
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
			Blocks.Clear();
			firstBlock = null;
			lastBlock = null;

			XStart = 0;
			List<int> YStart = new List<int>();
			BlockCount = 0;

			MinX = 0;
			MaxX = 0;
			MinY = 0;
			MaxY = 0;
		}
		public void ClearType(int T)
		{
			Block cBlock = firstBlock;
			while (cBlock != null)
			{
				if (cBlock.T == T)
					DeleteBlock(cBlock.X, cBlock.Y);

				cBlock = cBlock.next;
			}
		}

		// Move a block - do not delete/place. (placing can change off-course bounds)
		public void MoveBlock(int X, int Y, int MovX, int MovY)
		{
			Block Bloc = GetBlock(X, Y);
			if (Bloc.T != 99)
			{
				if (!BlockExists(X + MovX, Y + MovY, false))
				{
					// Make sure the index it's moving to exists before moving it
					CreateIndex(X + MovX, Y + MovY);
					// Get blocks at old position and at new position
					Block newB = Blocks[X - XStart][Y - YStart[X - XStart]];
					// MOVE IT
					newB.X += MovX;
					newB.Y += MovY;
					Blocks[newB.X - XStart][newB.Y - YStart[newB.X - XStart]] = newB;
					Blocks[X - XStart][Y - YStart[X - XStart]] = null;
				}
			}
		}

		// Make sure a given index exists in the Blocks list
		private void CreateIndex(int X, int Y)
		{
			int BX = X - XStart;
			// if ( this is for the very first block
			if (Blocks.Count == 0 || Blocks[0] == null)
			{
				XStart = X;
				BX = X - XStart;
				Blocks = new List<List<Block>>();
				Blocks.Add(new List<Block>());
				YStart = new List<int>();
				YStart.Add(Y);
			}
			// Check if X is out of bounds
			if (BX < 0)
			{ // new X is less than XStart
				for (int i = BX; i < 0; i++)
				{
					Blocks.Insert(0, null);
					YStart.Insert(0, 0);
				}
				XStart = X;
			}
			else if (BX > Blocks.Count - 1)
			{ // X is higher than max X in the array
				for (int i = Blocks.Count; i <= BX; i++)
				{
					Blocks.Add(null);
					YStart.Add(Y);
				}
			}
			// BX could have changed - add a BY
			BX = X - XStart;
			int BY = Y - YStart[BX];
			// Make sure list is not nothing
			if (Blocks[BX] == null)
			{
				Blocks[BX] = new List<Block>();
				YStart[BX] = Y;
				BY = 0;
			}
			// Check if Y is out of bounds
			if (BY < 0)
			{ // new Y is less than YStart
				for (int i = BY; i < 0; i++)
					Blocks[BX].Insert(0, null);
				YStart[BX] = Y;
			}
			else if (BY > Blocks[BX].Count - 1)
			{ // Y is higher than max Y in the array
				for (int i = Blocks[BX].Count; i <= BY; i++)
				{
					Blocks[BX].Add(null);
				}
			}
		}
		// Check if a block exists
		public bool BlockExists(int X, int Y, bool IndexR = false)
		{
			// Relative index, or straight X, Y coordinates?
			if (IndexR == false)
			{
				X -= XStart;
			}
			// See if that X exists.
			if (X < 0 || X >= Blocks.Count)
			{
				// It does not.
				return false;
			}
			else if (Blocks[X] == null)
			{
				// if it's in bounds, the array can still be a nothing
				return false;
			}
			// Y index, and Y exist
			if (IndexR == false)
			{
				Y -= YStart[X];
			}
			if (Y < 0 || Y >= Blocks[X].Count)
			{
				return false;
			}
			else if (Blocks[X][Y] == null)
			{
				return false;
			}
			// It exists if it doesn't not.
			return true;
		}
		// get Block by it's real X and Y
		public Block GetBlock(int LocX, int LocY)
		{
			Block TheBlock;
			// if ( the block exists, return it. Otherwise return a 99
			if (BlockExists(LocX, LocY, false))
			{
				TheBlock = Blocks[LocX - XStart][LocY - YStart[LocX - XStart]];
			}
			else
			{
				TheBlock = new Block();
				TheBlock.X = LocX;
				TheBlock.Y = LocY;
				TheBlock.T = 99;
			}
			return TheBlock;
		}

		// get the level's data
		public string GetData(bool keepArt = true)
		{
			string data = GetDataParam(keepArt);
			string stringToHash = settings["title"] + userName.ToLower() + data + "[salt]";
			byte[] bytesToHash = Encoding.UTF8.GetBytes(stringToHash);
			byte[] bytesHashed = (new MD5CryptoServiceProvider()).ComputeHash(bytesToHash);
			string upload_hash = BitConverter.ToString(bytesHashed).Replace("-", "").ToLower();

			string Items = GetItemsStr();

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

			string Data = "m3`" + Convert.ToString(BGC, 16) + "`" + BlockD + "`";
			if (keepArt)
			{
				// Layers 1-3
				for (int i = 0; i < 6; i++)
					Data += artCodes[i] + "`";
				Data += (bgID == -1 ? -1 : bgID + 200) + "`"; ; // I think. TODO: Verify.

				// Layers 00 and 0
				for (int i = 6; i < 10; i++)
					Data += artCodes[i] + "`";
			}
			else
			{
				Data += "``````";
				Data += bgID == -1 ? -1 : bgID + 200;
				Data += "````";
			}

			return Data;
		}
		private string GetBlockData()
		{
			StringBuilder ret = new StringBuilder("");
			Block cBlock = firstBlock;
			int lastX = 0;
			int lastY = 0;
			int lastT = 0;
			while (cBlock != null)
			{
				ret.Append(cBlock.X - lastX);
				ret.Append(";");
				ret.Append(cBlock.Y - lastY);
				if (cBlock.T != lastT)
					ret.Append(";" + cBlock.T);

				lastX = cBlock.X;
				lastY = cBlock.Y;
				lastT = cBlock.T;

				ret.Append(",");
				cBlock = cBlock.next;
			}
			ret.Remove(ret.Length - 1, 1);

			return ret.ToString();
		}
		public string GetItemsStr()
		{
			string ret = "";
			for (int i = 0; i < avItems.Length; i++)
				ret += avItems[i] + "`";
			if (ret.Length > 0)
				ret = ret.Substring(0, ret.Length - 1);

			return ret;
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

				if (!SetMapSetting(pName, LD))
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
				artCodes[i] = Codes[i + 3];
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
					artCodes[i] = Codes[i + 4];
			}

			// get blocks from data
			LoadBlocks(Codes[2]);
		}
		private void LoadBlocks(string bData)
		{
			playerStarts = new Point[] { new Point(), new Point(), new Point(), new Point() };
			// get blocks from data
			Blocks = new List<List<Block>>();
			XStart = 0;
			YStart = new List<int>();
			BlockCount = 0;
			MinX = 0;
			MaxX = 0;
			MinY = 0;
			MaxY = 0;
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

		public MapLE course;
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

		public bool IsSolid()
		{
			if (T != BlockID.Net && T != BlockID.Water && T < BlockID.Egg && (T < BlockID.P1 || T > BlockID.P4))
				return true;
			else
				return false;
		}

		public bool Safe
		{
			get
			{
				if ((T < 9 && T != 4) || T == 10 || T == 15 || T == 16 || T == 21 || T == 22 || (T > 24 && T != 99))
				{
					return true;
				}
				else
				{
					return false;
				}
			}
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
				modeIDs.Add("race", RACE);
				modeIDs.Add("deathmatch", DEATHMATCH);
				modeIDs.Add("objective", OBJECTIVE);
				modeIDs.Add("egg", EGG);
			}

			return modeIDs.ContainsKey(name) ? modeIDs[name] : RACE;
		}
	}
}
