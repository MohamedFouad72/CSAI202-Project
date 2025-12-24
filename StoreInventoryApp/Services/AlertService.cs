#nullable disable
using Microsoft.Data.SqlClient;

namespace StoreInventoryApp.Services
{
    public static class AlertService
    {
        public static void GenerateLowStockAlerts(SqlConnection connection, int storeId)
        {
            string query = @"
                INSERT INTO Alerts (AlertType, Title, Message, CreatedAt, IsRead, StoreID)
                SELECT 
                    'LowStock',
                    'Low Stock Alert: ' + p.ProductName,
                    'Product ' + p.ProductName + ' has only ' + 
                    CAST(i.QuantityOnHand AS VARCHAR) + ' units left. Reorder level is ' + 
                    CAST(p.ReorderLevel AS VARCHAR) + '.',
                    GETDATE(),
                    0,
                    @StoreID
                FROM Inventory i
                JOIN Products p ON i.ProductID = p.ProductID
                WHERE i.StoreID = @StoreID 
                AND i.QuantityOnHand <= p.ReorderLevel
                AND NOT EXISTS (
                    SELECT 1 FROM Alerts a 
                    WHERE a.ReferenceID = p.ProductID 
                    AND a.AlertType = 'LowStock' 
                    AND a.IsRead = 0
                )";

            using (SqlCommand cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@StoreID", storeId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void GenerateExpiryAlerts(SqlConnection connection)
        {
            string query = @"
                INSERT INTO Alerts (AlertType, Title, Message, CreatedAt, IsRead, ReferenceID)
                SELECT 
                    'Expiry',
                    'Batch Expiring Soon: ' + p.ProductName,
                    'Batch #' + b.BatchNumber + ' of ' + p.ProductName + 
                    ' expires on ' + CONVERT(VARCHAR, b.ExpiryDate, 103) + '.',
                    GETDATE(),
                    0,
                    b.BatchID
                FROM Batches b
                JOIN Products p ON b.ProductID = p.ProductID
                WHERE b.ExpiryDate <= DATEADD(DAY, 30, GETDATE())
                AND b.ExpiryDate > GETDATE()
                AND NOT EXISTS (
                    SELECT 1 FROM Alerts a 
                    WHERE a.ReferenceID = b.BatchID 
                    AND a.AlertType = 'Expiry' 
                    AND a.IsRead = 0
                )";

            using (SqlCommand cmd = new SqlCommand(query, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static void CheckAndGenerateAllAlerts(string connectionString, int storeId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                
                // Generate Low Stock Alerts
                GenerateLowStockAlerts(connection, storeId);
                
                // Generate Expiry Alerts
                GenerateExpiryAlerts(connection);
            }
        }
    }
}