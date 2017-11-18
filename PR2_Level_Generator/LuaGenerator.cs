using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using MoonSharp.Interpreter;

namespace PR2_Level_Generator
{
	public class LuaGenerator : ILevelGenerator
	{
		public MapLE Map { get; set; }
		public int LastSeed { get; } = 0;

		private Script script;
		private string _luaScript;
		public string LuaScript { get => _luaScript; }
		public string SetLua(string value)
		{
			_luaScript = value;
			Map = new MapLE();

			script = new Script(CoreModules.Preset_HardSandbox);
			parameters = new SortedDictionary<string, DynValue>();
			ExposeFunctions();
			try
			{
				// Wrap the code in a function, so that MoonSharp can use it as a coroutine.
				string codeToRun = "function Initialize()\n" + _luaScript + "\n" +
					"_G.Generate = Generate;\nend";
				script.DoString(codeToRun);
				DynValue initFunction = script.Globals.Get("Initialize");
				DynValue coroutine = script.CreateCoroutine(initFunction);
				coroutine.Coroutine.AutoYieldCounter = 100;

				int count = 0;
				DynValue result = coroutine.Coroutine.Resume();
				while (result.Type == DataType.YieldRequest)
				{
					// TODO: actually time this
					result = coroutine.Coroutine.Resume();
					count++;
				}
			}
			catch (InterpreterException ex) // base exception type for MoonSharp
			{
				SetLua("function Generate()\nend");
				return ex.DecoratedMessage;
			}

			return null;
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
			script.Globals["CreateParam"] = (Action<string>)((n) => { parameters.Add(n, DynValue.NewNil()); });
			script.Globals["SetParam"] = (Action<string, DynValue>)((n, v) => { parameters[n] = v; });
			script.Globals["GetParam"] = (Func<string, DynValue>)((n) => { return parameters[n]; });

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
				// TODO: implement a time-out via the CancellationTokenSource
				return Task.FromResult(script.Call(script.Globals["Generate"]).String);
			}
			catch (InterpreterException ex) // base exception type for MoonSharp
			{
				return Task.FromResult(ex.DecoratedMessage);
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
