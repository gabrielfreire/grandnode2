using Grand.Api.DTOs.Catalog;
using Grand.Domain.Catalog;
using Grand.Domain.Data;
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
        public static ProductDto ToProductDto(this AliExpressProduct aliExpressProduct, 
            bool publish = false, 
            bool showOnHomePage = false,
            int displayOrder = 1)
        {
            var dto = new ProductDto();
            dto.Id = UniqueIdentifier.New;
            dto.FullDescription = aliExpressProduct.Description;
            dto.Name = aliExpressProduct.Title;
            dto.MetaTitle = aliExpressProduct.Title;
            dto.ProductTypeId = ProductType.SimpleProduct;
            dto.ProductLayoutId = KnownIds.PRODUCT_LAYOUT_ID_SIMPLE_LAYOUT; // Simple Layout
            dto.OrderMaximumQuantity = 999999;
            dto.OrderMinimumQuantity = 1;
            dto.AvailableForPreOrder = false;
            dto.AllowCustomerReviews = true;
            dto.ShowOnHomePage = showOnHomePage;
            dto.Published = publish;
            dto.ManageInventoryMethodId = ManageInventoryMethod.ManageStockByAttributes;
            
            dto.ProductCost = double.Parse( aliExpressProduct.SalePrice.Min.ToString() );
            dto.Price = double.Parse( aliExpressProduct.SalePrice.Min.ToString() );
            dto.CatalogPrice = double.Parse(aliExpressProduct.OriginalPrice.Min.ToString() );
            dto.MinEnteredPrice = double.Parse(aliExpressProduct.SalePrice.Min.ToString());
            dto.MaxEnteredPrice = double.Parse(aliExpressProduct.SalePrice.Max.ToString());
            dto.StartPrice = double.Parse(aliExpressProduct.OriginalPrice.Min.ToString());
            dto.StockAvailability = aliExpressProduct.TotalAvailableQuantity > 0;
            dto.StockQuantity = int.Parse(aliExpressProduct.TotalAvailableQuantity.ToString());
            dto.DisplayStockQuantity = true;
            dto.VisibleIndividually = true;
            
            dto.DisplayOrder = displayOrder;

            return dto;
        }
    }
}
