using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ImageClient
{
    class Program
    {
        private static HttpClient _client;
        private const int ASYNC_CONCURRENCY = 10;
        private const int THREAD_COUNT = 4;
        private const int BUFFER_SIZE = 1024;
        private const string IMAGE_DIRECTORY = "./Users";
        private const string BASE_URL = "https://randomuser.me/api/";
        private static int _userCount = 0;
        private static ConcurrentBag<User> _users = new ConcurrentBag<User>();
        private static ConcurrentDictionary<string, string> _imageCache = new ConcurrentDictionary<string, string>();

        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");

            CreateUsersDirectory();
            _client = new HttpClient();

            Stopwatch sw = new Stopwatch();
            sw.Start();
            Stopwatch swTotal = new Stopwatch();
            swTotal.Start();

            int count = 1000;

            //GetBatchedUsersAndImages(count);
            GetUsersAndImages(count).Wait();

            //GetUsers(count).Wait();
            //sw.Stop();
            //Console.WriteLine($"Fetched users in {sw.ElapsedMilliseconds}ms");
            //sw.Restart();

            //GetImages().Wait();
            //sw.Stop();
            //Console.WriteLine($"Fetched user images in {sw.ElapsedMilliseconds}ms");

            swTotal.Stop();
            decimal usersPerSecond = Math.Round((decimal)(count * 1000) / (decimal)swTotal.ElapsedMilliseconds,3);
            Console.WriteLine($"Users per second: {usersPerSecond}");
            Console.WriteLine($"Finished in {swTotal.ElapsedMilliseconds}ms. Press any key to exit...");
            Console.ReadKey();
        }

        private static void CreateUsersDirectory()
        {
            if (Directory.Exists(IMAGE_DIRECTORY))
            {
                Directory.Delete(IMAGE_DIRECTORY, true);
            }
            
            Directory.CreateDirectory(IMAGE_DIRECTORY);
        }

        private static async Task GetUsersAndImages(int count)
        {
            var genders = new List<Gender>();
            for (int i = 0; i < count; i++)
            {
                genders.Add((i % 2) == 0 ? Gender.Female : Gender.Male);
            }
            Iterator iterator = new Iterator(ASYNC_CONCURRENCY);
            await iterator.IterateAsync(genders, GetRandomUserAndImage);
        }

        private static void GetBatchedUsersAndImages(int count)
        {
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < THREAD_COUNT; i++)
            {
                Task t = Task.Factory.StartNew(() =>
                {
                    GetUsersAndImages(count / THREAD_COUNT).Wait();
                }, TaskCreationOptions.LongRunning);
                tasks.Add(t);
            }

            Task.WhenAll(tasks).Wait();
        }

        private static async Task GetUsers(int count)
        {
            var genders = new List<Gender>();
            for(int i = 0; i < count; i++)
            {
                genders.Add((i % 2) == 0 ? Gender.Female : Gender.Male);
            }
            Iterator iterator = new Iterator(ASYNC_CONCURRENCY);
            await iterator.IterateAsync(genders, GetRandomUser);
        }

        private static async Task GetImages()
        {
            var imageUrls = new List<string>();
            foreach(var user in _users)
            {
                imageUrls.Add(user.GetImageUrl());
            }
            Iterator iterator = new Iterator(ASYNC_CONCURRENCY);
            await iterator.IterateAsync(imageUrls, GetUserImage);
        }

        private static async Task GetRandomUserAndImage(Gender gender)
        {
            var response = await _client.GetAsync($"{BASE_URL}?gender={gender.ToString()}");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = (JObject)JsonConvert.DeserializeObject(jsonString);
                if (jsonObject["results"] == null)
                {
                    throw new KeyNotFoundException("The random user response was invalid");
                }

                var randomUser = JsonConvert.DeserializeObject<List<User>>(jsonObject["results"].ToString())[0];
                await GetUserImage(randomUser.GetImageUrl());
            }
            else
            {
                throw new Exception("There was an error retrieving a random user");
            }
        }

        private static async Task GetRandomUser(Gender gender)
        {
            var response = await _client.GetAsync($"{BASE_URL}?gender={gender.ToString()}");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = (JObject)JsonConvert.DeserializeObject(jsonString);
                if (jsonObject["results"] == null)
                {
                    throw new KeyNotFoundException("The random user response was invalid");
                }

                var randomUser = JsonConvert.DeserializeObject<List<User>>(jsonObject["results"].ToString())[0];
                _users.Add(randomUser);
            }
            else
            {
                throw new Exception("There was an error retrieving a random user");
            }
        }

        private static async Task GetUserImage(string imageUrl)
        {
            int i = Interlocked.Increment(ref _userCount);
            string filePath = $"{IMAGE_DIRECTORY}/user{i}.jpg";

            string cachedFilePath;
            if (_imageCache.TryGetValue(imageUrl, out cachedFilePath))
            {
                File.Copy(cachedFilePath, filePath);
            }
            else
            {
                var response = await _client.GetAsync(imageUrl);

                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (var fileStream = File.Create(filePath))
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            await stream.CopyToAsync(fileStream, BUFFER_SIZE);
                        }
                    }
                    _imageCache[imageUrl] = filePath;
                }
                else
                {
                    throw new Exception("There was an error retrieving the user's image");
                }
            }
        }

        private async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
        {
            string stringData = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(stringData);
        }
    }
}
