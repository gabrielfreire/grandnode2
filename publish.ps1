dotnet build src/Plugins/Authentication.Facebook; `
dotnet build src/Plugins/Authentication.Google; `
dotnet build src/Plugins/DiscountRules.Standard; `
dotnet build src/Plugins/ExchangeRate.McExchange; `
dotnet build src/Plugins/Grand.Plugin.Api.Extended; `
dotnet build src/Plugins/Payments.BrainTree; `
dotnet build src/Plugins/Payments.CashOnDelivery; `
dotnet build stc/Plugins/Payments.PayPalStandard; `
dotnet build src/Plugins/Shipping.ByWeight; `
dotnet build src/Plugins/Shipping.FixedRateShipping; `
dotnet build src/Plugins/Shipping.ShippingPoint; `
dotnet build src/Plugins/Tax.CountryStateZip; `
dotnet build stc/Plugins/Tax.FixedRate; `
dotnet build src/Plugins/Widgets.FacebookPixel; `
dotnet build stc/Plugins/Widgets.GoogleAnalytics; `
dotnet build src/Plugins/Widgets.Slider; `
dotnet publish src/Web/Grand.Web -c Release