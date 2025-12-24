#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        [BindProperty]
        public string TaxRate { get; set; }

        [BindProperty]
        public string CurrencySymbol { get; set; }

        [BindProperty]
        public string StoreName { get; set; }

        [BindProperty]
        public string ContactEmail { get; set; }

        [BindProperty]
        public string ContactPhone { get; set; }

        public string Message { get; set; }
        public string ErrorMessage { get; set; }

        public IndexModel(IConfiguration config)
        {
            _config = config;
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
                return RedirectToPage("/Auth/Login");

            // Load current settings
            LoadSettings();
            return Page();
        }

        public IActionResult OnPost()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
                return RedirectToPage("/Auth/Login");

            try
            {
                using (SqlConnection connection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update or Insert settings
                            UpdateSetting(connection, transaction, "TaxRate", TaxRate);
                            UpdateSetting(connection, transaction, "CurrencySymbol", CurrencySymbol);
                            UpdateSetting(connection, transaction, "StoreName", StoreName);
                            UpdateSetting(connection, transaction, "ContactEmail", ContactEmail);
                            UpdateSetting(connection, transaction, "ContactPhone", ContactPhone);

                            transaction.Commit();
                            Message = "Settings updated successfully!";
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                LoadSettings();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error updating settings: " + ex.Message;
                LoadSettings();
                return Page();
            }
        }

        private void LoadSettings()
        {
            string query = "SELECT SettingKey, SettingValue FROM Settings";
            DataTable settings = _db.ExecuteQuery(query);

            foreach (DataRow row in settings.Rows)
            {
                string key = row["SettingKey"].ToString();
                string value = row["SettingValue"].ToString();

                switch (key)
                {
                    case "TaxRate": TaxRate = value; break;
                    case "CurrencySymbol": CurrencySymbol = value; break;
                    case "StoreName": StoreName = value; break;
                    case "ContactEmail": ContactEmail = value; break;
                    case "ContactPhone": ContactPhone = value; break;
                }
            }

            // Set defaults if not found
            TaxRate ??= "14";
            CurrencySymbol ??= "EGP";
            StoreName ??= "My Store";
        }

        private void UpdateSetting(SqlConnection connection, SqlTransaction transaction, string key, string value)
        {
            string query = @"
                IF EXISTS (SELECT 1 FROM Settings WHERE SettingKey = @Key)
                    UPDATE Settings SET SettingValue = @Value WHERE SettingKey = @Key
                ELSE
                    INSERT INTO Settings (SettingKey, SettingValue) VALUES (@Key, @Value)";

            using (SqlCommand cmd = new SqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Key", key);
                cmd.Parameters.AddWithValue("@Value", value ?? "");
                cmd.ExecuteNonQuery();
            }
        }
    }
}