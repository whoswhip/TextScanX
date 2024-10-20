using System.Collections.Concurrent;
using Tesseract;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace FindTextInImage
{
    class Program
    {
        static List<ImageDATA> images = new List<ImageDATA>();
        static double meanConfidence = 0.5;

        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.Title = "OCR Scanner │ whoswhip";
            await InitAsync();
            Console.Write("┌───┤OCR Scanner├───┐\n│[1] Search Files   │\n│[2] Search for Text│\n└[»]                ┘");
            Console.CursorLeft = 5;
            var choice = Console.ReadKey().KeyChar.ToString();
            switch (choice)
            {
                case "1":
                    Console.Clear();
                    Console.Title = "Finding Text │ whoswhip";
                    Console.Write("┌─────────┤OCR Scanner├────────┐\n│[1] Normal Directory Search   │\n│[2] Recursive Directory search│\n└[»]                           ┘");
                    Console.CursorLeft = 5;
                    var choice2 = Console.ReadKey().KeyChar.ToString();
                    switch (choice2)
                    {
                        case "1":
                            Console.Clear();
                            Console.Write("┌─┤Finding Text\n│[+] Enter the directory to search through\n└[»] ");
                            var path = Console.ReadLine().Replace(@"""", "");
                            if (!Directory.Exists(path))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("\nInvalid directory path");
                                Console.ResetColor();
                                Thread.Sleep(1000);
                                await ReturnToMain();
                            }
                            await SearchTextInImageAsync(path, true);
                            break;
                        case "2":
                            Console.Clear();
                            Console.Write("┌─┤Finding Text\n│[+] Enter the directory to search through\n└[»] ");
                            var path2 = Console.ReadLine().Replace(@"""", "");
                            if (!Directory.Exists(path2))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("\nInvalid directory path");
                                Console.ResetColor();
                                Thread.Sleep(1000);
                                await ReturnToMain();
                            }
                            await HandleRecursiveSearching(path2);
                            break;
                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\nInvalid choice");
                            Console.ResetColor();
                            Thread.Sleep(1000);
                            await ReturnToMain();
                            break;
                    }
                    break;
                case "2":
                    Console.Clear();
                    HandleSearchingDatabase().Wait();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nInvalid choice");
                    Console.ResetColor();
                    Thread.Sleep(1000);
                    await ReturnToMain();
                    break;
            }
        }
        static async Task ReturnToMain()
        {
            Console.Clear();
            await Main(null);
        }
        static async Task HandleRecursiveSearching(string rootdirectory)
        {

           var directories = Directory.GetDirectories(rootdirectory);
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories)
                                    .Concat(Directory.GetFiles(directory, "*.jpg", SearchOption.AllDirectories))
                                    .Concat(Directory.GetFiles(directory, "*.jpeg", SearchOption.AllDirectories))
                                    .Concat(Directory.GetFiles(directory, "*.bmp", SearchOption.AllDirectories))
                                    .Concat(Directory.GetFiles(directory, "*.webp", SearchOption.AllDirectories))
                                    .Concat(Directory.GetFiles(directory, "*.tiff", SearchOption.AllDirectories))
                                    .ToArray();
                if (files.Length > 0)
                {
                    await SearchTextInImageAsync(directory, false);
                }
                var subdirectories = Directory.GetDirectories(directory);
                if (subdirectories.Length > 0)
                {
                    await HandleRecursiveSearching(directory);
                }
            }
        }
        static async Task SearchTextInImageAsync(string directoryPath, bool rtm)
        {
            Console.WriteLine($"[INFO - {DateTime.Now}] Using mean confidence of {meanConfidence}");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var files = Directory.GetFiles(directoryPath, "*.png", SearchOption.AllDirectories)
                                .Concat(Directory.GetFiles(directoryPath, "*.jpg", SearchOption.AllDirectories))
                                .Concat(Directory.GetFiles(directoryPath, "*.jpeg", SearchOption.AllDirectories))
                                .Concat(Directory.GetFiles(directoryPath, "*.bmp", SearchOption.AllDirectories))
                                .Concat(Directory.GetFiles(directoryPath, "*.webp", SearchOption.AllDirectories))
                                .Concat(Directory.GetFiles(directoryPath, "*.tiff", SearchOption.AllDirectories))
                                .ToArray();
            var ocrResults = new ConcurrentBag<ImageDATA>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(files, parallelOptions, async file =>
                {
                    string hash = await Hashing.GetSha256Hash(file);
                    if (await IsImageInDatabase(hash))
                    {
                        return;
                    }
                    try
                    {
                        using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                        using (var img = Pix.LoadFromFile(file))
                        using (var page = engine.Process(img))
                        {
                            var text = page.GetText();
                            if (page.GetMeanConfidence() < meanConfidence)
                            {
                                return;
                            }
                            var creation = DateTimeOffset.FromFileTime(File.GetCreationTime(file).ToFileTime()).ToUnixTimeSeconds();
                            var data = new ImageDATA
                            {
                                UUID = Guid.NewGuid().ToString(),
                                ImageHash = hash,
                                Path = file,
                                Text = text,
                                CreationDate = creation
                            };
                            if (ocrResults.Contains(data) || images.Contains(data))
                            {
                                return;
                            }
                            ocrResults.Add(data);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error processing {file}: {e.Message}");
                        Console.ResetColor();
                    }
                });
            });

            stopwatch.Stop();
            Console.WriteLine($"Time taken to search text in {files.Length} images: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Reset();
            stopwatch.Start();
            await AddImageDataToDB(ocrResults.ToList());
            stopwatch.Stop();
            Console.WriteLine($"Time taken to add {ocrResults.Count} images to database: {stopwatch.ElapsedMilliseconds} ms");
            if (rtm)
            {
                Thread.Sleep(2500);
                await ReturnToMain();
            }
        }
        static async Task SearchTextInDatabase(string input)
        {
            using (var connection = new SqliteConnection("Data Source=OCR.db"))
            {
                await connection.OpenAsync();
                var searchCommand = connection.CreateCommand();
                searchCommand.CommandText = "SELECT * FROM image WHERE Text LIKE @input";
                searchCommand.Parameters.AddWithValue("@input", $"%{input}%");
                var reader = await searchCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Path: {reader.GetString(2)}");
                    Console.ResetColor();
                    Console.WriteLine($"Text: \n{reader.GetString(3)}");
                }
            }
        }
        static async Task HandleSearchingDatabase()
        {
            while (true)
            {
                Console.Clear();
                Console.Title = "Searching Database │ whoswhip";
                Console.Write("┌─┤Searching Database\n│[+] Enter the text to search for\n└[»] ");
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        Console.WriteLine("\nEscape key pressed. Exiting...");
                        Thread.Sleep(1000);
                        await ReturnToMain();
                        break;
                    }
                }
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }
                if (input == "eXiT" || input == "qUiT")
                {
                    Console.WriteLine("Exiting...");
                    Thread.Sleep(1000);
                    await ReturnToMain();
                }
                await SearchTextInDatabase(input);
                Console.ReadKey();
            }
        }
        static async Task InitAsync()
        {
            using (var connection = new SqliteConnection("Data Source=OCR.db"))
            {
                await connection.OpenAsync();
                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = "CREATE TABLE IF NOT EXISTS image (UUID TEXT, ImageHash TEXT, Path TEXT, Text TEXT, Created INTEGER)";
                await createTableCommand.ExecuteNonQueryAsync();

                var extractDataCommand = connection.CreateCommand();
                extractDataCommand.CommandText = "SELECT * FROM image";
                var reader = await extractDataCommand.ExecuteReaderAsync();
                if (!reader.HasRows)
                {
                    return;
                }
                while (await reader.ReadAsync())
                {
                    images.Add(new ImageDATA
                    {
                        UUID = reader.GetString(0),
                        ImageHash = reader.GetString(1),
                        Path = reader.GetString(2),
                        Text = reader.GetString(3),
                        CreationDate = reader.GetInt64(4)
                    });
                }
            }
            if (!File.Exists("config.json"))
            {
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine($"[WARNING - {DateTime.Now}] Config.json not found! Creating one. Update the values as you please.");
                Console.ResetColor();
                var jobject = new JObject
                {
                    { "MeanConfidence", 0.5 }
                };
                File.WriteAllText("config.json", jobject.ToString());
                Console.WriteLine($"[INFO - {DateTime.Now}] Using default value MeanConfidence of 50%/0.5.");
            }
            else
            {
                var config = JObject.Parse(File.ReadAllText("config.json"));
                meanConfidence = (double)config["MeanConfidence"];
                if (meanConfidence > 1)
                {
                    meanConfidence /= 100;
                }
                if (meanConfidence < 0 || meanConfidence > 1)
                {
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.WriteLine($"[WARNING - {DateTime.Now}] Invalid MeanConfidence value in config.json. Using default value of 50%/0.5.");
                    Console.ResetColor();
                    meanConfidence = 0.5;
                    config["MeanConfidence"] = 0.5;
                    File.WriteAllText("config.json", config.ToString());
                }
            }
        }
        static async Task AddImageDataToDB(List<ImageDATA> _images)
        {
            using (var connection = new SqliteConnection("Data Source=OCR.db"))
            {
                await connection.OpenAsync();
                foreach (var image in _images)
                {
                    var insertDataCommand = connection.CreateCommand();
                    insertDataCommand.CommandText = "INSERT INTO image (UUID, ImageHash, Path, Text, Created) VALUES (@UUID, @ImageHash, @Path, @Text, @Created)";
                    insertDataCommand.Parameters.AddWithValue("@UUID", image.UUID);
                    insertDataCommand.Parameters.AddWithValue("@ImageHash", image.ImageHash);
                    insertDataCommand.Parameters.AddWithValue("@Path", image.Path);
                    insertDataCommand.Parameters.AddWithValue("@Text", image.Text);
                    insertDataCommand.Parameters.AddWithValue("@Created", image.CreationDate);
                    await insertDataCommand.ExecuteNonQueryAsync();
                }
            }
        }
        static async Task<bool> IsImageInDatabase(string hash)
        {
            using (var connection = new SqliteConnection("Data Source=OCR.db"))
            {
                await connection.OpenAsync();
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT * FROM image WHERE ImageHash = @hash";
                checkCommand.Parameters.AddWithValue("@hash", hash);
                var reader = await checkCommand.ExecuteReaderAsync();
                return reader.HasRows;
            }
        }
    }
    class Hashing
    {
        public static async Task<string> GetSha256Hash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(await sha256.ComputeHashAsync(stream)).Replace("-", "");
                }
            }
        }
        /*public static async Task<string> HashImage(Image img)
        {
            var converter = new ImageConverter();
            var bytes = (byte[])converter.ConvertTo(img, typeof(byte[]));
            using (var sha256 = SHA256.Create())
            {
                using (var stream = new MemoryStream(bytes))
                {
                    return BitConverter.ToString(await sha256.ComputeHashAsync(stream)).Replace("-", "");
                }
            }
        }*/
    }
    class ImageDATA
    {
        public string? UUID { get; set; }
        public string? ImageHash { get; set; }
        public string? Path { get; set; }
        public string? Text { get; set; }
        public long? CreationDate { get; set; }
    }
}