using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using STRATUS.CAD.Models.StratusModels;
using STRATUS.CAD.Repos.MONGO;
using STRATUS.CAD.Services.StratusServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Replay.Controllers
{
    public class User
    {
        public static IConfigurationRoot _config = null;
        public static ServiceProvider _services = null;
        public static StratusUserService userService;

        public static int Usage(int errorCode)
        {
            Console.WriteLine("Usage: -user <<AzureADId> | <GTP UserId>>");
            Console.WriteLine("       Call Model Pipeline function endpoints to get the user.");
            return errorCode;
        }


        // First hit: mongodb+srv://gtp-user:ltkuev1sXmYXBgC3@stratuscad-qa.onttl.mongodb.net/?authMechanism=SCRAM-SHA-1;w=majority;connectTimeout=1h;maxIdleTime=1m;maxPoolSize=200;minPoolSize=25;waitQueueTimeout=3m;retryWrites=true
        //            mongodb+srv://gtp-user:ltkuev1sXmYXBgC3@stratuscad-qa-pri.onttl.mongodb.net/?authMechanism=SCRAM-SHA-1;w=majority;connectTimeout=1h;maxIdleTime=1m;maxPoolSize=200;minPoolSize=25;waitQueueTimeout=3m;retryWrites=true
        //            mongodb+srv://gtp-admin:3PF2G5GF1e1yHUSR@stratuscad-qa-pri.onttl.mongodb.net/?authMechanism=SCRAM-SHA-1;w=majority;connectTimeout=1h;maxIdleTime=1m;maxPoolSize=200;minPoolSize=25;waitQueueTimeout=3m;retryWrites=true


        public static IMongoDatabase GetMasterDatabase()
        {
            var con = string.Empty;

            con = "mongodb+srv://gtp-user:ltkuev1sXmYXBgC3@stratuscad-qa.onttl.mongodb.net/?retryWrites=true&w=majority&authMechanism=SCRAM-SHA-1"; // No master
            con = "mongodb+srv://gtp-user:ltkuev1sXmYXBgC3@qa-pri.onttl.azure.mongodb.net/?authMechanism=SCRAM-SHA-1;w=majority;connectTimeout=1h;maxIdleTime=1m;maxPoolSize=200;minPoolSize=25;waitQueueTimeout=3m;retryWrites=true";
            con = "mongodb+srv://gtp-user:ltkuev1sXmYXBgC3@qa.onttl.azure.mongodb.net/?retryWrites=true&w=majority&authMechanism=SCRAM-SHA-1";
            MongoClient dbClient = new MongoClient(con);
            var master = dbClient.GetDatabase("Master");

            /*
            var dbList = dbClient.ListDatabases().ToList();

            Console.WriteLine("The list of databases on this server is: ");
            foreach (var db in dbList)
            {
                Console.WriteLine(db);
            }

            var collections = master.ListCollections().ToList();
            foreach (var collection in collections)
            {
                Console.WriteLine(collection.ToString());
            }
            */
            return master;
        }

        static public void GetUser(IMongoDatabase masterDatabase, string azId)
        {
            var embeddedResourceManager = _services.GetRequiredService<EmbeddedResourceManager>();
            var aggregateName = $"MONGO.EmbeddedResources.User-Get.json";
            var aggregateJson = embeddedResourceManager.GetJsonStringAsync(aggregateName, removeComments: true).Result;
            aggregateJson = aggregateJson.Replace("#azure_ad_id#", azId);
            aggregateJson = aggregateJson.Replace("#user_id#", azId);

            var pipeline = BsonSerializer.Deserialize<BsonDocument[]>(aggregateJson).ToList();

            var user = masterDatabase.GetCollection<dynamic>("User");
            var filter = Builders<dynamic>.Filter.Eq("AzureADId", azId);
            var whoAmi = user.Find<dynamic>(filter).FirstOrDefault();
            Console.WriteLine(whoAmi.Email);

            var cursor = user.Aggregate<BsonDocument>(pipeline);
            var results = cursor.ToList();
            var item = results.FirstOrDefault();
            if (item == null)
                Console.WriteLine($"No user could be found for given id {azId}");
            else
                Console.WriteLine(item.ToJson());
        }

        static public async Task<int> Main(IConfigurationRoot config, ServiceProvider services, string[] args)
        {
            var azId = args[1];
            _config = config;
            _services = services;
            userService = _services.GetRequiredService<StratusUserService>();
            var mongoProvider = _services.GetRequiredService<MongoDatabaseProvider>();
            var masterDatabase = mongoProvider.MasterDatabase(true);
            // masterDatabase = GetMasterDatabase();
            // GetUser(masterDatabase, azId);

            var jsonResponse = await userService.GetUserJsonAsync(azId, azId);
            return 1;
        }
    }
}
