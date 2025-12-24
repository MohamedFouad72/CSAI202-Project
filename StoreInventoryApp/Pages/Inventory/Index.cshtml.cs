using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Services;
using System.Data;

namespace StoreInventoryApp.Pages.Inventory
{
    public class IndexModel : PageModel
    {
        private readonly IInventoryService _inventoryService;
        public DataTable InventoryList { get; set; } = new();

        public IndexModel(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        public async Task OnGetAsync()
        {
            InventoryList = await _inventoryService.GetInventoryLevelsAsync();
        }
    }
}