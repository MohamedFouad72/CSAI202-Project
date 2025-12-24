using Microsoft.AspNetCore.Mvc;
using StoreInventoryApp.DTOs;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace StoreInventoryApp.Pages.Users
{
    public class EditModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public EditModel(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        [BindProperty]
        public int UserID { get; set; }

        [BindProperty]
        public string UserName { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "User role is required")]
        public string UserRole { get; set; } = string.Empty;

        [BindProperty]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? UserEmail { get; set; }

        [BindProperty]
        public string? UserPhone { get; set; }

        [BindProperty]
        public int? StoreID { get; set; }

        [BindProperty]
        public string? SSN { get; set; }

        [BindProperty]
        public bool IsActive { get; set; }

        [BindProperty]
        public int? EmployeeID { get; set; }

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
        public bool IsEmployee { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserID");
            CurrentUserRole = HttpContext.Session.GetString("UserRole") ?? string.Empty;

            if (!currentUserId.HasValue)
            {
                return RedirectToPage("/Auth/Login");
            }

            if (CurrentUserRole != "Admin" && CurrentUserRole != "Manager")
            {
                TempData["Error"] = "Access denied. Only Admins and Managers can edit users.";
                return RedirectToPage("./Index");
            }

            await LoadStoresAsync();
            await LoadUserDataAsync(id);

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

            if (CurrentUserRole == "Manager")
            {
                int? currentUserStoreId = HttpContext.Session.GetInt32("StoreID");
                if (StoreID != currentUserStoreId)
                {
                    TempData["Error"] = "Managers can only edit users in their own store.";
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
                            string updateUserQuery = @"
                                UPDATE Users 
                                SET UserRole = @UserRole,
                                    UserEmail = @UserEmail,
                                    UserPhone = @UserPhone,
                                    StoreID = @StoreID,
                                    SSN = @SSN,
                                    IsActive = @IsActive" +
                                    (string.IsNullOrEmpty(Password) ? "" : ", Password = @Password") +
                                @" WHERE UserID = @UserID";

                            using (SqlCommand cmd = new SqlCommand(updateUserQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserID", UserID);
                                cmd.Parameters.AddWithValue("@UserRole", UserRole);
                                cmd.Parameters.AddWithValue("@UserEmail", string.IsNullOrEmpty(UserEmail) ? DBNull.Value : UserEmail);
                                cmd.Parameters.AddWithValue("@UserPhone", string.IsNullOrEmpty(UserPhone) ? DBNull.Value : UserPhone);
                                cmd.Parameters.AddWithValue("@StoreID", StoreID.HasValue ? StoreID.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@SSN", string.IsNullOrEmpty(SSN) ? DBNull.Value : SSN);
                                cmd.Parameters.AddWithValue("@IsActive", IsActive);

                                if (!string.IsNullOrEmpty(Password))
                                {
                                    cmd.Parameters.AddWithValue("@Password", Password);
                                }

                                await cmd.ExecuteNonQueryAsync();
                            }

                            if (UserRole != "Admin" && StoreID.HasValue)
                            {
                                if (EmployeeID.HasValue)
                                {
                                    string updateEmpQuery = @"
                                        UPDATE Employees 
                                        SET StoreID = @StoreID,
                                            HireDate = @HireDate,
                                            EmploymentType = @EmploymentType,
                                            Shift = @Shift,
                                            Position = @Position,
                                            CanProcessReturns = @CanProcessReturns
                                        WHERE EmployeeID = @EmployeeID";

                                    using (SqlCommand cmd = new SqlCommand(updateEmpQuery, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@EmployeeID", EmployeeID.Value);
                                        cmd.Parameters.AddWithValue("@StoreID", StoreID.Value);
                                        cmd.Parameters.AddWithValue("@HireDate", HireDate ?? DateTime.Now);
                                        cmd.Parameters.AddWithValue("@EmploymentType", string.IsNullOrEmpty(EmploymentType) ? DBNull.Value : EmploymentType);
                                        cmd.Parameters.AddWithValue("@Shift", string.IsNullOrEmpty(Shift) ? DBNull.Value : Shift);
                                        cmd.Parameters.AddWithValue("@Position", string.IsNullOrEmpty(Position) ? DBNull.Value : Position);
                                        cmd.Parameters.AddWithValue("@CanProcessReturns", CanProcessReturns);

                                        await cmd.ExecuteNonQueryAsync();
                                    }
                                }
                                else
                                {
                                    string insertEmpQuery = @"
                                        INSERT INTO Employees (UserID, StoreID, HireDate, EmploymentType, Shift, Position, CanProcessReturns, CreatedAt)
                                        VALUES (@UserID, @StoreID, @HireDate, @EmploymentType, @Shift, @Position, @CanProcessReturns, GETDATE())";

                                    using (SqlCommand cmd = new SqlCommand(insertEmpQuery, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@UserID", UserID);
                                        cmd.Parameters.AddWithValue("@StoreID", StoreID.Value);
                                        cmd.Parameters.AddWithValue("@HireDate", HireDate ?? DateTime.Now);
                                        cmd.Parameters.AddWithValue("@EmploymentType", string.IsNullOrEmpty(EmploymentType) ? DBNull.Value : EmploymentType);
                                        cmd.Parameters.AddWithValue("@Shift", string.IsNullOrEmpty(Shift) ? DBNull.Value : Shift);
                                        cmd.Parameters.AddWithValue("@Position", string.IsNullOrEmpty(Position) ? DBNull.Value : Position);
                                        cmd.Parameters.AddWithValue("@CanProcessReturns", CanProcessReturns);

                                        await cmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            else if (UserRole == "Admin" && EmployeeID.HasValue)
                            {
                                string deleteEmpQuery = "DELETE FROM Employees WHERE EmployeeID = @EmployeeID";
                                using (SqlCommand cmd = new SqlCommand(deleteEmpQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@EmployeeID", EmployeeID.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                            TempData["Success"] = $"User '{UserName}' updated successfully!";
                            return RedirectToPage("./Index");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            TempData["Error"] = $"Error updating user: {ex.Message}";
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

        private async Task LoadUserDataAsync(int userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT u.UserID, u.UserName, u.UserRole, u.UserEmail, u.UserPhone, 
                               u.StoreID, u.SSN, u.IsActive,
                               e.EmployeeID, e.Position, e.EmploymentType, e.Shift, 
                               e.HireDate, e.CanProcessReturns
                        FROM Users u
                        LEFT JOIN Employees e ON u.UserID = e.UserID
                        WHERE u.UserID = @UserID";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                UserID = reader.GetInt32(0);
                                UserName = reader.GetString(1);
                                UserRole = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                                UserEmail = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                                UserPhone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                                StoreID = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                                SSN = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                                IsActive = reader.GetBoolean(7);

                                if (!reader.IsDBNull(8))
                                {
                                    IsEmployee = true;
                                    EmployeeID = reader.GetInt32(8);
                                    Position = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
                                    EmploymentType = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
                                    Shift = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
                                    HireDate = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12);
                                    CanProcessReturns = reader.GetBoolean(13);
                                }
                            }
                            else
                            {
                                TempData["Error"] = "User not found.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading user data: {ex.Message}";
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


}