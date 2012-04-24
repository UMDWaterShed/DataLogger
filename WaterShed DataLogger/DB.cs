using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using WaterShed.DataLogger.Properties;
using System.Data;

namespace WaterShed.DataLogger
{
    class DB
    {
        public DB() { }

        static private String _activeDBConn = SetDBConn();

        static private String SetDBConn()
        {
            return Settings.Default.ConnString;
        }

        static public String GetDBConn()
        {
            return _activeDBConn;
        }

        static public MySqlConnection dbConn()
        {
            return new MySqlConnection(DB.GetDBConn());
        }

        public static String SQuote(String s)
        {
            //FINISH change to do MySql escaping instead of MsSql
            int len = s.Length + 25;
            StringBuilder tmpS = new StringBuilder(len); // hopefully only one alloc
            tmpS.Append("'");
            tmpS.Append(s.Replace("'", "''"));
            tmpS.Append("'");
            return tmpS.ToString();
        }

        static public IDataReader GetRS(String Sql, MySqlConnection dbconn)
        {
            using (MySqlCommand cmd = new MySqlCommand(Sql, dbconn))
            {
                return cmd.ExecuteReader();
            }
        }

        static public IDataReader GetRS(String Sql, MySqlParameter[] spa, MySqlConnection dbconn)
        {
            using (MySqlCommand cmd = new MySqlCommand(Sql, dbconn))
            {
                foreach (MySqlParameter sp in spa)
                {
                    cmd.Parameters.Add(sp);
                }
                return cmd.ExecuteReader();
            }
        }

        public static MySqlParameter SetValue(MySqlParameter sparam, object value)
        {
            if (value == null)
            {
                sparam.Value = DBNull.Value;
            }
            else
            {
                sparam.Value = value;
            }
            return sparam;
        }

        static public void ExecuteSQL(String Sql, MySqlConnection dbconn)
        {
            MySqlCommand cmd = new MySqlCommand(Sql, dbconn);
            try
            {
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                cmd.Dispose();
                throw (ex);
            }
        }

        public static String RSField(IDataReader rs, String fieldname)
        {
            try
            {
                int idx = rs.GetOrdinal(fieldname);
                if (rs.IsDBNull(idx))
                {
                    return String.Empty;
                }
                return rs.GetString(idx);
            }
            catch
            {
                return String.Empty;
            }
        }

        public static bool RSFieldBool(IDataReader rs, String fieldname)
        {
                try
                {
                    int idx = rs.GetOrdinal(fieldname);
                    if (rs.IsDBNull(idx))
                    {
                        return false;
                    }

                    String s = rs[fieldname].ToString();

                    return (s.Equals("TRUE", StringComparison.InvariantCultureIgnoreCase) ||
                            s.Equals("YES", StringComparison.InvariantCultureIgnoreCase) ||
                            s.Equals("1", StringComparison.InvariantCultureIgnoreCase));
                }
                catch
                {
                    return false;
                }
        }

        public static int RSFieldInt(IDataReader rs, String fieldname)
        {
                try
                {
                    int idx = rs.GetOrdinal(fieldname);
                    if (rs.IsDBNull(idx))
                    {
                        return 0;
                    }
                    return rs.GetInt32(idx);
                }
                catch
                {
                    return 0;
                }
        }

        public static int RSFieldTinyInt(IDataReader rs, String fieldname)
        {
                try
                {
                    int idx = rs.GetOrdinal(fieldname);
                    if (rs.IsDBNull(idx))
                    {
                        return 0;
                    }
                    return int.Parse(rs[idx].ToString());
                }
                catch
                {
                    return 0;
                }
        }

        public static Decimal RSFieldDecimal(IDataReader rs, String fieldname)
        {
                try
                {
                    int idx = rs.GetOrdinal(fieldname);
                    if (rs.IsDBNull(idx))
                    {
                        return System.Decimal.Zero;
                    }
                    return rs.GetDecimal(idx);
                }
                catch
                {
                    return System.Decimal.Zero;
                }
        }

        public static DateTime RSFieldDateTime(IDataReader rs, String fieldname)
        {
                try
                {
                    int idx = rs.GetOrdinal(fieldname);
                    if (rs.IsDBNull(idx))
                    {
                        return System.DateTime.MinValue;
                    }
                    //FINISH make sure this works for MySQL
                    return Convert.ToDateTime(rs[idx]);//, SqlServerCulture);

                }
                catch
                {
                    return System.DateTime.MinValue;
                }
        }

        public static DataSet GetTable(String tablename, String orderBy, MySqlConnection dbconn)
        {
            DataSet ds = new DataSet();
            String Sql = String.Empty;
            Sql = "select * from " + tablename + " order by " + orderBy;
            MySqlDataAdapter da = new MySqlDataAdapter(Sql, dbconn);
            da.Fill(ds, tablename);
            return ds;
        }

        /// <summary>
        /// Incrementally adds tables results to a dataset
        /// </summary>
        /// <param name="ds">Dataset to add the table to</param>
        /// <param name="tableName">Name of the table to be created in the DataSet</param>
        /// <param name="sqlQuery">Query to retrieve the data for the table.</param>
        static public int FillDataSet(DataSet ds, string tableName, string sqlQuery, MySqlConnection dbconn)
        {
            int n = 0;
            MySqlDataAdapter da = new MySqlDataAdapter(sqlQuery, dbconn);
            n = da.Fill(ds, tableName);
            return n;
        }

        static public int GetSqlN(String Sql, MySqlConnection dbconn)
        {
            int N = 0;

                using (IDataReader rs = DB.GetRS(Sql, dbconn))
                {
                    if (rs.Read())
                    {
                        N = DB.RSFieldInt(rs, "N");
                    }
                }

            return N;
        }

        /// <summary>
        /// Get a SQL DATETIME and parse it to a .NET DateTime
        /// </summary>
        /// <param name="Sql">A SQL statement</param>
        /// <param name="dbconn"></param>
        /// <returns></returns>
        static public DateTime GetSqlDateTime(String Sql, MySqlConnection dbconn)
        {
            DateTime dt = DateTime.MinValue;

            using (IDataReader rs = DB.GetRS(Sql, dbconn))
            {
                if (rs.Read())
                {
                    dt = DB.RSFieldDateTime(rs, "DateTime");
                }
            }

            return dt;
        }

        //*********************************************

        public MySqlDataAdapter GetDataAdapter(string Sql, MySqlConnection dbconn)
        {
            MySqlCommand cmd = new MySqlCommand(Sql, dbconn);
            MySqlDataAdapter da = new MySqlDataAdapter();
            da.SelectCommand = cmd;
            return da;
        }

        public string getMysqlDateFormat(DateTime argDate)
        {
            return argDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public string getMysqlDateFormat()
        {
            return DateTime.Now.ToString("yyy-MM-dd HH:mm:ss");
        }
    }
}
