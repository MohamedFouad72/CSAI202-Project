using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.DTOs;
using StoreInventoryApp.Services;
using System.Threading.Tasks;

namespace StoreInventoryApp.Pages.Reports
{
    public class InventoryReportModel : PageModel
    {
        private readonly IInventoryService _inventoryService;

        [BindProperty]
        public int? StoreID { get; set; }

        [BindProperty]
        public int? CategoryID { get; set; }

        [BindProperty]
        public string StockStatus { get; set; } = "All";

        public List<StoreDto> Stores { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public bool ReportGenerated { get; set; }

        public decimal TotalInventoryValue { get; set; }
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int CriticalStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        public List<InventoryItemDto> InventoryItems { get; set; } = new();
        public List<CategoryInventoryDto> CategoryBreakdown { get; set; } = new();

        public InventoryReportModel(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            await LoadFiltersAsync();

            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin")
            {
                StoreID = HttpContext.Session.GetInt32("StoreID");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            await LoadFiltersAsync();
            await GenerateReportAsync();

            ReportGenerated = true;
            return Page();
        }

        private async Task LoadFiltersAsync()
        {
            Stores = await _inventoryService.GetStoresAsync();
            Categories = await _inventoryService.GetCategoriesAsync();
        }

        private async Task GenerateReportAsync()
        {
            var result = await _inventoryService.GenerateInventoryReportAsync(StoreID, CategoryID, StockStatus);

            TotalInventoryValue = result.TotalInventoryValue;
            TotalProducts = result.TotalProducts;
            LowStockCount = result.LowStockCount;
            CriticalStockCount = result.CriticalStockCount;
            OutOfStockCount = result.OutOfStockCount;
            InventoryItems = result.InventoryItems;
            CategoryBreakdown = result.CategoryBreakdown;
        }
    }
}