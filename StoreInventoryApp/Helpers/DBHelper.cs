using Microsoft.Data.SqlClient;
using System.Data;

namespace StoreInventoryApp.Helpers
{
    public class DbHelper
    {
        private readonly string _connectionString;

        public DbHelper(IConfiguration config)
        {
            var connString = config.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
            }
            
            _connectionString = connString;
        }

        public DataTable ExecuteQuery(string query, SqlParameter[]? parameters = null)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            using var command = new SqlCommand(query, connection);
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            using var adapter = new SqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            return dataTable;
        }

        public object? ExecuteScalar(string query, SqlParameter[]? parameters = null)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            using var command = new SqlCommand(query, connection);
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            return command.ExecuteScalar();
        }

        public int ExecuteNonQuery(string query, SqlParameter[]? parameters = null)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            using var command = new SqlCommand(query, connection);
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            return command.ExecuteNonQuery();
        }

        // طريقة للمعاملات
        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}