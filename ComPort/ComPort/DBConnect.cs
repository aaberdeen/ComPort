using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Data;
//Add MySql Library
using MySql.Data.MySqlClient;

namespace ComPort
{
    class DBConnect
    {
        private MySqlConnection connection;
        private string server;
        private string database;
        private string uid;
        private string password;

        

        //Constructor
        public DBConnect()
        {
            Initialize();
        }

        //Initialize values
        private void Initialize()
        {
            //server = "localhost";
            server = "10.1.0.5";
            database = "wpandb";
            uid = "root";
            password = null;
            string connectionString;
            connectionString = "SERVER=" + server + ";" + "DATABASE=" + database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

            connection = new MySqlConnection(connectionString);
        }


        //open connection to database
        private bool OpenConnection()
        {
            try
            {
                connection.Open();
                return true;
            }
            
            catch (MySqlException ex)
            {
                //When handling errors, you can your application's response based on the error number.
                //The two most common error numbers when connecting are as follows:
                //0: Cannot connect to server.
                //1045: Invalid user name and/or password.
                switch (ex.Number)
                {
                    case 0:
                        MessageBox.Show("Cannot connect to server.  Contact administrator");
                        break;

                    case 1045:
                        MessageBox.Show("Invalid username/password, please try again");
                        break;
                }
                return false;
            }
            catch(Exception e)
            {
               // MessageBox.Show("other exeption");
                return false;
            }

        }

