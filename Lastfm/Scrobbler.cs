using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lastfm
{
    class Scrobbler
    {
        public static string lfmapiroot = "http://ws.audioscrobbler.com/2.0/";
        public static string api_key = "4a36f2acf9f3c12ce70a4d60313bc7f8";
        public static string api_secret = "2b083e0d3d3d87766e19cacd50411032";
        public static string jsonpls = "&format=json";
        private static readonly HttpClient client = new();


        public static async Task Main()
        {
            int usr = 99;
            if (!File.Exists(Directory.GetCurrentDirectory() + "\\SessionKey.txt"))
            {
                Console.WriteLine("No 'SessionKey.txt' found, beginning 'GetSessionKey'...");
                await GetSessionKey();
            }
            else
            {
                Console.WriteLine("'SessionKey.txt' found! \n");
            }
            
            while (usr != 0)
            {
                Console.WriteLine("What would you like to do? \n" +
                   "[1] Get a session key again \n" +
                   "[2] Scrobble a song \n" +
                   "[3] Display your recently scrobbled tracks \n" +
                   "[0] Exit");
                usr = Convert.ToInt32(Console.ReadLine());
                if (usr == 1)
                {
                    await GetSessionKey();
                }
                else if (usr == 2)
                {
                    await Scrobble();
                }
                else if (usr == 3)
                {
                    await GetRecentTracks();
                }               
            }
            Environment.Exit(1);
        }

        public static async Task GetSessionKey()
        {
            Console.Clear();
            // Fetch a request token
            var token = await GetToken();

            // Request authorization from the user
            string url = "http://www.last.fm/api/auth/?api_key=" + api_key + "&token=" + token;
            Process.Start("explorer", $"\"{url}\"");
            Console.WriteLine("Complete authentication on the website and press a key to continue...");
            Console.ReadLine();

            // Fetch A Web Service Session
            var values = new SortedDictionary<string, string>
            {
                {"method", "auth.getSession"},
                {"api_key", api_key},
                {"token", token}
            };
            var sKeyReq = await client.GetStringAsync(lfmapiroot + "?method=auth.getSession&api_key=" + api_key + "&token=" + token +
                "&api_sig=" + LfmSignDict(values) + jsonpls);
            using JsonDocument document = JsonDocument.Parse(sKeyReq);
            JsonElement root = document.RootElement;
            JsonElement sK = root.GetProperty("session").GetProperty("key");
            Console.WriteLine($"Got session key for user: {root.GetProperty("session").GetProperty("name")} \n");
            await File.WriteAllTextAsync(Directory.GetCurrentDirectory() + "\\SessionKey.txt", sK.GetString());
        }

        public static async Task<string> GetToken()
        {
            // using var client = new HttpClient();
            var result = await client.GetStringAsync(lfmapiroot + "?method=auth.gettoken" + "&api_key=" + api_key + jsonpls);
            using JsonDocument document = JsonDocument.Parse(result);
            JsonElement root = document.RootElement;
            JsonElement element = root.GetProperty("token");
            Console.WriteLine("Fetched token: " + element);
            return element.GetString();
        }

        public static string CreateMD5Hash(string input)
        {
            // Step 1, calculate MD5 hash from input
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Step 2, convert byte array to hex string
            StringBuilder sb = new();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static string LfmSignDict(SortedDictionary<string, string> paramsToSend)
        {
            StringBuilder sb = new();
            foreach (KeyValuePair<string, string> kvp in paramsToSend)
            {
                sb.Append(kvp.Key).Append(kvp.Value);
            }
            sb.Append(api_secret);
            return CreateMD5Hash(sb.ToString());
        }

        public static async Task Scrobble()
        {
            Console.Clear();
            Console.WriteLine("Please input the song's artist:");
            string artist = Console.ReadLine();
            Console.WriteLine("Please input the song's title:");
            string title = Console.ReadLine();
            string unixtimestamp = Convert.ToString(DateTimeOffset.Now.ToUnixTimeSeconds());

            var values = new SortedDictionary<string, string>
                {
                {"artist", artist},
                {"track", title},
                {"timestamp", unixtimestamp},
                {"api_key", api_key},
                {"sk", await ReadSessionKey()},
                {"method", "track.scrobble"},
                };
            values.Add("api_sig", LfmSignDict(values));
            values.Add("format", "json");
           
            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync(lfmapiroot, content);
            var responseString = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(responseString);
            JsonElement root = document.RootElement;
            JsonElement element = root.GetProperty("scrobbles").GetProperty("@attr").GetProperty("accepted");
            if (element.GetInt32() == 1)
            {
                Console.WriteLine("Scrobble successful!");
            }
            else
            {
                Console.WriteLine("Scrobble unsuccessful:");
                Console.WriteLine(root);
            }
            
        }

        public static async Task<string> ReadSessionKey()
        {
            return await File.ReadAllTextAsync(Directory.GetCurrentDirectory() + "\\SessionKey.txt");
        }

        public static async Task GetRecentTracks()
        {
            Console.Clear();
            Console.WriteLine("Please input your username:");
            string username = Console.ReadLine();
            Console.WriteLine("Please input how many recent tracks to get:    (limit: 200)");
            int limit = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("");

            var response = await client.GetStringAsync(lfmapiroot + "?method=user.getrecenttracks&user=" + username
                + "&limit=" + limit + "&api_key=" + api_key + "&format=json");
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            JsonElement element = root.GetProperty("recenttracks").GetProperty("track");
            bool isnowplaying = element[0].TryGetProperty("@attr", out JsonElement has);
            bool check = false;
            int count = 1;
            if (element.GetArrayLength() != limit)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Now playing: ");
                Console.ResetColor();
                Console.Write(element[0].GetProperty("artist").GetProperty("#text").ToString());
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(" || ");
                Console.ResetColor();
                Console.Write(element[0].GetProperty("name").ToString() + "\n");

                foreach (JsonElement song in element.EnumerateArray())
                {
                    if (check == true)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("[" + count + "] ");
                        Console.ResetColor();
                        Console.Write(song.GetProperty("artist").GetProperty("#text").ToString());
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(" || ");
                        Console.ResetColor();
                        Console.Write(song.GetProperty("name").ToString() + "\n");
                        count++;
                    }
                    else
                    {
                        check = true;
                    }
                }
            }
            else
            {
                foreach (JsonElement song in element.EnumerateArray())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("[" + count + "] ");
                    Console.ResetColor();
                    Console.Write(song.GetProperty("artist").GetProperty("#text").ToString());
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(" || ");
                    Console.ResetColor();
                    Console.Write(song.GetProperty("name").ToString() + "\n");
                    count++;
                    
                }
            }
            Console.WriteLine();
        }
    }
}