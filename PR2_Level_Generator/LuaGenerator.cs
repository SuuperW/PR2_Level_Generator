using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

using MoonSharp.Interpreter;

using Newtonsoft.Json.Linq;

namespace PR2_Level_Generator
{
	public class LuaGenerator : ILevelGenerator
	{
		public MapLE Map { get; set; }
		public int LastSeed { get; private set; } = 0;

		private Script script;
		public string ScriptName;
		private string _luaScript;
		public string LuaScript { get => _luaScript; }
		public string SetLua(string value)
		{
			_luaScript = value;
			Map = new MapLE();

			script = new Script(CoreModules.Preset_HardSandbox);
			ExposeFunctionsaAndTables();
			RemoveFunctionsWithCallbacks();
			try
			{
				// Wrap the code in a function, so that MoonSharp can use it as a coroutine.
				string codeToRun = "function Initialize() " + _luaScript + "\n" +
					"_G.Generate = Generate;\n_G.params = params;\nend";
				script.DoString(codeToRun);
				DynValue initFunction = script.Globals.Get("Initialize");
				Coroutine coroutine = script.CreateCoroutine(initFunction).Coroutine;
				coroutine.AutoYieldCounter = 100000;

				DynValue result = coroutine.Resume();
				if (result.Type == DataType.YieldRequest)
					return "initialization timed out";

				// Random seed
				if (Parameters != null && !GetParamNames().Contains("seed"))
					Parameters["seed"] = 0;
			}
			catch (InterpreterException ex) // base exception type for MoonSharp
			{
				SetLua("function Generate()\nend");
				return "Lua error: " + ex.DecoratedMessage;
			}

			return null;
		}
		private void RemoveFunctionsWithCallbacks()
		{
			// Prevent users from hanging the generator with an infinite loop.
			// If a coroutine calls a MoonSharp function which then calls a Lua function, it cannot AutoYield.
			// So, remove the ability to use these callbacks.

			script.Globals["load"] = DynValue.Nil;

			DynValue sort = script.Globals.Get("table").Table.Get("sort");
			script.Globals.Get("table").Table["sort"] = (Action<Table>)((t) => { script.Call(sort, t); });

			DynValue gsub = script.Globals.Get("string").Table.Get("gsub");
			script.Globals.Get("string").Table["gsub"] = (Func<DynValue, DynValue, DynValue, DynValue, DynValue>)((s, p, r, n) =>
			{
				if (p.Function == null) return script.Call(gsub, s, p, r, n);
				else return null;
			});
		}

		private Table Parameters => script.Globals.Get("params").Table;

		public LuaGenerator()
		{
			Script.WarmUp();
			SetLua("function Generate()\nend");
		}
		public LuaGenerator(string filePath)
		{
			Script.WarmUp();
			SetLua(File.ReadAllText(filePath));
		}

		#region "lua functions"
		private void ExposeFunctionsaAndTables()
		{
			script.Globals["PlaceBlock"] = (Action<int, int, int>)Map.AddBlock;
			script.Globals["GetBlock"] = (Func<int, int, int>)GetBlock;
			script.Globals["RemoveAllBlocks"] = (Action<int>)RemoveAllBlocks;

			script.Globals["FillRectangle"] = (Action<int, int, int, int, int[]>)FillRectangle;
			script.Globals["PlaceRectangle"] = (Action<int, int, int, int, int[]>)PlaceRectangle;
			script.Globals["ClearRectangle"] = (Action<int, int, int, int>)ClearRectangle;

			script.Globals["PlaceText"] = (Action<string, double, double, int, double, double>)Map.PlaceText;
			script.Globals["ColorFromRGB"] = (Func<int, int, int, int>)ColorFromRGB;

			script.Globals["BlockID"] = CreateBlockIDTable();
		}
		private Table CreateBlockIDTable()
		{
			Table ret = new Table(script);
			ret.Set("Basic1", DynValue.NewNumber(BlockID.BB0));
			ret.Set("Basic2", DynValue.NewNumber(BlockID.BB1));
			ret.Set("Basic3", DynValue.NewNumber(BlockID.BB2));
			ret.Set("Basic4", DynValue.NewNumber(BlockID.BB3));
			ret.Set("Brick", DynValue.NewNumber(4));
			ret.Set("Down", DynValue.NewNumber(5));
			ret.Set("Up", DynValue.NewNumber(6));
			ret.Set("Left", DynValue.NewNumber(7));
			ret.Set("Right", DynValue.NewNumber(8));
			ret.Set("Mine", DynValue.NewNumber(9));
			ret.Set("Item", DynValue.NewNumber(10));
			ret.Set("Player1", DynValue.NewNumber(11));
			ret.Set("Player2", DynValue.NewNumber(12));
			ret.Set("Player3", DynValue.NewNumber(13));
			ret.Set("Player4", DynValue.NewNumber(14));
			ret.Set("Ice", DynValue.NewNumber(15));
			ret.Set("Finish", DynValue.NewNumber(16));
			ret.Set("Crumble", DynValue.NewNumber(17));
			ret.Set("Vanish", DynValue.NewNumber(18));
			ret.Set("Move", DynValue.NewNumber(19));
			ret.Set("Water", DynValue.NewNumber(20));
			ret.Set("GravRight", DynValue.NewNumber(21));
			ret.Set("GravLeft", DynValue.NewNumber(22));
			ret.Set("Push", DynValue.NewNumber(23));
			ret.Set("Net", DynValue.NewNumber(24));
			ret.Set("InfiniteItem", DynValue.NewNumber(25));
			ret.Set("Happy", DynValue.NewNumber(26));
			ret.Set("Sad", DynValue.NewNumber(27));
			ret.Set("Heart", DynValue.NewNumber(28));
			ret.Set("Time", DynValue.NewNumber(29));
			ret.Set("Egg", DynValue.NewNumber(30));

			return ret;
		}

