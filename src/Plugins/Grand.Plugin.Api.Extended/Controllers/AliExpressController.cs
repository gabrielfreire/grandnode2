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

namespace Grand.Plugin.Api.Extended.Controllers
{
    [SwaggerTag("Api Extended")]
    public partial class AliExpressController : BaseODataController
    {
        private readonly IMediator _mediator;
        private readonly IPermissionService _permissionService;

        public AliExpressController(
            IPermissionService permissionService, 
            IMediator mediator)
        {
            _permissionService = permissionService;
            _mediator = mediator;
        }

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

        [SwaggerOperation(summary: "Adds a product from AliExpress to the Store", OperationId = "AddAliExpressProduct")]
        [HttpPost("Products/{key}/AddAliExpressProduct")]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> Post(string key)
        {
            if (!await _permissionService.Authorize(PermissionSystemName.Orders))
                return Forbid();
            
            // get aliexpress product
            var aliExpressProduct = await AliExpressScraper.GetProductById(decimal.Parse(key));

            // convert to productDto
            var product = await _mediator.Send(new AddProductCommand() { Model = aliExpressProduct.ToProductDto() });

            if (product != null)
            {
                // create pictures and add to productDto
                var _addedPictures = await CreatePicturesAndAddToProduct(aliExpressProduct.Images, product);

                // add product attributes to productDto
                if (aliExpressProduct.Variants.Options.Any(o => o.Name == "Color"))
                {
                    var colorOption = aliExpressProduct.Variants.Options.FirstOrDefault(o => o.Name == "Color");
                    var colorOptionImagesUrls = colorOption.Values.Select(v => v.ImagePath).ToList();
                    
                    var colorOptionPictureDtos = await CreatePicturesAndAddToProduct(colorOptionImagesUrls, product);
                    var productAttributeMappingDto = new Grand.Api.DTOs.Catalog.ProductAttributeMappingDto() {
                        AttributeControlTypeId = Domain.Catalog.AttributeControlType.ImageSquares,
                        ProductAttributeId = "621026006e254b2d02acf4b1",
                        IsRequired = true,
                        DisplayOrder = 0,
                        ProductAttributeValues = new List<ProductAttributeValueDto>()
                    };
                    var displayOrder = 0;
                    foreach(var val in colorOption.Values)
                    {
                        var attrValueDto = new ProductAttributeValueDto();
                        attrValueDto.Name = val.Name;
                        attrValueDto.PictureId = colorOptionPictureDtos.FirstOrDefault(p => p.AltAttribute == val.ImagePath).Id;
                        attrValueDto.DisplayOrder = displayOrder;
                        displayOrder++;
                        attrValueDto.Quantity = 1;

                        productAttributeMappingDto.ProductAttributeValues.Add(attrValueDto);
                    }
                    await _mediator.Send(new AddProductAttributeMappingCommand() {
                        Product = product,
                        Model = productAttributeMappingDto
                    });
                }
                if (aliExpressProduct.Variants.Options.Any(o => o.Name == "Shoe Size"))
                { 
                    var productAttributeMappingDto = new Grand.Api.DTOs.Catalog.ProductAttributeMappingDto() {
                        AttributeControlTypeId = Domain.Catalog.AttributeControlType.DropdownList,
                        ProductAttributeId = "621026006e254b2d02acf4b7",
                        IsRequired = true,
                        DisplayOrder = 1,
                        ProductAttributeValues = new List<ProductAttributeValueDto>()
                    };
                    var displayOrder = 0;
                    var option = aliExpressProduct.Variants.Options
                                .FirstOrDefault(o => o.Name == "Shoe Size");
                    foreach (var val in option.Values)
                    {
                        var attrValueDto = new ProductAttributeValueDto();
                        attrValueDto.Name = val.Name;
                        attrValueDto.DisplayOrder = displayOrder;
                        displayOrder++;
                        attrValueDto.Quantity = 1;
                        productAttributeMappingDto.ProductAttributeValues.Add(attrValueDto);
                    }
                    await _mediator.Send(new AddProductAttributeMappingCommand() {
                        Product = product,
                        Model = productAttributeMappingDto
                    });
                    

                }
            }

            return Ok((await _mediator.Send(new GetQuery<ProductDto>() { Id = product.Id })).FirstOrDefault());
        }

        private async Task<List<PictureDto>> CreatePicturesAndAddToProduct(List<string> imagesUrl, ProductDto product)
        {
            using var http = new HttpClient();
            var _addedPictureDtos = new List<PictureDto>();

            // create images in the Store
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
                        _addedPictureDtos.Add(pictureDto);

                }
                catch (Exception ex) { }
            }

            // add images to product
            if (_addedPictureDtos.Count > 0)
            {
                var order = 0;
                foreach (var addedPicture in _addedPictureDtos)
                {

                    await _mediator.Send(new AddProductPictureCommand() {
                        Product = product,
                        Model = new Grand.Api.DTOs.Catalog.ProductPictureDto() {
                            DisplayOrder = order,
                            PictureId = addedPicture.Id
                        }
                    });
                    order++;
                }
            }

            return _addedPictureDtos;
        }
    }
}
