using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddToDbFunction.Helpers
{
    internal class SQLHelper : IDisposable
    {
        SqlConnection _connection;

        public SQLHelper(SqlConnection connection)
        {
            _connection = connection;
        }

        public SqlDataReader SelectGroupById(string id)
        {
            using (SqlCommand command = new SqlCommand($"select report_id, modified_date from PBIX_to_Flat.Visuals WHERE report_id = @reportId group by report_id, modified_date", _connection))
            {
                command.Parameters.AddWithValue("@reportId", id);
                // Execute the command
                SqlDataReader reader = command.ExecuteReader();
                return reader;
            }
        }
        public void DeleteById(string tableName, string id)
        {
            using (SqlCommand command = new SqlCommand($"DELETE FROM {tableName} WHERE report_id = @reportId", _connection))
            {
                //command.Parameters.AddWithValue("tableName", tableName);
                command.Parameters.AddWithValue("reportId", id);             
                // Execute the command
                command.ExecuteNonQuery();
            }
        }

        public void UpdateDeletedById(string tableName, string id, DateTime deleted)
        {
            using (SqlCommand command = new SqlCommand($"UPDATE {tableName} SET is_deleted = @true, deleted_time = @deleted WHERE report_id = @reportId", _connection))
            {
                command.Parameters.AddWithValue("@reportId", id);
                command.Parameters.AddWithValue("@true", "True");
                command.Parameters.AddWithValue("@deleted", deleted);
                // Execute the command
                command.ExecuteNonQuery();
            }
        }
        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
