// Dev-server proxy: forwards the app's relative `/api/*` calls to the backend
// API. Under the Aspire AppHost the API endpoint is injected as a
// service-discovery environment variable; a localhost value is used as a
// fallback when running `ng serve` standalone. `secure: false` accepts the
// ASP.NET development certificate.
const target =
  process.env['services__fourdotnet-boogabooster-api__https__0'] ||
  process.env['services__fourdotnet-boogabooster-api__http__0'] ||
  'https://localhost:7001';

module.exports = [
  {
    context: ['/api'],
    target,
    secure: false,
    changeOrigin: true,
    pathRewrite: { '^/api': '' },
  },
];
