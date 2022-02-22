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

        /// <summary>
        /// Adds a product from AliExpress to the Store using AliExpress product ID
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [SwaggerOperation(summary: "Adds a product from AliExpress to the Store", OperationId = "AddAliExpressProduct")]
        [HttpPost("Products/{aliExpressProductId}/AddAliExpressProduct")]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> Post(
            string aliExpressProductId,
            [FromQuery] string[] categoriesId
            )
        {
            if (!await _permissionService.Authorize(PermissionSystemName.Orders))
                return Forbid();

            // get aliexpress product
            var aliExpressProduct = await AliExpressScraper.GetProductById(decimal.Parse(aliExpressProductId));
            
            // convert to productDto and add to database
            var productDto = await _mediator.Send(new AddProductCommand() { Model = aliExpressProduct.ToProductDto() });

            if (productDto != null)
            {
                // add aliexpress categories to store and insert them into the product
                try
                {
                    productDto = await CreateCategoriesAndAddToProduct(aliExpressProduct, categoriesId, productDto);
                }
                catch (InvalidOperationException ex)
                {
                    return NotFound();
                }

                // add User fields
                var product = productDto.ToEntity();
                product.UserFields.Add(new Domain.Common.UserField() 
                { 
                    Key = "AliExpressProductUrl",
                    Value = $"https://www.aliexpress.com/item/{aliExpressProductId}.html" 
                });
                product.UserFields.Add(new Domain.Common.UserField() {
                    Key = "AliExpressProductId",
                    Value = $"{aliExpressProductId}"
                });
                
                await _productService.UpdateProduct(product);

                // create pictures and add to productDto
                await CreatePicturesAndAddToProduct(aliExpressProduct.Images, productDto);
                productDto = (await _mediator.Send(new GetQuery<ProductDto>() { Id = productDto.Id })).FirstOrDefault();
                // add product attributes to productDto
                productDto = await CreateProductAttributeMappingFromAliExpressVariants(aliExpressProduct, productDto);
                // add product attr combinations
                productDto = await CreateProductAttributeCombinationsFromAttributeMappings(aliExpressProduct, productDto);
                
            }

            return Ok((await _mediator.Send(new GetQuery<ProductDto>() { Id = productDto.Id })).FirstOrDefault());
        }
        private async Task<ProductDto> CreateCategoriesAndAddToProduct(AliExpressProduct aliExpressProduct, string[] categoriesId, ProductDto productDto)
        {
            // category creation
            if (categoriesId.Length == 0)
            {
                var categoriesToAdd = new List<string>();
                string parentCategoryId = null;
                // we store categories from aliExpress in our store if they don't exist already
                foreach (var aliProdCategory in aliExpressProduct.ProductCategories)
                {

                    var existingCategory = await _mediator.Send(new GetQuery<CategoryDto>());
                    if (existingCategory.Any(c => c.Name == aliProdCategory.Name))
                    {
                        if (aliProdCategory.Name == aliExpressProduct.ProductCategories.Last().Name)
                        {
                            categoriesToAdd.Add(existingCategory.FirstOrDefault(c => c.Name == aliProdCategory.Name).Id);
                        }
                    }
                    else
                    {
                        var newCategoryAddedFromAliExpress = await _mediator.Send(new AddCategoryCommand() {
                            Model = new CategoryDto() {
                                Name = aliProdCategory.Name,
                                ParentCategoryId = parentCategoryId,
                                CategoryLayoutId = KnownIds.CATEGORY_LAYOUT_GRID_OR_LINES,
                                Published = true,
                                DisplayOrder = 1,
                                ExternalId = aliProdCategory.Id.ToString(),
                                FeaturedProductsOnHomePage = false,
                                IncludeInMenu = true
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
                    Product = productDto,
                    Model = new ProductCategoryDto() {
                        CategoryId = cateId,
                        IsFeaturedProduct = false
                    }
                });
            }
            return (await _mediator.Send(new GetQuery<ProductDto>() { Id = productDto.Id })).FirstOrDefault();
        }

        private async Task<List<PictureDto>> CreatePicturesAndAddToProduct(List<string> imagesUrl, ProductDto productDto)
        {
            using var http = new HttpClient();
            var _addedPictureDtos = new List<PictureDto>();

            // create images in the Store
            var order = 1;
            foreach (var img in imagesUrl)
            {
                using var response = await http.GetAsync(img);
                response.EnsureSuccessStatusCode();

                var pictureBytes = await response.Content.ReadAsByteArrayAsync();
                try
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
                            Product = productDto,
                            Model = new Grand.Api.DTOs.Catalog.ProductPictureDto() {
                                DisplayOrder = order,
                                PictureId = pictureDto.Id
                            }
                        });
                        _addedPictureDtos.Add(pictureDto);
                        order++;
                    }

                }
                catch (Exception ex) { }
            }

            return _addedPictureDtos;
        }
        private async Task<ProductDto> CreateProductAttributeMappingFromAliExpressVariants(AliExpressProduct aliExpressProduct, ProductDto productDto)
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
                    _pictureDtos = await CreatePicturesAndAddToProduct(colorOptionImagesUrls, productDto);
                }
                // size attr mapping
                else if (option.Name.ToLowerInvariant().Contains(KnownAliExpressAttrNames.Size))
                {
                    _productAttrId = KnownIds.PRODUCT_ATTRIBUTE_ID_SIZE;
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
                    attrValueDto.DisplayOrder = _optionValueDisplayOrder;
                    attrValueDto.PriceAdjustment = optionValuePrice == null ? default : double.Parse(optionValuePrice.SalePrice.ToString());
                    attrValueDto.Quantity = optionValuePrice == null ? 1 : int.Parse( optionValuePrice.AvailableQuantity.ToString() );
                    
                    productAttributeMappingDto.ProductAttributeValues.Add(attrValueDto);

                    _optionValueDisplayOrder++;

                }

                // save mapping to database
                var attrMapping = await SaveProductAttribute(productAttributeMappingDto, productDto.Id);
                _attributeMappingDisplayOrder++;
            }
            return (await _mediator.Send(new GetQuery<ProductDto>() { Id = productDto.Id })).FirstOrDefault();
        }

        private async Task<ProductAttributeMappingDto> SaveProductAttribute(ProductAttributeMappingDto dto, string productId)
        {
            var productAttributeMapping = dto.ToEntity();
            productAttributeMapping.Combination = true;
            await _productAttributeService.InsertProductAttributeMapping(productAttributeMapping, productId);
            return productAttributeMapping.ToModel();
        }

        private async Task<ProductDto> CreateProductAttributeCombinationsFromAttributeMappings(
            AliExpressProduct aliExpressProduct,
            ProductDto productDto)
        {
            var prices = aliExpressProduct.Variants.Prices;

            foreach (var price in prices)
            {
                var adjustedPrice = productDto.Price + double.Parse(price.SalePrice.ToString());
                var value1 = string.IsNullOrEmpty(price.OptionValueId1) ? null : aliExpressProduct.GetOptionValueById(price.OptionValueId1);
                var value2 = string.IsNullOrEmpty(price.OptionValueId2) ? null : aliExpressProduct.GetOptionValueById(price.OptionValueId2);


                var customAttributes = new List<CustomAttribute>();
                string pictureId = null;
                OptionValue valueWithImage = null;
                if (value1 != null)
                {
                    valueWithImage = value1.ImagePath != null ? value1 : null;
                    var productAttributeMappingInDto1 = productDto.ProductAttributeMappings
                        .Where(p => p.ProductAttributeValues.Any(v => v.Name == value1.DisplayName)).FirstOrDefault();
                    var productAttributeValueInDto1 = productDto.ProductAttributeMappings
                        .SelectMany(p => p.ProductAttributeValues).Where(v => v.Name == value1.DisplayName).FirstOrDefault();

                    if (productAttributeMappingInDto1 != null && productAttributeValueInDto1 != null)
                    {
                        customAttributes.Add(new CustomAttribute() {
                            Key = productAttributeMappingInDto1.Id,
                            Value = productAttributeValueInDto1.Id
                        });
                    }
                }
                if (value2 != null)
                {
                    if (valueWithImage == null)
                    {
                        valueWithImage = value2.ImagePath != null ? value2 : null;
                    }

                    var productAttributeMappingInDto2 = productDto.ProductAttributeMappings
                        .Where(p => p.ProductAttributeValues.Any(v => v.Name == value2.DisplayName)).FirstOrDefault();
                    var productAttributeValueInDto2 = productDto.ProductAttributeMappings
                        .SelectMany(p => p.ProductAttributeValues).Where(v => v.Name == value2.DisplayName).FirstOrDefault();

                    pictureId = valueWithImage != null ? productDto.ProductAttributeMappings
                            .FirstOrDefault(p => p.ProductAttributeId == KnownIds.PRODUCT_ATTRIBUTE_ID_COLOR)
                            .ProductAttributeValues.FirstOrDefault(v => v.Name == valueWithImage.DisplayName).PictureId : null;

                    if (productAttributeMappingInDto2 != null && productAttributeValueInDto2 != null)
                    {
                        customAttributes.Add(new CustomAttribute() {
                            Key = productAttributeMappingInDto2.Id,
                            Value = productAttributeValueInDto2.Id
                        });
                    }

                }
                var combination = new ProductAttributeCombination() {
                    PictureId = pictureId,
                    StockQuantity = int.Parse(price.AvailableQuantity.ToString()),
                    OverriddenPrice = adjustedPrice,
                    Attributes = customAttributes
                };

                if (combination != null)
                {
                    await SaveProductCombination(combination, productDto.Id);
                }
            }
            return (await _mediator.Send(new GetQuery<ProductDto>() { Id = productDto.Id })).FirstOrDefault();
        }

        private async Task SaveProductCombination(ProductAttributeCombination entity, string productId)
        {
            await _productAttributeService.InsertProductAttributeCombination(entity, productId);
        }
        

        

        
    }
}
