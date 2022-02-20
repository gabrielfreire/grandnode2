using Grand.Plugin.Api.Extended.Models;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended
{
    public class AliExpressScraper
    {
        public static async Task<AliExpressProduct> GetProductById(
            decimal productId,
            bool headlessBrowser = true)
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions() {
                Headless = headlessBrowser,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" },
                DefaultViewport = new ViewPortOptions() { IsLandscape = true }
            });

            var page = await browser.NewPageAsync();
            await page.GoToAsync($"https://www.aliexpress.com/item/{productId}.html");

            var runParams = await page.EvaluateFunctionAsync("() => runParams");
            var data = runParams["data"];
            var descriptionUrl = data["descriptionModule"]["descriptionUrl"];
            await page.GoToAsync(descriptionUrl.ToString());
            var descriptionData = await page.GetContentAsync();

            var aliProduct = new AliExpressProduct
            {
                Id = productId,
                Title = (string)data["titleModule"]["subject"],
                ActionCategoryId = (decimal)data["actionModule"]["categoryId"],
                ProductCategories = data["crossLinkModule"]["breadCrumbPathList"]
                    .Where(l => (decimal)l["cateId"] != 0)
                    .OrderBy(l => (decimal)l["cateId"])
                    .Select(l => new AliProductCategory
                    {
                        Id = (decimal)l["cateId"],
                        Name = (string)l["name"],
                        Target = (string)l["target"],
                        Url = (string)l["url"]
                    }).ToList(),
                TotalAvailableQuantity = (decimal) data["quantityModule"]["totalAvailQuantity"],
                Orders = (decimal)data["titleModule"]["tradeCount"],
                DescriptionUrl = descriptionUrl.ToString(),
                Description = descriptionData,
                Images = data["imageModule"]["imagePathList"].Select(li => (string)li).ToList(),
                Shop = new AliShop
                {
                    Name = (string)data["storeModule"]["storeName"],
                    Id = (decimal)data["storeModule"]["companyId"],
                    CompanyId = (decimal)data["storeModule"]["companyId"],
                    StoreNumber = (decimal)data["storeModule"]["storeNum"],
                    Followers = (decimal)data["storeModule"]["followingNumber"],
                    RatingCount = (decimal)data["storeModule"]["positiveNum"],
                    Rating = (string
                    )data["storeModule"]["positiveRate"],
                },
                Rating = new AliRating
                {
                    TotalStar = 5,
                    AverageStar = (decimal)data["titleModule"]["feedbackRating"]["averageStar"],
                    TotalStarCount = (decimal)data["titleModule"]["feedbackRating"]["totalValidNum"],
                    FiveStarCount = (decimal)data["titleModule"]["feedbackRating"]["fiveStarNum"],
                    FourStarCount = (decimal)data["titleModule"]["feedbackRating"]["fourStarNum"],
                    ThreeStarCount = (decimal)data["titleModule"]["feedbackRating"]["threeStarNum"],
                    TwoStarCount = (decimal)data["titleModule"]["feedbackRating"]["twoStarNum"],
                    OneStarCount = (decimal)data["titleModule"]["feedbackRating"]["oneStarNum"]
                },
                Currency = (string)data["webEnv"]["currency"],
                OriginalPrice = new AliProductPrice
                {
                    Min = (decimal)data["priceModule"]["minAmount"]["value"],
                    Max = (decimal)data["priceModule"]["maxAmount"]["value"]
                },
                SalePrice = new AliProductPrice
                {
                    Min = data["priceModule"]["minActivityAmount"] != null ?
                        (decimal)data["priceModule"]["minActivityAmount"]["value"] :
                        (decimal)data["priceModule"]["minAmount"]["value"],
                    Max = data["priceModule"]["maxActivityAmount"] != null ?
                        (decimal)data["priceModule"]["maxActivityAmount"]["value"] :
                        (decimal)data["priceModule"]["maxAmount"]["value"]
                },
                Variants = new AliProductVariant 
                {
                    Options = data["skuModule"]["productSKUPropertyList"].Select
                        (d => new AliVariantOption
                        {
                            Id = (decimal)d["skuPropertyId"],
                            Name = (string)d["skuPropertyName"],
                            Values = d["skuPropertyValues"].Select(pv => new OptionValue
                            {
                                Id = (decimal)pv["propertyValueId"],
                                Name = (string)pv["propertyValueName"],
                                DisplayName = (string)pv["propertyValueDisplayName"],
                                ImagePath = (string)pv["skuPropertyImagePath"]
                            }).ToList()
                        }).ToList(),
                    Prices = data["skuModule"]["skuPriceList"].Select
                        (d => new AliVariantPrice
                        {
                            Id = (decimal)d["skuId"],
                            AvailableQuantity = (decimal)d["skuVal"]["availQuantity"],
                            OptionValueIds = (string)d["skuPropIds"],
                            OriginalPrice = (decimal)d["skuVal"]["skuAmount"]["value"],
                            SalePrice = d["skuVal"]["skuActivityAmount"] != null ?
                                (decimal)d["skuVal"]["skuActivityAmount"]["value"] :
                                (decimal)d["skuVal"]["skuAmount"]["value"]
                        }).ToList()

                }
            };
            return aliProduct;
        }
    }
}