		private int ColorFromRGB(int r, int g, int b)
		{
			return (byte)b | ((byte)g << 8) | ((byte)r << 16);
		}

		private void FillRectangle(int x, int y, int width, int height, params int[] blockTypes)
		{
			if (!VerifyRectangleParams(width, height, blockTypes))
				return;

			for (int iX = x; iX < x + width; iX++)
			{
				int currentType = (iX - x) % blockTypes.Length;
				for (int iY = y; iY < y + height; iY++)
				{
					if (blockTypes[currentType] >= 0)
						Map.AddBlock(iX, iY, blockTypes[currentType]);
					currentType = ++currentType % blockTypes.Length;
				}
			}
		}
		private void PlaceRectangle(int x, int y, int width, int height, params int[] blockTypes)
		{
			if (!VerifyRectangleParams(width, height, blockTypes))
				return;

			// left/right
			for (int iX = x; iX < x + width; iX += Math.Max(1, width - 1)) // Math.Max so that width 1 won't loop
			{
				int currentType = (iX - x) % blockTypes.Length;
				for (int iY = y; iY < y + height; iY++)
				{
					if (blockTypes[currentType] >= 0)
						Map.AddBlock(iX, iY, blockTypes[currentType]);
					currentType = ++currentType % blockTypes.Length;
				}
			}

			// top/bottom
			for (int iY = y; iY < y + height; iY += Math.Max(1, height - 1))
			{
				int currentType = (iY - y + 1) % blockTypes.Length;
				for (int iX = x + 1; iX < x + width - 1; iX++)
				{
					Map.AddBlock(iX, iY, blockTypes[currentType]);
					currentType = ++currentType % blockTypes.Length;
				}
			}
		}
		private void ClearRectangle(int x, int y, int width, int height)
		{
			if (!VerifyRectangleParams(width, height, new int[] { 0 }))
				return;

			for (int iX = x; iX < x + width; iX++)
			{
				for (int iY = y; iY < y + height; iY++)
					Map.DeleteBlock(iX, iY);
			}
		}
		private bool VerifyRectangleParams(int width, int height, params int[] blockTypes)
		{
			if (blockTypes.Length == 0 || width < 1 || height < 1)
				return false;

			// Don't allow giant rectangles; they'd take too many resources
			if (width > 100 || height > 100)
				return false;

			return true;
		}

		private int GetBlock(int x, int y)
		{
			int t = Map.GetBlock(x, y).T;
			return t == 99 ? -1 : t;
		}
		private void RemoveAllBlocks(int type)
		{
			Map.ClearType(type);
		}
		#endregion

		public string[] GetParamNames()
		{
			if (Parameters != null)
				return Parameters.Keys.Convert<string>(DataType.String).ToArray();
			else
				return new string[0];
		}

		public string GetParamValue(string paramName)
		{
			if (Parameters != null)
				return Parameters.Get(paramName).CastToString();
			else
				return null;
		}

		public bool SetParamValue(string paramName, string value)
		{
			if (Parameters != null && GetParamNames().Contains(paramName))
			{
				Parameters[paramName] = DynValue.FromObject(script, JToken.Parse(value).ToObject<object>());
				return true;
			}
			else
				return false;
		}

		public Task<string> GenerateMap(CancellationTokenSource cts)
		{
			Map.ClearBlocks();

			if (!int.TryParse(GetParamValue("seed"), out int seed) || seed == 0)
				LastSeed = Environment.TickCount;
			else
				LastSeed = seed;
			script.DoString("math.randomseed(" + LastSeed + ");");

			Coroutine coroutine = script.CreateCoroutine(script.Globals["Generate"]).Coroutine;
			coroutine.AutoYieldCounter = 50000;

			DynValue result = DynValue.NewYieldReq(null);
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			try
			{
				while (result.Type == DataType.YieldRequest && !cts.IsCancellationRequested)
					result = coroutine.Resume();
			}
			catch (InterpreterException ex) // base exception type for MoonSharp
			{
				return Task.FromResult("Lua error: " + ex.DecoratedMessage);
			}

			if (cts.IsCancellationRequested)
				return Task.FromResult("generation timed out");

			Console.WriteLine("Level generated in " + stopwatch.ElapsedMilliseconds + "ms");
			return Task.FromResult(result.String);

		}

		public JObject GetSaveObject()
		{
			JObject json = new JObject();
			if (Parameters != null)
			{
				foreach (TablePair tp in Parameters.Pairs)
				{
					object value = JToken.Parse(tp.Value.ToString()).ToObject<object>();
					json[tp.Key.CastToString()] = JToken.FromObject(value);
				}
			}

			return json;
		}
	}
}
