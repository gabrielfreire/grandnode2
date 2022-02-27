using Grand.Api.Commands.Models.Catalog;
using Grand.Api.Commands.Models.Common;
using Grand.Api.Controllers;
using Grand.Api.DTOs.Common;
using Grand.Business.Common.Interfaces.Security;
using Grand.Business.Common.Services.Security;
using Grand.Plugin.Api.Extended.Extensions;
using Grand.Plugin.Api.Extended.Models;
using Grand.Api.DTOs.Catalog;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Grand.Api.Queries.Models.Common;
using System.Text.RegularExpressions;
using Grand.Domain.Catalog;
using Grand.Business.Catalog.Interfaces.Products;
using Grand.Api.Extensions;
using Grand.Domain.Common;
using Serilog;
using Grand.Business.Catalog.Interfaces.Categories;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [SwaggerTag("Api Extended")]
    public partial class AliExpressController : BaseODataController
    {
        private readonly IProductAttributeService _productAttributeService;
        private readonly IMediator _mediator;
        private readonly IProductService _productService;
        private readonly IPermissionService _permissionService;

        public AliExpressController(
            IPermissionService permissionService,
            IMediator mediator,
            IProductService productService,
            IProductAttributeService productAttributeService)
        {
            _permissionService = permissionService;
            _mediator = mediator;
            _productService = productService;
            _productAttributeService = productAttributeService;
        }

        /// <summary>
        /// Returns a product from AliExpress by ID
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [SwaggerOperation(summary: "Get product from AliExpress by ID", OperationId = "GetProductById")]
        [HttpGet("Products/{key}")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get(string key)
        {
            if (!await _permissionService.Authorize(PermissionSystemName.Orders))
                return Forbid();

            var product = await AliExpressScraper.GetProductById(decimal.Parse(key));
            if (product == null)
                return NotFound();

            return Ok(product);
        }

        /*
         * login to get api token
         * {
              "email": "gabrielfreiredev@gmail.com",
              "password": "cGFzc3dvcmR0ZXN0"
            }
         * */

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// {
        ///     "aliCategoryId": "200002071",
        ///     "aliCategoryName": "cat-supplies",
        ///     "publishCategory": true,
        ///     "publishProducts": true,
        ///     "includeInMenu": true,
        ///     "showOnHomePage": false,
        ///     "allowCustomerToSelectPageSize": true,
        ///     "pageSize": 10,
        ///     "pageSizeOption": "10,20,30"
        /// }
        /// </remarks>
        /// <param name="body"></param>
        /// <returns></returns>
        [SwaggerOperation(summary: "Adds a bunch of products from AliExpress to the Store by category id and name", OperationId = "AddAliExpressProductsByCategoryIdAndName")]
        [HttpPost("Products/AddAliExpressProductsByCategoryIdAndName")]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddAliExpressProductsByCategoryIdAndName(
            [FromBody] AddAliExpressProductsByCategoryIdAndNameRequestBody body
            )
        {
            if (!await _permissionService.Authorize(PermissionSystemName.Orders))
                return Forbid();

            var aliExpressProducts = await AliExpressScraper.ListProductsByCategoryIdAndName(body.AliCategoryId, body.AliCategoryName);

            var products = new List<Product>();
            var displayOrder = 1;
            foreach(var aliProd in aliExpressProducts)
            {
                Log.Information($"Adding AliExpress prod '{aliProd.Title}' to store");
                try
                {
                    var p = await CreateProductFromAliExpressProduct(
                            aliProd,
                            body.PublishProducts,
                            body.PublishCategory,
                            body.ShowOnHomePage,
                            body.AllowCustomerToSelectPageSize,
                            body.IncludeInMenu,
                            body.PageSize,
                            body.PageSizeOption,
                            displayOrder,
                            new string[] { });
                    
                    if (p != null)
                    {
                        products.Add(p);
                        displayOrder++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, ex.ToString());
                }
            }

            return Ok(products.Select(p => p.Id).ToList());
        }
        /// <summary>
        /// Adds a product from AliExpress to the Store using AliExpress product ID
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [SwaggerOperation(summary: "Adds a product from AliExpress to the Store", OperationId = "AddAliExpressProduct")]
        [HttpPost("Products/{aliExpressProductId}/AddAliExpressProduct")]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddAliExpressProduct(
            string aliExpressProductId,
            [FromQuery] string[] categoriesId,
            [FromQuery] bool publish = false,
            [FromQuery] bool showOnHomePage = false,
            [FromQuery] int displayOrder = 1
            )
        {
            if (!await _permissionService.Authorize(PermissionSystemName.Orders))
                return Forbid();

            // get aliexpress product
            var aliExpressProduct = await AliExpressScraper.GetProductById(decimal.Parse(aliExpressProductId));
            
            // convert to productDto and add to database
            var product = await CreateProductFromAliExpressProduct(
                aliExpressProduct, 
                publish, 
                true,
                showOnHomePage,
                true,
                true,
                10,
                "10,15,20",
                displayOrder, 
                categoriesId);

            return Ok(product.ToModel());
        }

        private async Task<Product> CreateProductFromAliExpressProduct(
            AliExpressProduct aliExpressProduct, 
            bool publishProduct, 
            bool publishCategory, 
            bool showOnHomePage, 
            bool allowCustomersToSelectPageSize,
            bool IncludeInMenu,
            int pageSize,
            string pageSizeOptions,
            int displayOrder, 
            string[] categoriesId)
        {
            var productDto = aliExpressProduct.ToProductDto(publishProduct, showOnHomePage, displayOrder);
            productDto = await _mediator.Send(new AddProductCommand() {
                Model = productDto
            });
            var product = productDto.ToEntity();

            if (product != null)
            {
                // add aliexpress categories to store and insert them into the product
                product = await CreateCategoriesAndAddToProduct(aliExpressProduct, 
                    categoriesId, 
                    product,
                    publishCategory,
                    allowCustomersToSelectPageSize,
                    IncludeInMenu,
                    pageSize,
                    pageSizeOptions);
                
                // add User fields
                product.UserFields.Add(new Domain.Common.UserField() {
                    Key = "AliExpressProductUrl",
                    Value = $"https://www.aliexpress.com/item/{aliExpressProduct.Id}.html"
                });
                product.UserFields.Add(new Domain.Common.UserField() {
                    Key = "AliExpressProductId",
                    Value = $"{aliExpressProduct.Id}"
                });

                await _productService.UpdateProduct(product);

                // create pictures and add to productDto
                await CreatePicturesAndAddToProduct(aliExpressProduct.Images, product);
                product = await _productService.GetProductById(product.Id);
                // add product attributes to productDto
                product = await CreateProductAttributeMappingFromAliExpressVariants(aliExpressProduct, product);
                // add product attr combinations
                product = await CreateProductAttributeCombinationsFromAttributeMappings(aliExpressProduct, product);

            }
            return product;
        }
        private async Task<Product> CreateCategoriesAndAddToProduct(
            AliExpressProduct aliExpressProduct, 
            string[] categoriesId, 
            Product product,
            bool publishCategories,
            bool allowCustomersToSelectPageSize,
            bool IncludeInMenu,
            int pageSize,
            string pageSizeOptions)
        {
            // category creation
            if (categoriesId.Length == 0)
            {
                var categoriesToAdd = new List<string>();
                string parentCategoryId = null;
                // we store categories from aliExpress in our store if they don't exist already
                foreach (var aliProdCategory in aliExpressProduct.ProductCategories)
                {

                    var categoriesQuery = await _mediator.Send(new GetQuery<CategoryDto>());
                    if (categoriesQuery.Any(c => c.ExternalId == aliProdCategory.Id.ToString()))
                    {
                        var existingCategory = categoriesQuery.FirstOrDefault(c => c.ExternalId == aliProdCategory.Id.ToString());

                        // is it the last?
                        if (existingCategory.ExternalId == aliExpressProduct.ProductCategories.Last().Id.ToString())
                        {
                            categoriesToAdd.Add(categoriesQuery.FirstOrDefault(c => c.ExternalId == aliProdCategory.Id.ToString()).Id);
                        }
                        else // skip if not
                        {
                            parentCategoryId = existingCategory.Id;
                            continue;
                        }
                    }
                    else
                    {
                        var newCategoryAddedFromAliExpress = await _mediator.Send(new AddCategoryCommand() {
                            Model = new CategoryDto() {
                                Name = aliProdCategory.Name,
                                ParentCategoryId = parentCategoryId,
                                CategoryLayoutId = KnownIds.CATEGORY_LAYOUT_GRID_OR_LINES,
                                Published = publishCategories,
                                DisplayOrder = 1,
                                PageSize = pageSize,
                                PictureId = product.ProductPictures.FirstOrDefault()?.PictureId,
                                ShowOnSearchBox = true,
                                AllowCustomersToSelectPageSize = allowCustomersToSelectPageSize,
                                PageSizeOptions = pageSizeOptions,
                                ExternalId = aliProdCategory.Id.ToString(),
                                FeaturedProductsOnHomePage = false,
                                IncludeInMenu = IncludeInMenu
                            }
                        });

                        if (newCategoryAddedFromAliExpress != null)
                        {
                            parentCategoryId = newCategoryAddedFromAliExpress.Id;
                            categoriesToAdd.Clear(); // only add the latest category
                            categoriesToAdd.Add(newCategoryAddedFromAliExpress.Id);
                        }
                        else
                            parentCategoryId = null;
                    }
                }
                categoriesId = categoriesToAdd.ToArray();
            }
            else
            {
                foreach (var cateId in categoriesId)
                {
                    var cat = await _mediator.Send(new GetQuery<CategoryDto>() { Id = cateId });
                    if (!cat.Any())
                        throw new InvalidOperationException($"Category Id {cateId} was not found");
                }
            }

            // add categories
            foreach (var cateId in categoriesId)
            {
                await _mediator.Send(new AddProductCategoryCommand() {
                    Product = product.ToModel(),
                    Model = new ProductCategoryDto() {
                        CategoryId = cateId,
                        IsFeaturedProduct = false
                    }
                });
            }
            return await _productService.GetProductById(product.Id);
        }

        private async Task<List<PictureDto>> CreatePicturesAndAddToProduct(List<string> imagesUrl, Product product)
        {
            using var http = new HttpClient();
            var _addedPictureDtos = new List<PictureDto>();

            // create images in the Store
            var order = 1;
            foreach (var img in imagesUrl)
            {
                byte[] pictureBytes = null;
                try
                {
                    using var response = await http.GetAsync(img);
                    response.EnsureSuccessStatusCode();
                    pictureBytes = await response.Content.ReadAsByteArrayAsync();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, ex.ToString());
                }

                if (pictureBytes != null && pictureBytes.Length > 0)
                {
                    var pictureDto = await _mediator.Send(new AddPictureCommand() {
                        PictureDto = new Grand.Api.DTOs.Common.PictureDto() {
                            PictureBinary = pictureBytes,
                            AltAttribute = img,
                            MimeType = "image/jpeg"
                        }
                    });
                    if (pictureDto != null)
                    {
                        await _mediator.Send(new AddProductPictureCommand() {
                            Product = product.ToModel(),
                            Model = new Grand.Api.DTOs.Catalog.ProductPictureDto() {
                                DisplayOrder = order,
                                PictureId = pictureDto.Id
                            }
                        });
                        _addedPictureDtos.Add(pictureDto);
                        order++;
                    }
                }
            }

            return _addedPictureDtos;
        }
        private async Task<Product> CreateProductAttributeMappingFromAliExpressVariants(AliExpressProduct aliExpressProduct, Product product)
        {
            // add product attributes to productDto
            var _options = aliExpressProduct.Variants.Options;
            var _attributeMappingDisplayOrder = 1;
            foreach (var option in _options)
            {
                // default to size
                var _productAttrId = KnownIds.PRODUCT_ATTRIBUTE_ID_SIZE; // size
                var _productAttrControlTypeId = AttributeControlType.DropdownList;
                var _pictureId = string.Empty;
                var _pictureDtos = new List<PictureDto>();
                // color attr mapping
                if (option.Name.ToLowerInvariant().Contains(KnownAliExpressAttrNames.Color))
                {
                    _productAttrId = KnownIds.PRODUCT_ATTRIBUTE_ID_COLOR;
                    _productAttrControlTypeId = AttributeControlType.ImageSquares;
                    var colorOptionImagesUrls = option.Values.Select(v => v.ImagePath).ToList();
                    _pictureDtos = await CreatePicturesAndAddToProduct(colorOptionImagesUrls, product);
                }
                // size attr mapping
                else if (option.Name.ToLowerInvariant().Contains(KnownAliExpressAttrNames.Size))
                {
                    _productAttrId = KnownIds.PRODUCT_ATTRIBUTE_ID_SIZE;
                }
                // ship from attr mapping
                else if (option.Name.ToLowerInvariant().Contains(KnownAliExpressAttrNames.Ships))
                {
                    _productAttrId = KnownIds.PRODUCT_ATTRIBUTE_ID_SHIPS;
                }
                // create new attr to use
                else
                {
                    var _productAttr = new ProductAttribute() {
                        Name = option.Name,
                        SeName = option.Name.ToLowerInvariant()
                    };
                    await _productAttributeService.InsertProductAttribute(_productAttr);
                    _productAttrId = _productAttr.Id;
                }

                var productAttributeMappingDto = new ProductAttributeMappingDto() {
                    AttributeControlTypeId = _productAttrControlTypeId,
                    ProductAttributeId = _productAttrId,
                    IsRequired = true,
                    DisplayOrder = _attributeMappingDisplayOrder,
                    ProductAttributeValues = new List<ProductAttributeValueDto>()
                };

                // add attr mapping values
                var _optionValueDisplayOrder = 1;

                foreach (var val in option.Values)
                {
                    var optionValuePrice = aliExpressProduct.HasMultipleVariants() ? null :
                        aliExpressProduct.GetPriceValueByOptionValueId(val.Id.ToString());

                    var attrValueDto = new ProductAttributeValueDto();
                    attrValueDto.Name = val.DisplayName;
                    attrValueDto.PictureId = _pictureDtos.Count > 0 ?
                        _pictureDtos.FirstOrDefault(p => p.AltAttribute == val.ImagePath)?.Id :
                        null;
                    attrValueDto.ImageSquaresPictureId = _pictureDtos.Count > 0 ?
                        _pictureDtos.FirstOrDefault(p => p.AltAttribute == val.ImagePath)?.Id :
                        null;
                    attrValueDto.DisplayOrder = _optionValueDisplayOrder;
                    attrValueDto.PriceAdjustment = optionValuePrice == null ? default : double.Parse(optionValuePrice.SalePrice.ToString());
                    attrValueDto.Quantity = optionValuePrice == null ? 1 : int.Parse( optionValuePrice.AvailableQuantity.ToString() );
                    productAttributeMappingDto.ProductAttributeValues.Add(attrValueDto);

                    _optionValueDisplayOrder++;

                }

                // save mapping to database
                var attrMapping = await SaveProductAttribute(productAttributeMappingDto, product.Id);
                _attributeMappingDisplayOrder++;
            }
            return await _productService.GetProductById(product.Id);
        }

        private async Task<ProductAttributeMappingDto> SaveProductAttribute(ProductAttributeMappingDto dto, string productId)
        {
            var productAttributeMapping = dto.ToEntity();
            productAttributeMapping.Combination = true;
            await _productAttributeService.InsertProductAttributeMapping(productAttributeMapping, productId);
            return productAttributeMapping.ToModel();
        }

        private async Task<Product> CreateProductAttributeCombinationsFromAttributeMappings(
            AliExpressProduct aliExpressProduct,
            Product product)
        {
            var prices = aliExpressProduct.Variants.Prices;

            foreach (var price in prices)
            {
                var adjustedPrice = double.Parse(price.SalePrice.ToString());
                
                var optionsId = !string.IsNullOrEmpty(price.OptionValueIds) ? price.OptionValueIds.Split(",") : new string[] { };
                
                var combination = new ProductAttributeCombination() {
                    PictureId = null,
                    StockQuantity = int.Parse(price.AvailableQuantity.ToString()),
                    OverriddenPrice = adjustedPrice,
                    Attributes = new List<CustomAttribute>()
                };
                
                foreach(var optionId in optionsId)
                {
                    var value = aliExpressProduct.GetOptionValueById(optionId);
                    if (value != null)
                    {
                        var pictureId = value.ImagePath != null ? value.ImagePath : null;
                        var productAttributeMappingInDto = product.ProductAttributeMappings
                            .FirstOrDefault(p => p.ProductAttributeValues.Any(v => v.Name == value.DisplayName));
                        
                        if (productAttributeMappingInDto != null)
                        {
                            var productAttributeValueInDto = productAttributeMappingInDto.ProductAttributeValues
                                .FirstOrDefault(v => v.Name == value.DisplayName);

                            if (productAttributeValueInDto != null)
                            {
                                if (productAttributeMappingInDto.AttributeControlTypeId == AttributeControlType.ImageSquares)
                                {
                                    combination.PictureId = productAttributeValueInDto.PictureId;
                                }
                                combination.Attributes.Add(new CustomAttribute() {
                                    Key = productAttributeMappingInDto.Id,
                                    Value = productAttributeValueInDto.Id
                                });
                            }
                        }
                    }
                }

                if (combination != null)
                {
                    await SaveProductCombination(combination, product.Id);
                }
            }
            return await _productService.GetProductById(product.Id);
        }

        private async Task SaveProductCombination(ProductAttributeCombination entity, string productId)
        {
            await _productAttributeService.InsertProductAttributeCombination(entity, productId);
        }
    }
}
