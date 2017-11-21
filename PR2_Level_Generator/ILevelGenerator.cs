using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace PR2_Level_Generator
{
	public interface ILevelGenerator
	{
		string[] GetParamNames();
		string GetParamValue(string paramName);
		bool SetParamValue(string paramName, string value);

		MapLE Map { get; }

		int LastSeed { get; }
		Task<string> GenerateMap(CancellationTokenSource cts);

		JObject GetSaveObject();
	}
}
