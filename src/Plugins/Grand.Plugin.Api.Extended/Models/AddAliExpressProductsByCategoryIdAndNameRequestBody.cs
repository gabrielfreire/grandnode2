using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Models
{
    public class AddAliExpressProductsByCategoryIdAndNameRequestBody
    {
        public string AliCategoryId { get; set; }
        public string AliCategoryName { get; set; }
        public bool PublishCategory { get; set; } = true;
        public bool PublishProducts { get; set; } = true;
        public bool IncludeInMenu { get; set; } = true;
        public bool ShowOnHomePage { get; set; } = false;
        public bool AllowCustomerToSelectPageSize { get; set; } = true;
        public int PageSize { get; set; } = 10;
        public string PageSizeOption { get; set; } = "10,15,20";
    }
}
