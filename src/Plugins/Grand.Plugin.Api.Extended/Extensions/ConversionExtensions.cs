using Grand.Api.DTOs.Catalog;
using Grand.Domain.Catalog;
using Grand.Plugin.Api.Extended.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Extensions
{
    public static class ConversionExtensions
    {
        public static ProductDto ToProductDto(this AliExpressProduct aliExpressProduct)
        {
            var dto = new ProductDto();
            dto.FullDescription = aliExpressProduct.Description;
            dto.Name = aliExpressProduct.Title;
            dto.MetaTitle = aliExpressProduct.Title;
            dto.ProductTypeId = ProductType.SimpleProduct;
            dto.ProductLayoutId = "621026006e254b2d02acf47f"; // Simple Layout
            dto.AllowCustomerReviews = true;
            dto.ShowOnHomePage = false;
            dto.Published = false;
            dto.Price = double.Parse( aliExpressProduct.OriginalPrice.Min.ToString() );
            dto.CatalogPrice = double.Parse(aliExpressProduct.SalePrice.Min.ToString() );
            dto.MinEnteredPrice = double.Parse(aliExpressProduct.SalePrice.Min.ToString());
            dto.MaxEnteredPrice = double.Parse(aliExpressProduct.SalePrice.Max.ToString());
            dto.StartPrice = double.Parse(aliExpressProduct.OriginalPrice.Min.ToString());
            dto.StockAvailability = aliExpressProduct.TotalAvailableQuantity > 0;
            dto.StockQuantity = int.Parse(aliExpressProduct.TotalAvailableQuantity.ToString());
            dto.DisplayStockQuantity = true;
            dto.VisibleIndividually = true;

            return dto;
        }
    }
}
