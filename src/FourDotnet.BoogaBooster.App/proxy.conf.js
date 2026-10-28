// Dev-server proxy: forwards the app's relative `/api/*` calls to the backend
// API. Under the Aspire AppHost the API endpoint is injected as an environment
// variable: Aspire 13's JavaScript integration names it
// `<RESOURCE>_<ENDPOINT>` (`FOURDOTNET_BOOGABOOSTER_API_HTTPS`); the older
// `services__<resource>__<endpoint>__0` service-discovery form is still read
// in case it is injected. The API's own launch-profile URL is the fallback
// when running `ng serve` standalone. `secure: false` accepts the ASP.NET
// development certificate.
const target =
  process.env['FOURDOTNET_BOOGABOOSTER_API_HTTPS'] ||
  process.env['FOURDOTNET_BOOGABOOSTER_API_HTTP'] ||
  process.env['services__fourdotnet-boogabooster-api__https__0'] ||
  process.env['services__fourdotnet-boogabooster-api__http__0'] ||
  'https://localhost:7069';

module.exports = [
  {
    context: ['/api'],
    target,
    secure: false,
    changeOrigin: true,
    pathRewrite: { '^/api': '' },
  },
];
