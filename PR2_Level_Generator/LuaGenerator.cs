using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

using MoonSharp.Interpreter;

namespace PR2_Level_Generator
{
	public class LuaGenerator : ILevelGenerator
	{
		public MapLE Map { get; set; }
		public int LastSeed { get; } = 0;

		private Script script;
		public string ScriptName;
		private string _luaScript;
		public string LuaScript { get => _luaScript; }
		public string SetLua(string value)
		{
			_luaScript = value;
			Map = new MapLE();

			script = new Script(CoreModules.Preset_HardSandbox);
			parameters = new SortedDictionary<string, DynValue>();
			ExposeFunctions();
			RemoveFunctionsWithCallbacks();
			try
			{
				// Wrap the code in a function, so that MoonSharp can use it as a coroutine.
				string codeToRun = "function Initialize() " + _luaScript + "\n" +
					"_G.Generate = Generate;\nend";
				script.DoString(codeToRun);
				DynValue initFunction = script.Globals.Get("Initialize");
				Coroutine coroutine = script.CreateCoroutine(initFunction).Coroutine;
				coroutine.AutoYieldCounter = 100000;

				DynValue result = coroutine.Resume();
				if (result.Type == DataType.YieldRequest)
					return "initialization timed out";
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
			script.Globals.Get("string").Table["gsub"] = (Func<DynValue, DynValue, DynValue, DynValue, DynValue>)((s, p, r, n) => {
				if (p.Function == null) return script.Call(gsub, s, p, r, n); else return null; });
		}

		private SortedDictionary<string, DynValue> parameters;

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
		private void ExposeFunctions()
		{
			script.Globals["SetParam"] = (Action<string, DynValue>)((n, v) => { parameters[n] = v; });
			script.Globals["GetParam"] = (Func<string, DynValue>)((n) => {
				if (parameters.ContainsKey(n)) return parameters[n];
				else throw new ScriptRuntimeException("Attempted to get non-existent param '" + n + "'."; });

			script.Globals["PlaceBlock"] = (Action<int, int, int>)Map.AddBlock;

			script.Globals["PlaceText"] = (Action<string, int, int>)((t, x, y) =>
			{
				if (Map.artCodes[0].Length > 0)
					Map.artCodes[0] += ",";
				Map.artCodes[0] += x + ";" + y + ";t;" + t + ";0;100;100";
			});
		}
		#endregion

		public string[] GetParamNames()
		{
			string[] ret = new string[parameters.Count];
			parameters.Keys.CopyTo(ret, 0);
			return ret;
		}

		public string GetParamValue(string paramName)
		{
			return parameters[paramName].CastToString();
		}

		public bool SetParamValue(string paramName, string value)
		{
			if (parameters.ContainsKey(paramName))
			{
				parameters[paramName] = DynValue.FromObject(script, value);
				return true;
			}
			else
				return false;
		}

		public Task<string> GenerateMap(CancellationTokenSource cts)
		{
			Map.ClearBlocks();
			try
			{
				Coroutine coroutine = script.CreateCoroutine(script.Globals["Generate"]).Coroutine;
				coroutine.AutoYieldCounter = 50000;

				DynValue result = DynValue.NewYieldReq(null);
				Stopwatch stopwatch = new Stopwatch();
				stopwatch.Start();
				while (result.Type == DataType.YieldRequest && !cts.IsCancellationRequested)
					result = coroutine.Resume();

				if (cts.IsCancellationRequested)
					return Task.FromResult("generation timed out");

				Console.WriteLine("Level generated in " + stopwatch.ElapsedMilliseconds + "ms");
				return Task.FromResult(result.String);
			}
			catch (InterpreterException ex) // base exception type for MoonSharp
			{
				return Task.FromResult("Lua error: " + ex.DecoratedMessage);
			}
		}

		public string GetSaveString()
		{
			StringBuilder ret = new StringBuilder();
			ret.Append(this.GetType().ToString());
			foreach (KeyValuePair<string, DynValue> kvp in parameters)
			{
				ret.Append("\n" + kvp.Key + ":" + kvp.Value.ToString());
			}

			return ret.ToString();
		}
	}
}
