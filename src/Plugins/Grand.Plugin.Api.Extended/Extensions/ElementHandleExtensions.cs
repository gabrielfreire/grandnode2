using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Extensions
{
    public static class ElementHandleExtensions
    {
        public static async Task<string> TextContentAsync(this ElementHandle element)
        {
            var _prop = await element.GetPropertyAsync("innerText");
            return await _prop.JsonValueAsync<string>();
        }
        public static async Task<string> GetAttributeAsync(this ElementHandle element, string attr)
        {
            var _prop = await element.GetPropertyAsync(attr);
            return await _prop.JsonValueAsync<string>();
        }
    }
}
