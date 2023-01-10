using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STRATUS.CAD.Services.AzureServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Replay.Controllers
{
    internal class Bim360Folder
    {
        public static IConfigurationRoot _config = null;
        public static ServiceProvider _services = null;

        public static int Usage(int errorCode)
        {
            Console.WriteLine("Usage: Bim360Folder user");
            Console.WriteLine("       ");
            return errorCode;
        }

        static public async Task<int> Main(IConfigurationRoot config, ServiceProvider services, string[] args)
        {
            _config = config;
            _services = services;
            var block = _services.GetRequiredService<BlockBlobService>();
            return Usage(1);
        }

        public void ReplayLoad()
        {
            /*
             * Upload attachment, to model (Enum: Role = WireWorks Wire Pull, Feeder Schedl, etc.)
            FileStream testStream = new FileStream(
            // Upload a bunch of files, and see if (base 64, UUEncoded files breaks? Code that writes blocks can't handle)
            await block.UploadAsync(Guid.Parse("02ff41d0-c462-4fd3-b4bb-d761172c3148"),
            "https://modeldataqa.blob.core.windows.net/02ff41d0-c462-4fd3-b4bb-d761172c3148/fabconfig/Fabrication2022-TweetGarot/db_items_images_items.zip", testStream);
            */
        }
    }
}
