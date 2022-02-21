using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Models
{
    public class AliExpressProduct
    {
        public decimal Id { get; set; }
        public string Title { get; set; }
        public decimal ActionCategoryId { get; set; }
        public List<AliProductCategory> ProductCategories { get; set; } = new List<AliProductCategory>();
        public decimal TotalAvailableQuantity { get; set; }
        public decimal Orders { get; set; }
        public string DescriptionUrl { get; set; }
        public string Description { get; set; }
        public AliShop Shop { get; set; }
        public AliRating Rating { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public string Currency { get; set; }
        public AliProductPrice OriginalPrice { get; set; }
        public AliProductPrice SalePrice { get; set; }
        public AliProductVariant Variants { get; set; }

        public AliVariantOption GetOptionByValueId(string valueId)
        {
            return Variants.Options.FirstOrDefault(o => o.Values.Any(v => v.Id == decimal.Parse(valueId)));
        }
        public OptionValue GetOptionValueById(string valueId)
        {
            return Variants.Options.SelectMany(o => o.Values).FirstOrDefault(o => o.Id == decimal.Parse(valueId));
        }
        public AliVariantPrice GetPriceValueByOptionValueId(string valueId)
        {
            return Variants.Prices.FirstOrDefault(p => p.OptionValueIds == valueId);
        }

        public bool HasMultipleVariants() => Variants.Prices.Any(p => p.OptionValueIds.Split(",").Length > 1);
    }

    public class AliProductCategory
    {
        public decimal Id { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public string Url { get; set; }
    }
    public class AliShop
    {
        public decimal Id { get; set; }
        public decimal CompanyId { get; set; }
        public string Name { get; set; }
        public decimal StoreNumber { get; set; }
        public decimal Followers { get; set; }
        public decimal RatingCount { get; set; }
        public string Rating { get; set; }
    }
    public class AliRating
    {
        public decimal TotalStar { get; set; } = 5;
        public decimal AverageStar { get; set; }
        public decimal TotalStarCount { get; set; }
        public decimal FiveStarCount { get; set; }
        public decimal FourStarCount { get; set; }
        public decimal ThreeStarCount { get; set; }
        public decimal TwoStarCount { get; set; }
        public decimal OneStarCount { get; set; }
    }
    public class AliProductPrice
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }
    public class AliProductVariant
    {
        public List<AliVariantOption> Options { get; set; } = new List<AliVariantOption>();
        public List<AliVariantPrice> Prices { get; set; } = new List<AliVariantPrice>();
    }
    public class AliVariantOption
    {
        public decimal Id { get; set; }
        public string Name { get; set; }
        public List<OptionValue> Values { get; set; } = new List<OptionValue>();
    }
    public class OptionValue
    {
        public decimal Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ImagePath { get; set; }
    }
    public class AliVariantPrice
    {
        public decimal Id { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string OptionValueIds{ get; set; }
        public string OptionValueId1 { get => !string.IsNullOrEmpty( OptionValueIds ) ? 
                OptionValueIds.Split(",").Length > 0 ? OptionValueIds.Split(",")[0] : string.Empty : string.Empty; }
        public string OptionValueId2 { get => !string.IsNullOrEmpty( OptionValueIds ) ? 
                OptionValueIds.Split(",").Length > 1 ? OptionValueIds.Split(",")[1] : string.Empty : string.Empty; }
        public decimal OriginalPrice { get; set; }
        public decimal SalePrice { get; set; }
    }
}
