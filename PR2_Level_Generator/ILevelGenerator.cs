using System.Threading;
using System.Threading.Tasks;

namespace PR2_Level_Generator
{
	public interface ILevelGenerator
	{
		string[] GetParamNames();
		double GetParamValue(string paramName);
		void SetParamValue(string paramName, double value);

		MapLE Map { get; }

		int LastSeed { get; }
		Task<bool> GenerateMap(CancellationTokenSource cts);

		string GetSaveString();
	}
}
