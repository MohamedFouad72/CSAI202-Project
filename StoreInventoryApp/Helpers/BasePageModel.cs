using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StoreInventoryApp.Helpers
{
    public class BasePageModel : PageModel
    {
        protected int? GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserID");
        }

        protected string? GetCurrentUserName()
        {
            return HttpContext.Session.GetString("UserName");
        }

        protected string? GetCurrentUserRole()
        {
            return HttpContext.Session.GetString("UserRole");
        }

        protected int? GetCurrentStoreId()
        {
            return HttpContext.Session.GetInt32("StoreID");
        }

        protected bool IsAuthenticated()
        {
            return GetCurrentUserId().HasValue;
        }

        protected IActionResult? CheckAuthentication()
        {
            if (!IsAuthenticated())
            {
                return RedirectToPage("/Auth/Login");
            }
            return null;
        }

        protected bool HasRole(params string[] allowedRoles)
        {
            var userRole = GetCurrentUserRole();
            if (string.IsNullOrEmpty(userRole))
                return false;

            return allowedRoles.Any(role =>
                role.Equals(userRole, StringComparison.OrdinalIgnoreCase));
        }

        protected IActionResult? CheckAuthorization(params string[] allowedRoles)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null)
                return authCheck;

            if (!HasRole(allowedRoles))
            {
                return RedirectToPage("/Auth/AccessDenied");
            }

            return null;
        }

        protected bool IsAdmin()
        {
            return HasRole("Admin");
        }

        protected bool IsSubAdmin()
        {
            return HasRole("SubAdmin");
        }

        protected bool IsStaff()
        {
            return HasRole("Staff");
        }
    }
}
