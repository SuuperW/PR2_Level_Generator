using System;
using System.Threading.Tasks;
using System.Threading;

namespace PR2_Level_Generator
{
	public interface ILuaGenerator
	{
		string LuaScript { get; }
		string SetLua(string value);

		string[] GetParamNames();
		string GetParamValue(string paramName);
		void SetParamValue(string paramName, string value);

		MapLE Map { get; }
		int LastSeed { get; }

		Task<string> GenerateMap(CancellationTokenSource cts);

		string GetSaveString();

	}
}
