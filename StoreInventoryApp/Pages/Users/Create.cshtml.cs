using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace StoreInventoryApp.Pages.Users
{
    public class CreateModel : PageModel
    {
        private readonly IConfiguration _configuration;
        
         private readonly string? _connectionString;  // جعلها nullable

            public CreateModel(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _connectionString = _configuration.GetConnectionString("DefaultConnection");
        
        // تهيئة الخصائص
        Stores = new List<StoreDto>();
        CurrentUserRole = string.Empty;
        UserName = string.Empty;
        UserRole = string.Empty;
        UserEmail = string.Empty;
        UserPhone = string.Empty;
        SSN = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        Position = string.Empty;
        EmploymentType = string.Empty;
        Shift = string.Empty;
    }
      
        [BindProperty]
        [Required(ErrorMessage = "Username is required")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        public string UserName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "User role is required")]
        public string UserRole { get; set; } = string.Empty;

        [BindProperty]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? UserEmail { get; set; }

        [BindProperty]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string? UserPhone { get; set; }

        [BindProperty]
        public int? StoreID { get; set; }

        [BindProperty]
        public string? SSN { get; set; }

        [BindProperty]
        public string? Position { get; set; }

        [BindProperty]
        public string? EmploymentType { get; set; }

        [BindProperty]
        public string? Shift { get; set; }

        [BindProperty]
        public DateTime? HireDate { get; set; }

        [BindProperty]
        public bool CanProcessReturns { get; set; }

        public List<StoreDto> Stores { get; set; } = new List<StoreDto>();
        public string CurrentUserRole { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            CurrentUserRole = HttpContext.Session.GetString("UserRole") ?? string.Empty;

            if (!userId.HasValue)
            {
                return RedirectToPage("/Auth/Login");
            }

            if (CurrentUserRole != "Admin" && CurrentUserRole != "Manager")
            {
                TempData["Error"] = "Access denied. Only Admins and Managers can create users.";
                return RedirectToPage("./Index");
            }

            await LoadStoresAsync();
            HireDate = DateTime.Now;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserID");
            CurrentUserRole = HttpContext.Session.GetString("UserRole") ?? string.Empty;

            if (!currentUserId.HasValue)
            {
                return RedirectToPage("/Auth/Login");
            }

            await LoadStoresAsync();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors below.";
                return Page();
            }

            if (UserRole != "Admin" && !StoreID.HasValue)
            {
                ModelState.AddModelError("StoreID", "Store is required for non-Admin users");
                TempData["Error"] = "Please select a store for this user.";
                return Page();
            }

            if (CurrentUserRole == "Manager")
            {
                int? currentUserStoreId = HttpContext.Session.GetInt32("StoreID");
                if (StoreID != currentUserStoreId)
                {
                    TempData["Error"] = "Managers can only create users for their own store.";
                    return Page();
                }
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string checkQuery = "SELECT COUNT(*) FROM Users WHERE UserName = @UserName";
                            using (SqlCommand checkCmd = new SqlCommand(checkQuery, connection, transaction))
                            {
                                checkCmd.Parameters.AddWithValue("@UserName", UserName);
                                int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                                
                                if (count > 0)
                                {
                                    transaction.Rollback();
                                    TempData["Error"] = $"Username '{UserName}' already exists. Please choose a different username.";
                                    return Page();
                                }
                            }

                            string insertUserQuery = @"
                                INSERT INTO Users (UserName, Password, UserRole, UserEmail, UserPhone, StoreID, SSN, IsActive, CreatedAt)
                                OUTPUT INSERTED.UserID
                                VALUES (@UserName, @Password, @UserRole, @UserEmail, @UserPhone, @StoreID, @SSN, 1, GETDATE())";

                            int newUserId;
                            using (SqlCommand cmd = new SqlCommand(insertUserQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserName", UserName);
                                cmd.Parameters.AddWithValue("@Password", Password);
                                cmd.Parameters.AddWithValue("@UserRole", UserRole);
                                cmd.Parameters.AddWithValue("@UserEmail", string.IsNullOrEmpty(UserEmail) ? DBNull.Value : UserEmail);
                                cmd.Parameters.AddWithValue("@UserPhone", string.IsNullOrEmpty(UserPhone) ? DBNull.Value : UserPhone);
                                cmd.Parameters.AddWithValue("@StoreID", StoreID.HasValue ? StoreID.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@SSN", string.IsNullOrEmpty(SSN) ? DBNull.Value : SSN);

                                newUserId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

                            if (UserRole != "Admin" && StoreID.HasValue)
                            {
                                string insertEmployeeQuery = @"
                                    INSERT INTO Employees (UserID, StoreID, HireDate, EmploymentType, Shift, Position, CanProcessReturns, CreatedAt)
                                    VALUES (@UserID, @StoreID, @HireDate, @EmploymentType, @Shift, @Position, @CanProcessReturns, GETDATE())";

                                using (SqlCommand cmd = new SqlCommand(insertEmployeeQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@UserID", newUserId);
                                    cmd.Parameters.AddWithValue("@StoreID", StoreID.Value);
                                    cmd.Parameters.AddWithValue("@HireDate", HireDate ?? DateTime.Now);
                                    cmd.Parameters.AddWithValue("@EmploymentType", string.IsNullOrEmpty(EmploymentType) ? DBNull.Value : EmploymentType);
                                    cmd.Parameters.AddWithValue("@Shift", string.IsNullOrEmpty(Shift) ? DBNull.Value : Shift);
                                    cmd.Parameters.AddWithValue("@Position", string.IsNullOrEmpty(Position) ? DBNull.Value : Position);
                                    cmd.Parameters.AddWithValue("@CanProcessReturns", CanProcessReturns);

                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                            TempData["Success"] = $"User '{UserName}' created successfully!";
                            return RedirectToPage("./Index");
                        }
                        catch (SqlException sqlEx)
                        {
                            transaction.Rollback();
                            
                            if (sqlEx.Number == 2627)
                            {
                                TempData["Error"] = "Username already exists. Please choose a different username.";
                            }
                            else if (sqlEx.Number == 547)
                            {
                                TempData["Error"] = "Invalid store selection. Please select a valid store.";
                            }
                            else
                            {
                                TempData["Error"] = $"Database error: {sqlEx.Message}";
                            }

                            return Page();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            TempData["Error"] = $"Error creating user: {ex.Message}";
                            return Page();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Unexpected error: {ex.Message}";
                return Page();
            }
        }

        private async Task LoadStoresAsync()
        {
            Stores = new List<StoreDto>();

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT StoreID, StoreName, StoreType FROM Stores ORDER BY StoreName";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Stores.Add(new StoreDto
                            {
                                StoreID = reader.GetInt32(0),
                                StoreName = reader.GetString(1),
                                StoreType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading stores: {ex.Message}");
            }
        }
    }

    public class StoreDto
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
    }
}