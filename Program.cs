/* 
 * #Author - Pavan Shegokar
### Explanation of the Code

-File Monitoring : The application uses `Timer` to monitor the input folder for new XML files every 5 second.
-Loading Reference Data : The application loads static data for value factors and emissions factors from the `ReferenceData.xml` file.
-XML Parsing : It reads the XML file and extracts data for wind, gas, and coal generators.

 **Calculations :
  -Total generation values are calculated for each generator using the formula: `Energy x Price x ValueFactor`.
  -Daily emissions are calculated for gas and coal generators using the formula: `Energy x EmissionsRating x EmissionFactor`.
  -Actual heat rates for coal generators are calculated using the formula: `TotalHeatInput / ActualNetGeneration`.

-Output Generation : The results are saved in a new XML file in the specified output folder.

### Notes
- Ensure that the paths in the `app.config` file are correctly set to your input and output directories, as well as the reference data file.
- The application will run continuously, monitoring for new files until manually stopped. 

*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Xml.Linq;


namespace XMLParserApp
{
    public class Program
    {
        private static string inputFolder;
        private static string outputFolder;
        private static Timer timer;
        private static Dictionary<string, decimal> valueFactors;
        private static Dictionary<string, decimal> emissionsFactors;

        public class Generator
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public decimal EmissionsRating { get; set; }
            public decimal TotalHeatInput { get; set; }
            public decimal ActualNetGeneration { get; set; }
            public List<GenerationData> GenerationData { get; set; }
        }

        public class GenerationData
        {
            public DateTime Date { get; set; }
            public decimal Energy { get; set; }
            public decimal Price { get; set; }
        }

        static void Main(string[] args)
        {
            // Load configuration settings
            LoadConfiguration();

            // Load reference file data
            string referenceDataFile = ConfigurationManager.AppSettings["ReferenceDataFile"];
            LoadReferenceData(referenceDataFile);

            // Set up a timer to monitor the input folder
            int fileTimerInSec = Convert.ToInt32(ConfigurationManager.AppSettings["FileTimerInSec"]);
            timer = new Timer(fileTimerInSec); // Check every 5 seconds
            timer.Elapsed += CheckForNewFiles;
            timer.Start();

            Console.WriteLine("Monitoring input folder for new XML files...");
            Console.ReadLine(); // Keep the application running          

        }

        private static void LoadReferenceData(string filePath)
        {
            XDocument referenceDoc = XDocument.Load(filePath);

            valueFactors = new Dictionary<string, decimal>
            {
                { "Offshore Wind", 0.265m },
                { "Onshore Wind", 0.946m },
                { "Gas", 0.696m },
                { "Coal", 0.696m }
            };

            emissionsFactors = new Dictionary<string, decimal>
            {
                { "Gas", 0.562m },
                { "Coal", 0.812m }
            };
        }

        private static void LoadConfiguration()
        {
            try
            {
                inputFolder = ConfigurationManager.AppSettings["InputFolder"];
                outputFolder = ConfigurationManager.AppSettings["OutputFolder"];
            }
            catch (ConfigurationErrorsException ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void CheckForNewFiles(object sender, ElapsedEventArgs e)
        {
            try
            {
                var file = GetFiles(inputFolder, "*.xml");

                if (file != null)
                {
                    ProcessFile(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for new files: {ex.Message}");
            }
        }

        private static string GetFiles(string path, string fileFormats)
        {
            var file = new DirectoryInfo(path).GetFiles(fileFormats).OrderByDescending(o => o.CreationTime).FirstOrDefault();
            if (file == null)
            {
                Console.WriteLine("No file found in input folder...");
                return null;
            }
            return file.FullName;
        }

        private static void ProcessFile(string filePath)
        {
            try
            {
                Console.WriteLine($"New file detected: {filePath}");

                XDocument doc = XDocument.Load(filePath);
                var generators = new List<Generator>();

                // Process Wind Generators
                generators.AddRange(doc.Descendants("WindGenerator").Select(g => new Generator
                {
                    Name = (string)g.Element("Name"),
                    Type = g.Element("Location").Value.Contains("Offshore") ? "Offshore Wind" : "Onshore Wind",
                    GenerationData = g.Element("Generation")?.Elements("Day")
                        .Select(d => new GenerationData
                        {
                            Date = (DateTime)d.Element("Date"),
                            Energy = (decimal)d.Element("Energy"),
                            Price = (decimal)d.Element("Price")
                        }).ToList()
                }));

                // Process Gas Generators
                generators.AddRange(doc.Descendants("GasGenerator").Select(g => new Generator
                {
                    Name = (string)g.Element("Name"),
                    Type = "Gas",
                    EmissionsRating = (decimal)g.Element("EmissionsRating"),
                    GenerationData = g.Element("Generation")?.Elements("Day")
                        .Select(d => new GenerationData
                        {
                            Date = (DateTime)d.Element("Date"),
                            Energy = (decimal)d.Element("Energy"),
                            Price = (decimal)d.Element("Price")
                        }).ToList()
                }));

                // Process Coal Generators
                generators.AddRange(doc.Descendants("CoalGenerator").Select(g => new Generator
                {
                    Name = (string)g.Element("Name"),
                    Type = "Coal",
                    EmissionsRating = (decimal)g.Element("EmissionsRating"),
                    TotalHeatInput = (decimal)g.Element("TotalHeatInput"),
                    ActualNetGeneration = (decimal)g.Element("ActualNetGeneration"),
                    GenerationData = g.Element("Generation")?.Elements("Day")
                        .Select(d => new GenerationData
                        {
                            Date = (DateTime)d.Element("Date"),
                            Energy = (decimal)d.Element("Energy"),
                            Price = (decimal)d.Element("Price")
                        }).ToList()
                }));

                // Calculate Total Generation Values 
                var totalGenerationValues = generators.Select(g => new
                {
                    GeneratorName = g.Name,
                    TotalGenerationValue = g.GenerationData.Sum(d => d.Energy * d.Price * valueFactors[g.Type])
                });

                //Calculate Daily Emissions
                var dailyEmissions = generators.Where(g => g.Type == "Gas" || g.Type == "Coal")
                    .SelectMany(g => g.GenerationData.Select(d => new
                    {
                        Date = d.Date.Date,
                        Emission = d.Energy * g.EmissionsRating * emissionsFactors[g.Type],
                        GeneratorName = g.Name
                    }))
                    .GroupBy(d => d.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        HighestEmission = g.OrderByDescending(d => d.Emission).FirstOrDefault()
                    });

                // Calculate Actual Heat Rates for Coal Generators
                var actualHeatRates = generators.Where(g => g.Type == "Coal")
                    .Select(g => new
                    {
                        GeneratorName = g.Name,
                        ActualHeatRate = g.TotalHeatInput / g.ActualNetGeneration
                    });

                //Generate output xml
                GenerateOutputXml(totalGenerationValues, dailyEmissions, actualHeatRates, filePath);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }
        }

        private static void GenerateOutputXml(IEnumerable<dynamic> totalGenerationValues,
            IEnumerable<dynamic> dailyEmissions,
            IEnumerable<dynamic> actualHeatRates, string filePath)
        {
            try
            {
                XElement output = new XElement("GenerationOutput",
                new XElement("Totals",
                    totalGenerationValues.Select(g => new XElement("Generator",
                        new XElement("Name", g.GeneratorName),
                        new XElement("Total", g.TotalGenerationValue)
                    ))
                ),
                new XElement("MaxEmissionGenerators",
                    dailyEmissions.Select(g => new XElement("Day",
                        new XElement("Name", g.HighestEmission.GeneratorName),
                        new XElement("Date", g.Date),
                        new XElement("Emission", g.HighestEmission?.Emission ?? 0)
                    ))
                ),
                new XElement("ActualHeatRates",
                    actualHeatRates.Select(g => new XElement("ActualHeatRate",
                        new XElement("Name", g.GeneratorName),
                        new XElement("HeatRate", g.ActualHeatRate)
                    ))
                )
            );

                string outputFilePath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(filePath) + "-Result.xml");
                output.Save(outputFilePath);
                Console.WriteLine($"Output generated: {outputFilePath}");
                File.Delete(filePath); // Optionally delete the processed file
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating output file {filePath}: {ex.Message}");
            }
        }
    }
}

