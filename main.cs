using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot;
using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using System.Globalization;

namespace findNBASalary
{
    class EspnScraper
    {
        #region globals
        static string salaryFile = "salaries.txt";
        static string statsFiles = "salaryStats.txt";
        static string baseIncomeURL = "https://www.espn.com/nba/salaries/_/page/";
        static string startingIncomeURL = "https://www.espn.com/nba/salaries";
        #endregion

        static async Task Main()
        {


            List<string> salaryPages = new List<string> { startingIncomeURL }; // First page without the page number in the URL

            for (int i = 2; i <= 12; i++)
            {
                salaryPages.Add($"{baseIncomeURL}{i}");
            }

            foreach (string url in salaryPages)
            {
                string espnHtmlData = await DownloadHtmlAsync(url);
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(espnHtmlData);
                await processSalaries(document);

            }
            await calculateSalaryStats(salaryFile);
						await graphStats(salaryFile);
        }

        static async Task processSalaries(HtmlDocument document)
        {
            var salaries = new List<string>();
            var salaryTable = document.DocumentNode.SelectSingleNode("//table[contains(@class, 'tablehead')]");

            if (salaryTable == null)
            {
                Console.WriteLine("Salary table not found.");
                return;
            }

            foreach (var row in salaryTable.SelectNodes(".//tr[position() > 1]")) // Skip the header row.
            {
                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count <= 3) continue;

                string playerName = cells[1].InnerText.Trim();
                string playerSalary = cells[3].InnerText.Trim();
                if (playerSalary == "SALARY") continue;

                // Strip away commas and $ before putting the number in the file
                playerSalary = playerSalary.Replace(",", "").Replace("$", "");

                salaries.Add(playerSalary);
                Console.WriteLine($"Player: {playerName}, Salary: {playerSalary}");
            }

            await appendSalariesToFile(salaryFile, salaries);
        }

        static async Task appendSalariesToFile(string filePath, IEnumerable<string> salaries)
        {
            using StreamWriter writer = new StreamWriter(filePath, append: true);
            foreach (var salary in salaries)
            {
                await writer.WriteLineAsync(salary);
            }
        }

        static async Task<string> DownloadHtmlAsync(string url)
        {
            using HttpClient client = new HttpClient();
            return await client.GetStringAsync(url);
        }

        static async Task calculateSalaryStats(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("File not found.");
                return;
            }
            string[] salaryLines = await File.ReadAllLinesAsync(filePath);
            var salaries = salaryLines.Select(line =>
            {
                var trimmedLine = line.Trim();
                return decimal.TryParse(trimmedLine, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal value) ? value : 0;
            })
            .Where(value => value > 0)
            .ToList();
            if (!salaries.Any())
            {
                Console.WriteLine("No valid salary data found.");
                return;
            }
            var maxSalary = salaries.Max();
            var minSalary = salaries.Min();
            var averageSalary = salaries.Average();
            var totalSalary = salaries.Sum();
            var medianSalary = CalculateMedian(salaries);
            string stats = $@"
				Maximum Salary: {maxSalary:C}
				Minimum Salary: {minSalary:C}
				Average Salary: {averageSalary:C}
				Total Salary: {totalSalary:C}
				Median Salary: {medianSalary:C}
				";
            Console.WriteLine(stats);
            await File.WriteAllTextAsync("salaryStats.txt", stats);
        }

        private static decimal CalculateMedian(List<decimal> numbers)
        {
            int numberCount = numbers.Count();
            int halfIndex = numbers.Count() / 2;
            var sortedNumbers = numbers.OrderBy(n => n).ToList();
            decimal median;
            if ((numberCount % 2) == 0)
            {
                median = (sortedNumbers.ElementAt(halfIndex) + sortedNumbers.ElementAt(halfIndex - 1)) / 2;
            }
            else
            {
                median = sortedNumbers.ElementAt(halfIndex);
            }
            return median;
        }
        static async Task graphStats(string filePath)
        {
						// Read the salary data
						string[] salaryLines = await File.ReadAllLinesAsync(filePath);
						var salaries = salaryLines
								.Select(line => decimal.Parse(line.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture))
								.OrderBy(value => value)
								.ToList();
						// Ensure there are at least two data points to plot a graph
						if (salaries.Count < 2)
						{
								Console.WriteLine("Not enough data to plot graph.");
								return;
						}
						// Initialize a new plot model
						var plotModel = new PlotModel { Title = "NBA Salary Scatter Plot" };
						// Create a line series
						var lineSeries = new LineSeries();
						for (int i = 0; i < salaries.Count; i++)
						{
								lineSeries.Points.Add(new DataPoint(i, (double)salaries[i]));
						}
						// Add the series to the plot model
						plotModel.Series.Add(lineSeries);
						// Customize the plot (optional)
						plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Player Index" });
						plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Salary ($)" });
						// Save the plot as a PNG image
						var pngExporter = new PngExporter { Width = 600, Height = 400, Background = OxyColors.White };
						pngExporter.ExportToFile(plotModel, "salaryScatterPlot.png");
        }
    }
}