        //Close connection
        private bool CloseConnection()
        {
            try
            {
                connection.Close();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        //Insert statement
        public void Insert()
        {
            string query = "INSERT INTO tableinfo (name, age) VALUES('John Smith', '33')";

            //open connection
            if (this.OpenConnection() == true)
            {
                //create command and assign the query and connection from the constructor
                MySqlCommand cmd = new MySqlCommand(query, connection);
                
                //Execute command
                cmd.ExecuteNonQuery();

                //close connection
                this.CloseConnection();
            }
        }

        //Update statement
        public void Update()
        {
            string query = "UPDATE tableinfo SET name='Joe', age='22' WHERE name='John Smith'";

            //Open connection
            if (this.OpenConnection() == true)
            {
                //create mysql command
                MySqlCommand cmd = new MySqlCommand();
                //Assign the query using CommandText
                cmd.CommandText = query;
                //Assign the connection using Connection
                cmd.Connection = connection;

                //Execute query
                cmd.ExecuteNonQuery();

                //close connection
                this.CloseConnection();
            }
        }

        //Delete statement
        public void Delete()
        {
            string query = "DELETE FROM tableinfo WHERE name='John Smith'";

            if (this.OpenConnection() == true)
            {
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.ExecuteNonQuery();
                this.CloseConnection();
            }
        }

        //Select statement
        public List<string>[] Select()
        {
            
            string query = "SELECT * FROM locationdb";

            //Create a list to store the result
            List<string>[] list = new List<string>[3];
            list[0] = new List<string>();
            list[1] = new List<string>();
            list[2] = new List<string>();

            //Open connection
            if (this.OpenConnection() == true)
            {
                //Create Command
                MySqlCommand cmd = new MySqlCommand(query, connection);
                //Create a data reader and Execute the command
                MySqlDataReader dataReader = cmd.ExecuteReader();
                
                //Read the data and store them in the list
                while (dataReader.Read())
                {
                    //list[0].Add(dataReader["id"] + "");
                    //list[1].Add(dataReader["name"] + "");
                    //list[2].Add(dataReader["age"] + "");
                    Debug.WriteLine("TAGid " + dataReader["TagID"] + "\tPktLqi: " + dataReader["PktLqi"]);


                }

            

                //close Data Reader
                dataReader.Close();

                //close Connection
                this.CloseConnection();

                //return list to be displayed
                return list;
            }
            else
            {
                return list;
            }
        }

        public DataTable searchDB(string sql)
        {
            DataTable table = new DataTable();

            if (this.OpenConnection() == true)
            {
                // Creates a SQL command
                using (var command = new MySqlCommand(sql, connection))
                {
                    // Loads the query results into the table
                    table.Load(command.ExecuteReader());
                    this.CloseConnection();
                    return table;
                }

            }
            else
            {
                this.CloseConnection();
                return null;
            }




        }


        public void trackingDBaseUpDate(Tag2 WorkingTag) //(string TagAdd, string ReaderAdd)
        {
                
                DataTable table = new DataTable(); // to store results
                // System.Data.SqlClient.SqlCommandBuilder trackingCB;                     //needed to add new records on clossed DB 
                // trackingCB = new System.Data.SqlClient.SqlCommandBuilder(trackingDA);           //
                

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //Remove all TOFdistance entrys for this tag
                    //------------------------------------------------------------------------------------------------------------
                    string sql = string.Format("UPDATE LocationDB SET TOFdistance = 0 WHERE TagAdd like '{0}%'", WorkingTag.TagAdd); // TagAdd);

                    //Create Command
                    MySqlCommand command = new MySqlCommand(sql, connection);
                    //Create a data reader and Execute the command
                    
                    //command.ExecuteNonQuery();  //Will change to this after testing

                    //----------------------------------------------------------------------------------------------------------------
                    //check if tag reader pair are in DB
                    //------------------------------------------------------------------------------------------------------------------
                    
                   sql = string.Format("SELECT * FROM LocationDB WHERE TagAdd like '{0}%' and ReaderAdd like '{1}%' LIMIT 1", WorkingTag.TagAdd, WorkingTag.ReaderAdd); //TagAdd,ReaderAdd);

                    //sql = string.Format("SELECT * FROM LocationDb");
                   command = new MySqlCommand(sql, connection);
                   try
                   {
                       MySqlDataReader dataReader = command.ExecuteReader();
                       // table.Load(command.ExecuteReader(), LoadOption.OverwriteChanges);
                       table.Load(dataReader, LoadOption.OverwriteChanges);


                       if (table.Rows.Count > 0)
                       {// We have one in the table so needs updating
                           // trackingCon.Open();
                           string sqlChange = string.Format("UPDATE LocationDB SET PktLqi = '{0}', TOFdistance ='{1}', TOFmac= '{2}', TimeStamp = (@value), TOF_MAC_LQI_LIFETIME = '{3}', RxLQI = '{4}', sequence = '{7}' WHERE TagAdd like '{5}%' and ReaderAdd like '{6}%'  ", WorkingTag.PktLqi, WorkingTag.TOFdistance, WorkingTag.TOFmac, 6, WorkingTag.RxLQI, WorkingTag.TagAdd, WorkingTag.ReaderAdd, WorkingTag.PktSequence);

                           // Creates a SQL command
                           command = new MySqlCommand(sqlChange, connection);
                           command.Parameters.AddWithValue("@value", DateTime.Now);
                           command.ExecuteNonQuery();


                       }
                       else
                       {// Not in the table so needs adding 
                           trackingDataBaseAddNew(WorkingTag);           //add new data to tracking DB
                       }

                       this.CloseConnection();
                   }
                   catch
                   { }


                }

             }

        private void trackingDataBaseAddNew(Tag2 WorkingTag)
        {
            
            MySqlCommand cmd = new MySqlCommand();
            cmd.CommandText = "INSERT INTO LocationDB (TagAdd, ReaderAdd, PktLqi, TOFdistance, TOFmac, TimeStamp, TOF_MAC_LQI_LIFETIME, RxLQI,sequence) VALUES(@TagAdd,@ReaderAdd,@PktLqi,@TOFdistance,@TOFmac,@TimeStamp,@TOF_MAC_LQI_LIFETIME,@RxLQI,@sequence)";
            cmd.Parameters.AddWithValue("TagAdd", WorkingTag.TagAdd);
            cmd.Parameters.AddWithValue("ReaderAdd", WorkingTag.ReaderAdd);
            cmd.Parameters.AddWithValue("PktLqi", WorkingTag.PktLqi);
            cmd.Parameters.AddWithValue("TOFdistance", WorkingTag.TOFdistance);
            cmd.Parameters.AddWithValue("TOFmac", WorkingTag.TOFmac);
            cmd.Parameters.AddWithValue("TimeStamp", DateTime.Now);
            cmd.Parameters.AddWithValue("TOF_MAC_LQI_LIFETIME", 6);
            cmd.Parameters.AddWithValue("RxLQI", WorkingTag.RxLQI);
            cmd.Parameters.AddWithValue("sequence", WorkingTag.PktSequence);
            cmd.Connection = connection;
            cmd.ExecuteNonQuery();

            
        }

        public void historyDataBaseAddNew(Tag2 WorkingTag)
        {
            //Open connection
            if (this.OpenConnection() == true)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.CommandText = @"INSERT INTO Historydb ( PktLength, ReaderAdd, TagAdd, Volt, PktSequence, PktEvent, PktLqi, TOFping, TOFtimeout, TOFrefuse, TOFsuccess, TOFdistance, RSSIdistance, TOFerror, TOFmac, PktTemp, BrSequ, BrCmd) 
                                                    VALUES(@PktLength,@ReaderAdd,@TagAdd,@Volt,@PktSequence,@PktEvent,@PktLqi,@TOFping,@TOFtimeout,@TOFrefuse,@TOFsuccess,@TOFdistance,@RSSIdistance,@TOFerror,@TOFmac,@PktTemp,@BrSequ,@BrCmd)";

                cmd.Parameters.AddWithValue("PktLength", WorkingTag.PktLength);
                cmd.Parameters.AddWithValue("ReaderAdd", WorkingTag.ReaderAdd);
                cmd.Parameters.AddWithValue("TagAdd", WorkingTag.TagAdd);
                cmd.Parameters.AddWithValue("Volt", WorkingTag.Volt);
                cmd.Parameters.AddWithValue("PktSequence", WorkingTag.PktSequence);
                cmd.Parameters.AddWithValue("PktEvent", WorkingTag.PktEvent);
                cmd.Parameters.AddWithValue("PktLqi", WorkingTag.PktLqi);
                cmd.Parameters.AddWithValue("TOFping", WorkingTag.TOFping);
                cmd.Parameters.AddWithValue("TOFtimeout", WorkingTag.TOFtimeout);
                cmd.Parameters.AddWithValue("TOFrefuse", WorkingTag.TOFrefuse);
                cmd.Parameters.AddWithValue("TOFsuccess", WorkingTag.TOFsuccess);
                cmd.Parameters.AddWithValue("TOFdistance", WorkingTag.TOFdistance);
                cmd.Parameters.AddWithValue("RSSIdistance", WorkingTag.RSSIdistance);
                cmd.Parameters.AddWithValue("TOFerror", WorkingTag.TOFerror);
                cmd.Parameters.AddWithValue("TOFmac", WorkingTag.TOFmac);
                cmd.Parameters.AddWithValue("PktTemp", WorkingTag.PktTemp);
                cmd.Parameters.AddWithValue("BrSequ", WorkingTag.BrSequ);
                cmd.Parameters.AddWithValue("BrCmd", WorkingTag.BrCmd);
                cmd.Connection = connection;
                cmd.ExecuteNonQuery();

                this.CloseConnection();

            }
   

        }

