using Grand.Plugin.Api.Extended.Extensions;
using Grand.Plugin.Api.Extended.Models;
using PuppeteerSharp;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended
{
    public class AliExpressScraper
    {
        public static async Task<List<AliExpressProduct>> ListProductsByCategoryIdAndName(
            string categoryId,
            string categoryName,
            bool headlessBrowser = true)
        {
            var aliExpressproducts = new List<AliExpressProduct>();

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions() {
                Headless = headlessBrowser,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" },
                DefaultViewport = new ViewPortOptions() { IsLandscape = true }
            });

            var page = await browser.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions() {
                Width = 1200,
                Height = 800
            });
            await page.GoToAsync($"https://www.aliexpress.com/category/{categoryId}/{categoryName}.html");

            var bodyClientHeightTask = page.EvaluateFunctionAsync<int>("() => document.body.scrollHeight");
            var windowScrollYTask = page.EvaluateFunctionAsync<int>("() => window.scrollY");

            var screenHeight = await bodyClientHeightTask;
            var scrollY = await windowScrollYTask;

            var totalHeight = 0;
            var distance = 1000;

            while (totalHeight <= screenHeight)
            {
                await page.EvaluateFunctionAsync($"() => window.scrollBy(0, {distance})");

                screenHeight = await page.EvaluateFunctionAsync<int>("() => document.body.scrollHeight");
                scrollY = await page.EvaluateFunctionAsync<int>("() => window.scrollY");

                totalHeight += distance;

                await Task.Delay(100);
            }

            var hrefs = (await page.QuerySelectorAllAsync("a._3t7zg"))
                .Select(async a => await a.GetAttributeAsync("href"))
                .ToList();
            
            var reg = new Regex(@"item\/\d+\.html");

            var productIds = new List<string>();
            foreach (var href in hrefs)
            {
                var productId = reg.Match(await href).Value
                    .Replace(".html", "")
                    .Replace("item/", "");
                
                productIds.Add(productId);
            }

            foreach (var id in productIds)
            {
                Log.Information($" - Scraping product {id}");
                try
                {
                    aliExpressproducts.Add(await GetProductById(decimal.Parse(id), true, page));
                }
                catch(Exception ex)
                {
                    Log.Fatal(ex, ex.ToString());
                }
            }
            return aliExpressproducts;
        }

        public static async Task<AliExpressProduct> GetProductById(
            decimal productId,
            bool headlessBrowser = true,
            Page page = null)
        {
            if (page == null)
            {
                await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions() {
                    Headless = headlessBrowser,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" },
                    DefaultViewport = new ViewPortOptions() { IsLandscape = true }
                });

                page = await browser.NewPageAsync();
            }
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
                ProductCategories = data["crossLinkModule"]["breadCrumbPathList"] != null ? 
                    data["crossLinkModule"]?["breadCrumbPathList"]?
                        .Where(l => (decimal)l["cateId"] != 0)
                        .OrderBy(l => (decimal)l["cateId"])
                        .Select(l => new AliProductCategory
                        {
                            Id = (decimal)l["cateId"],
                            Name = (string)l["name"],
                            Target = (string)l["target"],
                            Url = (string)l["url"]
                        })?.ToList() :
                        new List<AliProductCategory>(),
                TotalAvailableQuantity = (decimal) data["quantityModule"]["totalAvailQuantity"],
                Orders = (decimal)data["titleModule"]["tradeCount"],
                DescriptionUrl = descriptionUrl.ToString(),
                Description = descriptionData,
                Images = data["imageModule"]["imagePathList"] != null ? 
                    data["imageModule"]?["imagePathList"]?.Select(li => (string)li)?.ToList() :
                    new List<string>(),
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
                    Options = data["skuModule"]["productSKUPropertyList"] != null ? 
                        data["skuModule"]?["productSKUPropertyList"]?.Select
                            (d => new AliVariantOption
                            {
                                Id = (decimal)d["skuPropertyId"],
                                Name = (string)d["skuPropertyName"],
                                Values = d["skuPropertyValues"]?.Select(pv => new OptionValue
                                {
                                    Id = (decimal)pv["propertyValueId"],
                                    Name = (string)pv["propertyValueName"],
                                    DisplayName = (string)pv["propertyValueDisplayName"],
                                    ImagePath = (string)pv["skuPropertyImagePath"]
                                })?.ToList()
                            })?.ToList() :
                            new List<AliVariantOption>(),
                    Prices = data["skuModule"]["skuPriceList"] != null ? 
                        data["skuModule"]?["skuPriceList"]?.Select
                            (d => new AliVariantPrice
                            {
                                Id = (decimal)d["skuId"],
                                AvailableQuantity = (decimal)d["skuVal"]["availQuantity"],
                                OptionValueIds = (string)d["skuPropIds"],
                                OriginalPrice = (decimal)d["skuVal"]["skuAmount"]["value"],
                                SalePrice = d["skuVal"]["skuActivityAmount"] != null ?
                                    (decimal)d["skuVal"]["skuActivityAmount"]["value"] :
                                    (decimal)d["skuVal"]["skuAmount"]["value"]
                            })?.ToList() :
                            new List<AliVariantPrice>()

                }
            };
            return aliProduct;
        }
    }
}
