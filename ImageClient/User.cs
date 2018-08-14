
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ImageClient
{
    public class User
    {
        public Gender Gender { get; set; }
        public string Email { get; set; }

        [JsonProperty("picture")]
        private Dictionary<string,string> ImageUrls { get; set; }

        public string GetImageUrl(string imageSize = "medium")
        {
            string url;
            bool imageExists = ImageUrls.TryGetValue(imageSize, out url);
            if (!imageExists)
            {
                throw new KeyNotFoundException($"{this} does not contain an image with the size: {imageSize}");
            }
            return url;
        }

        public override string ToString()
        {
            return this.Email.ToString();
        }
    }

    public enum Gender
    {
        Female = 1,
        Male = 2
    }
}
