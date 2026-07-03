var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FourDotnet_BoogaBooster_Api>("fourdotnet-boogabooster-api");

builder.Build().Run();
