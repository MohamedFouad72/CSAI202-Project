using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
#nullable disable

namespace StoreInventoryApp.Pages.Auth
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            return DoLogout();
        }

        public IActionResult OnPost()
        {
            return DoLogout();
        }

        private IActionResult DoLogout()
        {
            HttpContext.Session.Clear();

            return RedirectToPage("/Auth/Login");
        }
    }
}