        public void LQIdecay()
        {
            if (this.OpenConnection() == true)
            {
                string sql = string.Format("UPDATE LocationDB SET PktLqi = PktLqi/(POWER(0.75,(TOF_MAC_LQI_LIFETIME - 6))), TOF_MAC_LQI_LIFETIME = (TOF_MAC_LQI_LIFETIME -1)  WHERE PktLqi > 0 and TOF_MAC_LQI_LIFETIME > 0 ");   //TagAdd, ReaderAdd, PktLqi, TimeStamp, TOF_MAC_LQI_LIFETIME

                //Create Command
                MySqlCommand command = new MySqlCommand(sql, connection);
                command.ExecuteNonQuery();

                this.CloseConnection();

            }
            this.CloseConnection();                
            
        }




        //Count statement
        public int Count()
        {
            string query = "SELECT Count(*) FROM tableinfo";
            int Count = -1;

            //Open Connection
            if (this.OpenConnection() == true)
            {
                //Create Mysql Command
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //ExecuteScalar will return one value
                Count = int.Parse(cmd.ExecuteScalar()+"");
                
                //close Connection
                this.CloseConnection();

                return Count;
            }
            else
            {
                return Count;
            }
        }

        //Backup
        public void Backup()
        {
            try
            {
                DateTime Time = DateTime.Now;
                int year = Time.Year;
                int month = Time.Month;
                int day = Time.Day;
                int hour = Time.Hour;
                int minute = Time.Minute;
                int second = Time.Second;
                int millisecond = Time.Millisecond;

                //Save file to C:\ with the current date as a filename
                string path;
                path = "C:\\" + year + "-" + month + "-" + day + "-" + hour + "-" + minute + "-" + second + "-" + millisecond + ".sql";
                StreamWriter file = new StreamWriter(path);

                
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "mysqldump";
                psi.RedirectStandardInput = false;
                psi.RedirectStandardOutput = true;
                psi.Arguments = string.Format(@"-u{0} -p{1} -h{2} {3}", uid, password, server, database);
                psi.UseShellExecute = false;

                Process process = Process.Start(psi);

                string output;
                output = process.StandardOutput.ReadToEnd();
                file.WriteLine(output);
                process.WaitForExit();
                file.Close();
                process.Close();
            }
            catch (IOException ex)
            {
                MessageBox.Show("Error , unable to backup!");
            }
        }

        //Restore
        public void Restore()
        {
            try
            {
                //Read file from C:\
                string path;
                path = "C:\\MySqlBackup.sql";
                StreamReader file = new StreamReader(path);
                string input = file.ReadToEnd();
                file.Close();


                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "mysql";
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = false;
                psi.Arguments = string.Format(@"-u{0} -p{1} -h{2} {3}", uid, password, server, database);
                psi.UseShellExecute = false;

                
                Process process = Process.Start(psi);
                process.StandardInput.WriteLine(input);
                process.StandardInput.Close();
                process.WaitForExit();
                process.Close();
            }
            catch (IOException ex)
            {
                MessageBox.Show("Error , unable to Restore!");
            }
        }


        public class Tag2
        {
            public string TagAdd { get; set; }
            public int TTL { get; set; }           // tag time to live

            public int PktLength;
            public int PktSequence;
            public int PktEvent;
            public int PktTemp;
            public int Volt;
            public int PktLqi;
            public int BrSequ;
            public int BrCmd;
            public int TOFping;
            public int TOFtimeout;
            public int TOFrefuse;
            public int TOFsuccess;
            public int TOFdistance;
            public int RSSIdistance;
            public int TOFerror;
            public string TOFmac;
            public string ReaderAdd;
            public int RxLQI;

        }

    }
}
