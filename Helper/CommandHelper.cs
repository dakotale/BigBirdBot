using OpenAI_API.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Helper
{
    public class CommandHelper
    {
        public CommandHelper() { }

        public async Task<ImageResult?> GetImage(string prompt)
        {
            ImageResult? image = new ImageResult();
            try
            {
                if (prompt.Trim().Length > 0)
                {
                    var api = new OpenAI_API.OpenAIAPI(Constants.Constants.openAiSecret);
                    ImageGenerationRequest request = new ImageGenerationRequest();
                    request.Size = ImageSize._256;
                    request.Prompt = prompt;
                    request.NumOfImages = 1;
                    image = await api.ImageGenerations.CreateImageAsync(request);
                }
            }
            catch (Exception ex)
            {
                return image;
            }
            
            return image;
        }
    }
}
