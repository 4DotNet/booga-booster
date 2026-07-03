using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// RabbitMQ backs the Dapr pub/sub component. Fixed, parameter-backed credentials and host port keep
// the connection string the Dapr component uses stable across restarts.
var rabbitMqUsername = builder.AddParameter("rabbitmq-username", secret: true);
var rabbitMqPassword = builder.AddParameter("rabbitmq-password", secret: true);
var rabbitmq = builder.AddRabbitMQ("messaging", rabbitMqUsername, rabbitMqPassword, port: 5672)
    .WithManagementPlugin(port: 15672);

var rabbitMqConnectionString = ReferenceExpression.Create(
    $"amqp://{rabbitMqUsername.Resource}:{rabbitMqPassword.Resource}@{rabbitmq.GetEndpoint("tcp").Property(EndpointProperty.HostAndPort)}");

// Dapr pub/sub component configured PROGRAMMATICALLY via the Aspire Community Toolkit for Dapr —
// no static components/*.yaml. The component name must match IntegrationMessagingDefaults.PubSubName.
var pubSub = builder.AddDaprComponent("pubsub", "pubsub.rabbitmq")
    .WithMetadata("connectionString", rabbitMqConnectionString)
    // Parameter-backed secret metadata makes the toolkit emit the local secret store the
    // value-provider-backed metadata requires.
    .WithMetadata("username", rabbitMqUsername.Resource)
    .WithMetadata("password", rabbitMqPassword.Resource)
    .WaitFor(rabbitmq);

var api = builder.AddProject<Projects.FourDotnet_BoogaBooster_Api>("fourdotnet-boogabooster-api")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithDaprSidecar(sidecar => sidecar
        // The API serves its Dapr endpoints over HTTPS (and uses HTTPS redirection), so the
        // sidecar must speak HTTPS on the app channel. Without this, daprd's startup fetch of
        // /dapr/subscribe hits an HTTP->HTTPS redirect and no subscriptions are registered.
        .WithOptions(new DaprSidecarOptions { AppProtocol = "https" })
        .WithReference(pubSub));

// Angular frontend (Aspire JavaScript integration). AddViteApp runs the "dev" npm script,
// installs dependencies, and injects the assigned port (--port/PORT) and API service-discovery
// env vars. WithExternalHttpEndpoints exposes it outside the app network.
builder.AddViteApp("frontend", "../../FourDotnet.BoogaBooster.App")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
