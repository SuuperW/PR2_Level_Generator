using System;
using System.Collections.Generic;
using System.Text;

namespace LevelGenBot
{
    class CommandHistory
    {
		const double ticksPerSecond = 10000000;

		private SortedList<ulong, SortedDictionary<string, long>> history = new SortedList<ulong, SortedDictionary<string, long>>();
		private SortedDictionary<string, long> commandHistory = new SortedDictionary<string, long>();
		private SortedDictionary<ulong, long> userHistory = new SortedDictionary<ulong, long>();

		public void AddCommand(string command, ulong userID)
		{
			if (!history.ContainsKey(userID))
				history.Add(userID, new SortedDictionary<string, long>());

			history[userID][command] = commandHistory[command] = userHistory[userID] = DateTime.Now.Ticks;
		}

		public double TimeSinceLastUse(string command)
		{
			if (!commandHistory.ContainsKey(command))
				return double.PositiveInfinity;
			return (DateTime.Now.Ticks - commandHistory[command]) / ticksPerSecond;
		}
		public double TimeSinceLastUse(string command, ulong userID)
		{
			if (!history.ContainsKey(userID) || !history[userID].ContainsKey(command))
				return double.PositiveInfinity;
			return (DateTime.Now.Ticks - history[userID][command]) / ticksPerSecond;
		}
		public double TimeSinceLastCommand(ulong userID)
		{
			if (!userHistory.ContainsKey(userID))
				return double.PositiveInfinity;
			return (DateTime.Now.Ticks - userHistory[userID]) / ticksPerSecond;
		}
    }
}
