using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Commodities.Properties;

namespace Commodities
{
	class Program
	{
		string startingStation = "Louis De Lacaille Prospect";
		int cash = 1545;

		Settings Settings
		{
			get { return Settings.Default; }
		}

		List<Commodity> commodities;
		Dictionary<string, string> stations;

		// precalculated for better performance;
		Dictionary<string, List<Commodity>> stationCommodities;

		static void Main(string[] args)
		{
			try
			{
				new Program().Solve();
				Console.WriteLine("done");
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex);
			}
			Console.ReadKey();
		}

		public void Solve()
		{
#if !DEBUG
			InputVariables();
#endif
			Console.WriteLine("Calculating routes");
			Load();
			var routes = Routes(startingStation, cash * 1000, Settings.Hops).OrderByDescending(a => a.Item2).Take(Settings.RoutesCount);
			foreach (var route in routes)
				PrintRoute(route);
		}

		private List<Tuple<List<Transaction>, int>> Routes(string src, int cash, int hops)
		{
			if (hops == 0)
				return new List<Tuple<List<Transaction>, int>> { new Tuple<List<Transaction>, int>(new List<Transaction>(), 0) };
			var cargo = BuyAtStation(src, cash).ToDictionary(a => a.Commodity.Name);
			var result = new List<Tuple<List<Transaction>, int>>();
			Parallel.ForEach(stations.Keys, dst =>
			{
				var bestDeal = SellAtStation(dst, cargo).OrderByDescending(a => a.Profit).FirstOrDefault();
				if (bestDeal != null)
				{
					var nextSteps = Routes(dst, cash + bestDeal.Profit, hops - 1);
					foreach (var step in nextSteps)
					{
						step.Item1.Insert(0, bestDeal);
						lock (result)
							result.Add(new Tuple<List<Transaction>, int>(step.Item1, step.Item2 + bestDeal.Profit));
					}
				}
			});
			return result;
		}

		private List<Cargo> BuyAtStation(string station, int cash)
		{
			return stationCommodities[station]
				.Where(a => a.Supply != Level.None && a.Buy > 0 /* in case of incorrect data */)
				.Select(a => new Cargo { Commodity = a, Amount = Math.Min(cash / a.Buy, Settings.Capacity) })
				.ToList();
		}

		private List<Transaction> SellAtStation(string station, List<Cargo> cargo)
		{
			return SellAtStation(station, cargo.ToDictionary(a => a.Commodity.Name));
		}

		// this is a performance optimization of previous method
		private List<Transaction> SellAtStation(string station, Dictionary<string, Cargo> cargo)
		{
			return stationCommodities[station]
				.Where(a => a.Demand != Level.None && cargo.ContainsKey(a.Name))
				.Select(a => new Transaction(cargo[a.Name], a))
				.ToList();
		}

		private void InputVariables()
		{
			Console.Write("Cash (k): ");
			cash = int.Parse(Console.ReadLine());
			Console.Write("Starting station: ");
			startingStation = Console.ReadLine();
		}

		private void PrintRoute(Tuple<List<Transaction>, int> route)
		{
			Console.WriteLine("Route with total profit: {0}", route.Item2);
			foreach (var hop in route.Item1)
				Console.WriteLine("Hop '{0}'-'{1}' with {2} x{3}={4}$", stations[hop.Source.Station], stations[hop.Destination.Station], hop.Source.Name, hop.Amount, hop.Profit);
			Console.WriteLine();
		}

		private void Load()
		{
			commodities = new List<Commodity>();
			stations = File.ReadAllLines(Settings.StationsFile).Select(a => a.Split('\t')).ToDictionary(a => a[1], a => a[0]);
			foreach (var line in File.ReadAllLines(Settings.CommoditiesFile))
			{
				var a = line.Split('\t');
				if (!stations.ContainsKey(a[4]))
					stations.Add(a[4], a[4]);
				commodities.Add(new Commodity
				{
					Category = a[0],
					Name = a[1],
					Demand = (Level)Enum.Parse(typeof(Level), a[2]),
					Supply = (Level)Enum.Parse(typeof(Level), a[3]),
					Station = a[4],
					Buy = int.Parse(a[5]),
					Sell = int.Parse(a[6])
				});
			}
			stationCommodities = commodities.GroupBy(a => a.Station).ToDictionary(a => a.Key, a => a.ToList());
		}
	}

	class Commodity
	{
		public string Category { get; set; }
		public string Name { get; set; }
		public string Station { get; set; }
		public Level Supply { get; set; }
		public Level Demand { get; set; }
		public int Buy { get; set; }
		public int Sell { get; set; }
	}

	enum Level
	{
		High, Med, Low, None
	}

	class Cargo
	{
		public Commodity Commodity { get; set; }
		public int Amount { get; set; }
	}

	class Transaction
	{
		public Commodity Source { get; set; }
		public Commodity Destination { get; set; }
		public int Amount { get; set; }

		public int Profit
		{
			get { return Amount * (Destination.Sell - Source.Buy); }
		}

		public Transaction(Commodity source, Commodity destination, int amount)
		{
			Source = source;
			Destination = destination;
			Amount = amount;
		}

		public Transaction(Cargo cargo, Commodity destination)
		{
			Destination = destination;
			Source = cargo.Commodity;
			Amount = cargo.Amount;
		}
	}
}
