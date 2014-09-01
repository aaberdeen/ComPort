using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using ComPort;



namespace ComPort_Form
{
    #region Public Enumerations
    public enum DataMode { Text, Hex }
    public enum LogMsgType { Incoming, Outgoing, Normal, Warning, Error };
    #endregion


    public partial class Form1 : Form
    {
        DBConnect dbConnect;
       
       

        int PortArrayMax = 200;
        //List<Tag> myTagList = new List<Tag>();
        List<Reader> myReaderList = new List<Reader>();
        BindingList<TagBind> allTagList = new BindingList<TagBind>();     //changed to binding list for Datagridview binding
        List<TagReader> myTagReaderList = new List<TagReader>();  //list of all tag router pairs. Need as we can't search BindingList<tag>

        
        DBConnect.Tag2 WorkingTag = new DBConnect.Tag2();

        System.Data.SqlClient.SqlConnection historyCon;
       //   System.Data.SqlClient.SqlConnection trackingCon;  //OLD FOR windows db

        // SQLiteConnection historyCon;  //TO USE LATER
        MySqlConnection trackingCon;

        DataSet historyDS;
        DataSet trackingDS;

        System.Data.SqlClient.SqlDataAdapter historyDA;
        //System.Data.SqlClient.SqlDataAdapter trackingDA; // OLD for windows db

        MySqlDataAdapter trackingDA;
        

        int historyMaxRows = 0;
        int trackingMaxRows = 0;

        bool doTracking = false;
        bool doHistory = false;

        #region Local Variables

         TreeNode treeNode = new TreeNode();

        // The main control for communicating through the RS-232 port
        private SerialPort comport = new SerialPort();

        // Various colors for logging info
        private Color[] LogMsgTypeColor = { Color.Blue, Color.Green, Color.Black, Color.Orange, Color.Red };

        // Temp holder for whether a key was pressed
       // private bool KeyHandled = false;

        byte InFrameFlag = 0;
        byte AAflag = 0;
        int PortArrayCount = 0;
        int[] PortArray = new int[200];
       // tag[] myTags;

		//private Settings settings = Settings.Default;
        #endregion


        //#region Constructor





        public Form1()
        {
            InitializeComponent();
        
        // When data is recieved through the port, call this method
        // comport.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
		 //comport.PinChanged += new SerialPinChangedEventHandler(comport_PinChanged);

        
        }


        private void comboBox1_MouseClick(object sender, MouseEventArgs e)
        {
            comboBox1.Items.Clear();
            foreach (string s in SerialPort.GetPortNames())
                comboBox1.Items.Add(s);
        }

        /// <summary> Send the user's data currently entered in the 'send' box.</summary>
        private void SendData(string data)
        {
           // if (CurrentDataMode == DataMode.Text)
           // {
                // Send the user's text straight out the port
            try
            {
                comport.Write(data);
            }
            catch (Exception)
            {
                Log(LogMsgType.Error, "Send problem: Is com port selected? "  + "\n");
            }

            txtSendData.SelectAll();
        }

        private void butStart_Click(object sender, EventArgs e)
        {
            

            cbHistoryDB.Enabled = false;
            cbTrackingDB.Enabled = false;

          //  LocationDBSetup();  // test function
            historyDataBaseSetup();
            trackingDataBaseSetup();

            bool error = false;

            // If the port is open, close it.
            if (comport.IsOpen) comport.Close();
            else
            {
                // Set the port's settings
                comport.BaudRate = 38400;
                comport.DataBits = 8;
                comport.StopBits = StopBits.One;
                comport.Parity = Parity.None;
                comport.PortName = comboBox1.Text;

                try
                {
                    // Open the port
                    comport.Open();
                }
                catch (UnauthorizedAccessException) { error = true; }
                //catch (IOException) { error = true; }
                catch (ArgumentException) { error = true; }

                if (error) MessageBox.Show(this, "Could not open the COM port.  Most likely it is already in use, has been removed, or is unavailable.", "COM Port Unavalible", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                else
                {

                }
            }

            butStart.Enabled = false;
            butStop.Enabled = true;
            //ComPortTimer.Enabled = true;
            timer1.Enabled = true;
            backgroundWorker1.RunWorkerAsync();
        }

        private void butStop_Click(object sender, EventArgs e)
        {
           // ComPortTimer.Enabled = false;

            
            backgroundWorker1.CancelAsync();
            
            cbHistoryDB.Enabled = true;
            cbTrackingDB.Enabled = true;
            timer1.Enabled = false;

            if (comport.IsOpen) comport.Close();
            
            butStart.Enabled = true;
            butStop.Enabled = false;
           

        }


        private int get_comms()
        {
            // If the com port has been closed, message box, return -1;
            if (!comport.IsOpen)
            {
                MessageBox.Show("comport not open");
                return -2;
            }
            else
            {
                try
                {
                    int read = comport.ReadByte();
                    return read;      //returns -1 if end of stream has been read
                }
                catch
                {
                    return -1;
                }
            }
            
       }






        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {        

            // If the com port has been closed, do nothing
            if (!comport.IsOpen) return;

             //backgroundWorker1.RunWorkerAsync();  //We have more data in the buffer so kick off the background worker

   
                // Obtain the number of bytes waiting in the port's buffer
                int bytes = comport.BytesToRead;
               // richTextBox1.Text += string.Format("{0}",bytes);
                Debug.WriteLine(bytes);
     
        }

        private void AAStrip()
        {
           

            for (int i = 1; i <= (PortArrayCount - 1); i++)
            {
                if (PortArray[i] == 0xaa)
                {
                    if (PortArray[i - 1] == 0xaa)
                    {
                        // shift array left form this point
                        for (int j = i; j <= PortArrayCount - 1; j++)
                        {
                            PortArray[j] = PortArray[j + 1];
                        }
                    }

                    
                }
            }


        }

        private void ExtractData(ref int PktSequence, ref string TagAdd, ref string ReaderAdd, ref string TOFmac)  //could use a class to pass this back
        {
                //First extract all the data into workingTag Object
                //------------------------------------------------------------ work in progress
                            WorkingTag.TagAdd = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[28].ToString("X2"), PortArray[27].ToString("X2"), PortArray[26].ToString("X2"), PortArray[25].ToString("X2"), PortArray[24].ToString("X2"), PortArray[23].ToString("X2"), PortArray[22].ToString("X2"), PortArray[21].ToString("X2"));
                            WorkingTag.PktLength =  (PortArray[2] << 8) + PortArray[1]; //Then use it to index allTag List
                            WorkingTag.PktSequence = PortArray[3];
                            WorkingTag.PktEvent = PortArray[11];
                            WorkingTag.PktTemp = PortArray[12];
                            WorkingTag.Volt = (PortArray[14] << 8) + PortArray[13];
                            WorkingTag.PktLqi = PortArray[37];
                            WorkingTag.BrSequ = PortArray[38];
                            WorkingTag.BrCmd = PortArray[39];
                            WorkingTag.TOFping = PortArray[40];
                            WorkingTag.TOFtimeout = (PortArray[42] << 8) + PortArray[41];
                            WorkingTag.TOFrefuse = PortArray[43];
                            WorkingTag.TOFsuccess = PortArray[44];
                            WorkingTag.TOFdistance = ((PortArray[48] << 24) + (PortArray[47] << 16) + (PortArray[46] << 8) + (PortArray[45]));//TOFdistanceEdit.Text := floattostr(TOFdistance / 100);
                            WorkingTag.RSSIdistance = ((PortArray[52] << 24) + (PortArray[51] << 16) + (PortArray[50] << 8) + (PortArray[49]));//RSSIdistanceEdit.Text := floattostr(RSSIdistance / 100);
                            WorkingTag.TOFerror = PortArray[53];
                            WorkingTag.TOFmac = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[61].ToString("X2"), PortArray[60].ToString("X2"), PortArray[59].ToString("X2"), PortArray[58].ToString("X2"), PortArray[57].ToString("X2"), PortArray[56].ToString("X2"), PortArray[55].ToString("X2"), PortArray[54].ToString("X2"));
                            WorkingTag.ReaderAdd = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[36].ToString("X2"), PortArray[35].ToString("X2"), PortArray[34].ToString("X2"), PortArray[33].ToString("X2"), PortArray[32].ToString("X2"), PortArray[31].ToString("X2"), PortArray[30].ToString("X2"), PortArray[29].ToString("X2"));
                            WorkingTag.RxLQI = PortArray[62];
                             

                //---------------------------------------------


                // List<tag> myTagList = new List<tag>();

                
                historyDataBaseAddNew();           //add new data to DB. Adds everything
   
               // string TagReaderAddTemp = WorkingTag.TagAdd + WorkingTag.ReaderAdd; // TagAddTemp + ReaderAddTemp;
                // need to make list of readers and the tags that they have


              

                if (cbTrackingDB.Checked)
                {

                    dbConnect.trackingDBaseUpDate(WorkingTag);          //Adds new tag to DB OR updates tags
                    

                }


                Reader searchResultR = myReaderList.Find(Rtest => Rtest.ReaderAdd == WorkingTag.ReaderAdd); //ReaderAddTemp);
                if (searchResultR == null)       // Reader not in list
                {
                    myReaderList.Add(new Reader
                     {

                         ReaderAdd = WorkingTag.ReaderAdd, 
                         myTagList = new List<DBConnect.Tag2>(){new DBConnect.Tag2{TagAdd=WorkingTag.TagAdd, TTL =10}},

                     });

                }
                else                // If it is in the list we need to add the Tag to it  if it is not already there. If it is we need to modify it.
                {

                    DBConnect.Tag2 searchResultT = searchResultR.FindTag(WorkingTag.TagAdd);
                    if (searchResultT == null)          // tag not in list
                    {//add

                        searchResultR.AddNewTag(ref PortArray);
                    }
                    else
                    {
                        //update TTL of tag

                        searchResultR.UpdateTag(searchResultT,ref PortArray);
                        searchResultT.TTL = 10;

                    }
                                     
                }

                // Also Create a separate list of all the tags in system
                // Search for uneque tagAdd+routerAdd 


                TagReader searchResultTR = myTagReaderList.Find(TRtest => TRtest.TagReaderAdd == WorkingTag.TagAdd + WorkingTag.ReaderAdd); 
                if (searchResultTR == null)
                {//add to tagReaderList and add to allTagList 
                    myTagReaderList.Add(new TagReader { TagReaderAdd = WorkingTag.TagAdd + WorkingTag.ReaderAdd });

                    upDateDataGridView();
        
                }
                else
                {// update values in allTagList
                    int indexTR = myTagReaderList.IndexOf(searchResultTR); // get an index to update from myTagReaderList
                   
                    dataGridView1.Invoke(new EventHandler(delegate  //need to invoke datagridview1 because allTagList is linked to it
                        {
                            
                            allTagList[indexTR].PktLength = WorkingTag.PktLength; 
                            allTagList[indexTR].PktSequence = WorkingTag.PktSequence; 
                            allTagList[indexTR].PktEvent = WorkingTag.PktEvent; 
                            allTagList[indexTR].PktTemp = WorkingTag.PktTemp; 
                            allTagList[indexTR].Volt = WorkingTag.Volt; 
                            allTagList[indexTR].PktLqi = WorkingTag.PktLqi; 
                            allTagList[indexTR].BrSequ = WorkingTag.BrSequ; 
                            allTagList[indexTR].BrCmd = WorkingTag.BrCmd; 
                            allTagList[indexTR].TOFping = WorkingTag.TOFping; 
                            allTagList[indexTR].TOFtimeout = WorkingTag.TOFtimeout; 
                            allTagList[indexTR].TOFrefuse = WorkingTag.TOFrefuse; 
                            allTagList[indexTR].TOFsuccess = WorkingTag.TOFsuccess; 
                            allTagList[indexTR].TOFdistance = WorkingTag.TOFdistance; 
                            allTagList[indexTR].RSSIdistance = WorkingTag.RSSIdistance; 
                            allTagList[indexTR].TOFerror = WorkingTag.TOFerror; 
                            allTagList[indexTR].TOFmac = WorkingTag.TOFmac; 
                            allTagList[indexTR].ReaderAdd = WorkingTag.ReaderAdd; 
                            allTagList[indexTR].RxLQI = WorkingTag.RxLQI; 
                            allTagList[indexTR].TagAdd = WorkingTag.TagAdd; 
                        }));

                }

        }

   
        public void upDateDataGridView()
        {

            if (this.dataGridView1.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => upDateDataGridView()));
            }
            else
            {
                allTagList.Add(new TagBind(ref WorkingTag));  // PortArray));
            }

        }
       
        /// <summary> Log data to the terminal window. </summary>
        /// <param name="msgtype"> The type of message to be written. </param>
        /// <param name="msg"> The string containing the message to be shown. </param>
        private void Log(LogMsgType msgtype, string msg)
        {
            rtfTerminal.Invoke(new EventHandler(delegate
            {
                rtfTerminal.SelectedText = string.Empty;
                rtfTerminal.SelectionFont = new Font(rtfTerminal.SelectionFont, FontStyle.Bold);
             //   rtfTerminal.SelectionColor = LogMsgTypeColor[(int)msgtype];
                rtfTerminal.AppendText(msg + "\n");
                rtfTerminal.ScrollToCaret();
            }));
        }

      
        private void Form1_Load(object sender, EventArgs e)
        {
           // historyDataBaseSetup();  //moved to start button

            dataGridViewSetup();

            ////
            //// This is the first node in the view.
            ////
            //TreeNode treeNode = new TreeNode("Windows");
            //treeView1.Nodes.Add(treeNode);
            ////
            //// Another node following the first node.
            ////
            //treeNode = new TreeNode("Linux");
            //treeView1.Nodes.Add(treeNode);
            ////
            //// Create two child nodes and put them in an array.
            //// ... Add the third node, and specify these as its children.
            ////
            //TreeNode node2 = new TreeNode("C#");
            //TreeNode node3 = new TreeNode("VB.NET");
            //TreeNode[] array = new TreeNode[] { node2, node3 };
            ////
            //// Final node.
            ////
            //treeNode = new TreeNode("Dot Net Perls", array);
            //treeView1.Nodes.Add(treeNode);
        
        }

        private void dataGridViewSetup()
        {
            //dataGridView1.AutoGenerateColumns = false;

            //DataGridViewTextBoxColumn Length = new DataGridViewTextBoxColumn();
            //Length.DataPropertyName = "PktLength";
            //Length.HeaderText = "Length";

            //dataGridView1.Columns.Add(Length);
            dataGridView1.DataSource = allTagList;

            dataGridView1.Columns.Remove("TTL");
            int i = 0;
            dataGridView1.Columns["PktLength"].DisplayIndex = i++;
            dataGridView1.Columns["ReaderAdd"].DisplayIndex = i++;
            dataGridView1.Columns["TagAdd"].DisplayIndex = i++;
            dataGridView1.Columns["Volt"].DisplayIndex = i++;
            dataGridView1.Columns["PktSequence"].DisplayIndex = i++;
            dataGridView1.Columns["PktEvent"].DisplayIndex = i++;
            dataGridView1.Columns["PktLqi"].DisplayIndex = i++;
            dataGridView1.Columns["TOFping"].DisplayIndex = i++;
            dataGridView1.Columns["TOFtimeout"].DisplayIndex = i++;
            dataGridView1.Columns["TOFrefuse"].DisplayIndex = i++;
            dataGridView1.Columns["TOFsuccess"].DisplayIndex = i++;
            dataGridView1.Columns["TOFdistance"].DisplayIndex = i++;
            dataGridView1.Columns["RSSIdistance"].DisplayIndex = i++;
            dataGridView1.Columns["TOFerror"].DisplayIndex = i++;
            dataGridView1.Columns["TOFmac"].DisplayIndex = i++;
            dataGridView1.Columns["RxLQI"].DisplayIndex = i++;
            dataGridView1.Columns["BrCmd"].DisplayIndex = i++;
            dataGridView1.Columns["BrSequ"].DisplayIndex = i++;

            //dataGridView1.AutoResizeColumns();
            // Configure the details DataGridView so that its columns automatically
            // adjust their widths when the data changes.
            dataGridView1.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.AllCells;
        }

        private void LocationDBSetup()  // just created to test connection to SQdatabase INFO from: http://blog.tigrangasparian.com/2012/02/09/getting-started-with-sqlite-in-c-part-one/ 
        {
            dbConnect = new DBConnect(); //initialises new db connection

             
             

            //string sql = "select * from LocationDb";
            //MySqlCommand command = new MySqlCommand(sql, m_dbConnection);

            
            dbConnect.Select();

            //while (reader.Read())
            //    Debug.WriteLine("TAGid " + reader["TagID"] + "\tPktLqi: " + reader["PktLqi"]);
                
        }

        private void historyDataBaseSetup()  // needs changing to SQlite 
        {
            if (cbHistoryDB.Checked)
            {
                //doHistory = true;

                //// database setup
                //historyCon = new System.Data.SqlClient.SqlConnection();
                //historyDS = new DataSet();

                //string sql = "SELECT * From tblWPAN";

                //historyDA = new System.Data.SqlClient.SqlDataAdapter(sql, historyCon);

                //// con.ConnectionString = "Data Source=.\\SQLEXPRESS;AttachDbFilename=C:\\Users\\andy.DDDES\\Google Drive\\C#\\ComPort\\ComPort\\ComPort\\WPANtracking.mdf;Integrated Security=True;Connect Timeout=30;User Instance=True";

                ////historyCon.ConnectionString = "Data Source=.\\SQLEXPRESS;AttachDbFilename=C:\\WPANtracking.mdf;Integrated Security=True;Connect Timeout=30;User Instance=True";
                
                //historyCon.ConnectionString = string.Format("Data Source=.\\SQLEXPRESS;AttachDbFilename={0};Integrated Security=True;Connect Timeout=30;User Instance=True",tbDbFile.Text);

                //try
                //{
                //    historyCon.Open();
                //    MessageBox.Show("history DB open");
                //}
                //catch
                //{
                //    MessageBox.Show("history DB NOT open");
                //}



                //historyDA.Fill(historyDS, "WPANtracking");
                //// NavigateRecords();

                //historyMaxRows = historyDS.Tables["WPANtracking"].Rows.Count;

                //try
                //{
                //    historyCon.Close();
                //    MessageBox.Show("history DB closed");
                //}
                //catch
                //{
                //    MessageBox.Show("history BD NOT CLOSED");
                //}

                ////end databasee setup
            }
            else
            {
                doHistory = false;
                // Dont set up data base
            }

        }

        private void historyDataBaseAddNew()
        {
            if (cbHistoryDB.Checked)
            {
                dbConnect.historyDataBaseAddNew(WorkingTag);

                //System.Data.SqlClient.SqlCommandBuilder historyCB;                     //needed to add new records on clossed DB 
                //historyCB = new System.Data.SqlClient.SqlCommandBuilder(historyDA);           //

                //DataRow dRow = historyDS.Tables["WPANtracking"].NewRow();

                //dRow[1] = WorkingTag.PktLength; 
                //dRow[5] = WorkingTag.PktSequence; 
                //dRow[6] = WorkingTag.PktEvent; 
                //dRow[16] = WorkingTag.PktTemp; 
                //dRow[4] = WorkingTag.Volt; 
                //dRow[7] = WorkingTag.PktLqi; 
                //dRow[16] = WorkingTag.BrSequ; 
                //dRow[17] = WorkingTag.BrCmd; 
                //dRow[8] = WorkingTag.TOFping; 
                //dRow[9] = WorkingTag.TOFtimeout; 
                //dRow[10] = WorkingTag.TOFrefuse; 
                //dRow[11] = WorkingTag.TOFsuccess; 
                //dRow[12] = WorkingTag.TOFdistance; 
                //dRow[13] = WorkingTag.RSSIdistance; 
                //dRow[14] = WorkingTag.TOFerror; 
                //dRow[15] = WorkingTag.TOFmac; 
                //dRow[2] = WorkingTag.ReaderAdd; 
                ////  dRow[16] = PortArray[62]; //RxLQI
                //dRow[3] = WorkingTag.TagAdd; 


                //string alldata = null;

                //for (int i = 0; i <= 61; i++)
                //{
                //   alldata = alldata + PortArray[i].ToString("x2");
                //}

                //dRow[19] = alldata;

                //historyDS.Tables["WPANtracking"].Rows.Add(dRow);                       //Updates the dataset in memory

                //// maxRows = maxRows + 1;

                //historyMaxRows = historyDS.Tables["WPANtracking"].Rows.Count;

               


                //try
                //{
                //    historyDA.Update(historyDS, "WPANtracking");                                  //Updates the database
                //}
                //catch
                //{

                //}


                ////  MessageBox.Show("Entry Added");
            }
            else
            {
               // Database shouldn't be open so don't add stuff
            }



        }


        private void trackingDataBaseSetup()
        {
            if (cbTrackingDB.Checked)
            {
                dbConnect = new DBConnect(); //initialises new db connection
            }
     
        }



        //Timeer 1 tick used to deminish LQI in tracking database
        private void timer1_Tick(object sender, EventArgs e)
        {
            //dbConnect.LQIdecay();

        }

        private void refreshTree()
        {
            if (this.treeView1.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => refreshTree()));
            }
            else
            {
             treeView1.Nodes.Clear();

            List<Reader> searchResultR = myReaderList.FindAll(Rtest => Rtest.ReaderAdd != "");
            foreach (Reader R in searchResultR)
            {
                List<DBConnect.Tag2> T = R.FindAllTags();

                TreeNode treeNode = new TreeNode(string.Format("Readers MAC = {0} \n", R.ReaderAdd));
                treeView1.Nodes.Add(treeNode);
                foreach (DBConnect.Tag2 A in T)
                {
                    TreeNode tagNode = new TreeNode(string.Format("MAC= {0}, Sequ= {1}, Event= {2}, LQI= {3}, TOFping= {4} ", A.TagAdd, A.PktSequence, A.PktEvent, A.PktLqi, A.TOFping, A.TOFtimeout, A.TOFrefuse, A.TOFsuccess,A.TOFdistance,A.TOFerror,A.TOFmac,A.RxLQI,A.BrCmd,A.BrSequ));
                    treeNode.Nodes.Add(tagNode);

                }


                treeView1.ExpandAll();
            }
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            refreshTree();
         
            //http://www.switchonthecode.com/tutorials/csharp-tutorial-binding-a-datagridview-to-a-collection
           
        }

        //Send Data Button
        private void button4_Click(object sender, EventArgs e)
        {
            SendData(txtSendData.Text);
        }

        //Unicast Button
        private void button5_Click(object sender, EventArgs e)
        {
            string sequ = "";   
            sequ += String.Format("{0:x2}", (uint)System.Convert.ToUInt32(textBox3.Text.ToString()));
            string message = "U -s " + sequ.ToUpper() + " -f 7E -t " + textBox1.Text + " -m " + textBox2.Text + '\0';
            SendData(message);

            int sequInt = Convert.ToInt16(textBox3.Text) + 1;
            if (sequInt == 255) { sequInt = 0; }
            textBox3.Text = Convert.ToString(sequInt);

        }

   
        /// BroadCast Button
        private void button6_Click(object sender, EventArgs e)
        {
            string sequ = "";   
            sequ += String.Format("{0:x2}", (uint)System.Convert.ToUInt32(textBox3.Text.ToString()));
            
          //  sequ = sequ.PadLeft(2, '0');
            string message = "B -s "+ sequ + " -f FF -m " + textBox2.Text + "\0";
           
            SendData(message);

            int sequInt = Convert.ToInt16(textBox3.Text) + 1;
            if (sequInt == 255) { sequInt = 0; }
            textBox3.Text = Convert.ToString(sequInt);


        }

        private void btnCreateDatabase_Click(object sender, EventArgs e)
        {
            String str;
            SqlConnection myConn = new SqlConnection("Server=DELPRECANDY;Integrated security=SSPI;database=master");

           // con.ConnectionString = "Data Source=.\\SQLEXPRESS;AttachDbFilename=C:\\Users\\andy.DDDES\\Google Drive\\C#\\ComPort\\ComPort\\ComPort\\WPANtracking.mdf;Integrated Security=True;Connect Timeout=30;User Instance=True";


            str = "CREATE DATABASE MyDatabase ON PRIMARY " +
                "(NAME = MyDatabase_Data, " +
                "FILENAME = 'C:\\MyDatabaseData.mdf', " +
                "SIZE = 2MB, MAXSIZE = 10MB, FILEGROWTH = 10%) " +
                "LOG ON (NAME = MyDatabase_Log, " +
                "FILENAME = 'C:\\MyDatabaseLog.ldf', " +
                "SIZE = 1MB, " +
                "MAXSIZE = 5MB, " +
                "FILEGROWTH = 10%)";

            SqlCommand myCommand = new SqlCommand(str, myConn);
            try
            {
                myConn.Open();
                myCommand.ExecuteNonQuery();
                MessageBox.Show("DataBase is Created Successfully", "MyProgram", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.ToString(), "MyProgram", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                if (myConn.State == ConnectionState.Open)
                {
                    myConn.Close();
                }
            }


        }

  
        private void BackGroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            int PktLengthInt = 0;
            int PktSequence = 0;
            string TagAdd = "";
            string ReaderAdd = "";
            string TOFmac = "";

            bool Do_Work = true;

            while (Do_Work)
            {
                int com_byte = get_comms();


                //richTextBox1.Invoke(new EventHandler(delegate
                //        {

                //            richTextBox1.Text += string.Format("{0:x}", com_byte.ToString("x2"));
                //            List<Reader> searchResultR = myReaderList.FindAll(Rtest => Rtest.ReaderAdd != "");
                           
                //        }));



    
                if (com_byte == -1)
                {
                    // reached end of buffer
                    //So we can stop the background worker for now start again on buffer RX
                    backgroundWorker1.CancelAsync();
                    Do_Work = false;
                }
                if (com_byte == -2)
                {
                    Do_Work = false;
                }
                else
                //if (com_byte != -1)
                {
                    if (InFrameFlag == 0)
                    {
                        if (com_byte == 0xaa)                      //Possible SoF
                        {
                            AAflag = 1;
                        }

                        if (AAflag == 1 & com_byte != 0xaa)          //SoF Indication
                        {
                            PortArrayCount = 1;
                            AAflag = 0;
                            InFrameFlag = 1;
                        }
                        if (AAflag == 1 & com_byte == 0xaa)
                        {
                            InFrameFlag = 0;                        //Keep hunting for SoF
                            PortArrayCount = 0;
                        }

                    }

                    //Put bytes into array

                    PortArray[PortArrayCount] = com_byte;

                    if (PortArrayCount < PortArrayMax -1)
                    {
                        PortArrayCount++;
                    }
                    else
                    { //PortArrayCount Error
                    }

                    if (PortArrayCount > 3)                                                         //Pull out packet length once we have it
                    {
                        PktLengthInt = ((PortArray[2] << 8) + PortArray[1]) + 7;

                    }


                    if (PktLengthInt > PortArrayMax - 1) //ERROR THE LENGTH IS WAY TO BIG!!!
                    {
                        InFrameFlag = 0;
                        PortArrayCount = 0;
                        PktLengthInt = 0;

                        for (int i = 0; i == PortArrayMax; i++)
                        {
                            PortArray[i] = 0;                          //Clear Down Array
                        }
                    }


                    if (PortArrayCount == PktLengthInt)                                             //Pull stuff out of array
                    {
                        // InFrameFlag = 0;

                        AAStrip();                                                                  //Remove AA Padding

                        ExtractData(ref PktSequence, ref TagAdd, ref ReaderAdd, ref TOFmac);                      //Put data into class lists


                        InFrameFlag = 0;
                        PortArrayCount = 0;

                        for (int i = 0; i == PortArrayMax; i++)
                        {
                            PortArray[i] = 0;                          //Clear Down Array
                        }
                    }

                }

            }
           // backgroundWorker1.ReportProgress(1,e);
            
        }

        private void BackGroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
           // richTextBox1.Text = e.UserState.ToString();


        }

        private void BackGroundWorker_RunWorkComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            richTextBox1.Text = string.Format("Readers online = {0} \n", myReaderList.Count);
            List<Reader> searchResultR = myReaderList.FindAll(Rtest => Rtest.ReaderAdd != "");
            foreach (Reader R in searchResultR)
            {
                richTextBox1.AppendText(string.Format("Readers MAC = {0} \n", R.ReaderAdd));
                List<DBConnect.Tag2> T = R.FindAllTags();
                // TreeNode treeNode = new TreeNode(string.Format("Readers MAC = {0} \n", R.ReaderAdd));
                foreach (DBConnect.Tag2 A in T)
                {
                    richTextBox1.AppendText(string.Format("Tag MAC = {0} TTL = {1} \n", A.TagAdd, A.TTL));
                }
            }

        }

        
       
        class TagReader
        {
            public string TagReaderAdd; 
        }

        /// <summary>
        /// Jennic Reader class used to make list of routers each with list of tags
        /// </summary>
        class Reader                       //Sub Class WAS : Jennic
        {
            public string ReaderAdd { get; set; }
            public List<DBConnect.Tag2> myTagList { get; set; }        //list of tags 

            

            /// <summary>
            /// Method to add new tag to router list
            /// </summary>
            /// <param name="PortArray"></param>

            public void AddNewTag(ref int[] PortArray)
            {
                myTagList.Add(new DBConnect.Tag2
                {
                    PktEvent = PortArray[11],
                    PktLength = (PortArray[2] << 8) + PortArray[1],
                    PktTemp = PortArray[12],
                    Volt = (PortArray[14] << 8) + PortArray[13],
                    TagAdd = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[28].ToString("X2"), PortArray[27].ToString("X2"), PortArray[26].ToString("X2"), PortArray[25].ToString("X2"), PortArray[24].ToString("X2"), PortArray[23].ToString("X2"), PortArray[22].ToString("X2"), PortArray[21].ToString("X2")),
                    ReaderAdd = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[36].ToString("X2"), PortArray[35].ToString("X2"), PortArray[34].ToString("X2"), PortArray[33].ToString("X2"), PortArray[32].ToString("X2"), PortArray[31].ToString("X2"), PortArray[30].ToString("X2"), PortArray[29].ToString("X2")),
                    PktLqi = PortArray[37],
                    BrSequ = PortArray[38],
                    BrCmd = PortArray[39],
                    TOFping = PortArray[40],
                    TOFtimeout = (PortArray[42] << 8) + PortArray[41],
                    TOFrefuse = PortArray[43],
                    TOFsuccess = PortArray[44],
                    TOFdistance = ((PortArray[48] << 24) + (PortArray[47] << 16) + (PortArray[46] << 8) + (PortArray[45])),  //TOFdistanceEdit.Text := floattostr(TOFdistance / 100);
                    RSSIdistance = ((PortArray[52] << 24) + (PortArray[51] << 16) + (PortArray[50] << 8) + (PortArray[49])),   //RSSIdistanceEdit.Text := floattostr(RSSIdistance / 100);
                    TOFerror = PortArray[53],
                    TOFmac = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[61].ToString("X2"), PortArray[60].ToString("X2"), PortArray[59].ToString("X2"), PortArray[58].ToString("X2"), PortArray[57].ToString("X2"), PortArray[56].ToString("X2"), PortArray[55].ToString("X2"), PortArray[54].ToString("X2")),
                    RxLQI = PortArray[62],
                    

                });
                
            }

   

            /// <summary>
            /// Method Remove Tag from Router List
            /// </summary>
            /// <param name="TagItem"></param>

            public void RemoveTag(DBConnect.Tag2 TagItem)
            {
                myTagList.Remove(TagItem);
            }

            /// <summary>
            /// Method update tag in Router List
            /// </summary>
            /// <param name="result"></param>
            /// <param name="PortArray"></param>

            public void UpdateTag(DBConnect.Tag2 result, ref int[] PortArray)
            {
                //Tag result = FindTag(TagAddTemp);
                
                result.PktLength = (PortArray[2] << 8) + PortArray[1];
                result.PktSequence = PortArray[3];
                
                result.PktEvent = PortArray[11];
                result.PktTemp = PortArray[12];
                result.Volt = (PortArray[14] << 8) + PortArray[13];

                result.TagAdd = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[28].ToString("X2"), PortArray[27].ToString("X2"), PortArray[26].ToString("X2"), PortArray[25].ToString("X2"), PortArray[24].ToString("X2"), PortArray[23].ToString("X2"), PortArray[22].ToString("X2"), PortArray[21].ToString("X2"));
                result.ReaderAdd = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[36].ToString("X2"), PortArray[35].ToString("X2"), PortArray[34].ToString("X2"), PortArray[33].ToString("X2"), PortArray[32].ToString("X2"), PortArray[31].ToString("X2"), PortArray[30].ToString("X2"), PortArray[29].ToString("X2"));
                result.PktLqi = PortArray[37];
                result.BrSequ = PortArray[38];
                result.BrCmd = PortArray[39];
                result.TOFping = PortArray[40];
                result.TOFtimeout = (PortArray[42] << 8) + PortArray[41];
                result.TOFrefuse = PortArray[43];
                result.TOFsuccess = PortArray[44];
                result.TOFdistance = ((PortArray[48] << 24) + (PortArray[47] << 16) + (PortArray[46] << 8) + (PortArray[45]));  //TOFdistanceEdit.Text := floattostr(TOFdistance / 100);
                result.RSSIdistance = ((PortArray[52] << 24) + (PortArray[51] << 16) + (PortArray[50] << 8) + (PortArray[49]));   //RSSIdistanceEdit.Text := floattostr(RSSIdistance / 100);
                result.TOFerror = PortArray[53];
                result.TOFmac = string.Format("{0:X}{1:X}{2:X}{3:X}{4:X}{5:X}{6:X}{7:X}", PortArray[61].ToString("X2"), PortArray[60].ToString("X2"), PortArray[59].ToString("X2"), PortArray[58].ToString("X2"), PortArray[57].ToString("X2"), PortArray[56].ToString("X2"), PortArray[55].ToString("X2"), PortArray[54].ToString("X2"));
                result.RxLQI = PortArray[62];
                result.TTL = 10;

            }

            /// <summary>
            /// Method to decrement TTL in Router List
            /// </summary>
            /// <param name="TagAddTemp"></param>

            public void DecTTL(string TagAddTemp)
            {
                DBConnect.Tag2 result = FindTag(TagAddTemp);
                 result.TTL--;
            }

            /// <summary>
            /// Method to find tag in router list
            /// </summary>
            /// <param name="TagAddTemp"></param>
            /// <returns></returns>

            public DBConnect.Tag2 FindTag(string TagAddTemp)
            {
                    DBConnect.Tag2 searchResult = myTagList.Find(Ttest => Ttest.TagAdd == TagAddTemp);
                    return searchResult;
            }

            /// <summary>
            /// Method to find All tags in router list
            /// </summary>
            /// <returns></returns>

            public List<DBConnect.Tag2> FindAllTags()
            {
                List<DBConnect.Tag2> searchResult = myTagList.FindAll(Ttest => Ttest.TagAdd != "");
                return searchResult;
            }

            /// <summary>
            /// Method to get number of tags in router list
            /// </summary>
            /// <returns></returns>

            public int NumberOfTags()
            {
                return myTagList.Count;
            }

            
        }        

        /// <summary>
        /// Jennic Tag class
        /// </summary>
        class TagBind : INotifyPropertyChanged                     
        {
           //  public string TagAdd { get; set; }
             public int TTL { get; set; }           // tag time to live

            private int _PktLength;
            private int _PktSequence;
            private int _PktEvent;
            private int _PktTemp;
            private int _Volt;
            private int _PktLqi;
            private int _BrSequ;
            private int _BrCmd;
            private int _TOFping;
            private int _TOFtimeout;
            private int _TOFrefuse;
            private int _TOFsuccess;
            private int _TOFdistance;
            private int _RSSIdistance;
            private int _TOFerror;
            private string _TOFmac;
            private string _ReaderAdd;
            private string _TagAdd;
            private int _RxLQI;

            public event PropertyChangedEventHandler PropertyChanged;

        

            public TagBind(ref DBConnect.Tag2 TagIn) 
            {
                _PktLength = TagIn.PktLength; 
                _PktSequence = TagIn.PktSequence; 
                _PktEvent = TagIn.PktEvent; 
                _PktTemp = TagIn.PktTemp; 
                _Volt = TagIn.Volt; 
                _PktLqi = TagIn.PktLqi; 
                _BrSequ = TagIn.BrSequ; 
                _BrCmd = TagIn.BrCmd;  
                _TOFping = TagIn.TOFping; 
                _TOFtimeout = TagIn.TOFtimeout; 
                _TOFrefuse = TagIn.TOFrefuse; 
                _TOFsuccess = TagIn.TOFsuccess; 
                _TOFdistance = TagIn.TOFdistance; 
                _RSSIdistance = TagIn.RSSIdistance; 
                _TOFerror = TagIn.TOFerror; 
                _TOFmac = TagIn.TOFmac; 
                _ReaderAdd = TagIn.ReaderAdd; 
                _RxLQI = TagIn.RxLQI; 
                _TagAdd = TagIn.TagAdd; 
              
                //    TTL = 10,

            }
            public int PktLength
            {
                get { return _PktLength; }
                set
                {
                    _PktLength = value;
                    this.NotifyPropertyChanged("PktLength");
                }
            }
            public int PktSequence
            {
                get { return _PktSequence; }
                set
                {
                    _PktSequence = value;
                    this.NotifyPropertyChanged("PktSequence");
                }
            }
            public int PktEvent
            {
                get { return _PktEvent; }
                set
                {
                    _PktEvent = value;
                    this.NotifyPropertyChanged("PktEvent");
                }
            }
            public int PktTemp
            {
                get { return _PktTemp; }
                set
                {
                    _PktTemp = value;
                    this.NotifyPropertyChanged("PktTemp");
                }
            }
            public int Volt
            {
                get { return _Volt; }
                set
                {
                    _Volt = value;
                    this.NotifyPropertyChanged("Volt");
                }
            }
            public int PktLqi
            {
                get { return _PktLqi; }
                set
                {
                    _PktLqi = value;
                    this.NotifyPropertyChanged("PktLqi");
                }
            }
            public int BrSequ
            {
                get { return _BrSequ; }
                set
                {
                    _BrSequ = value;
                    this.NotifyPropertyChanged("BrSequ");
                }
            }
            public int BrCmd
            {
                get { return _BrCmd; }
                set
                {
                    _BrCmd = value;
                    this.NotifyPropertyChanged("BrCmd");
                }
            }
            public int TOFping
            {
                get { return _TOFping; }
                set
                {
                    _TOFping = value;
                    this.NotifyPropertyChanged("TOFping");
                }
            }
            public int TOFtimeout
            {
                get { return _TOFtimeout; }
                set
                {
                    _TOFtimeout = value;
                    this.NotifyPropertyChanged("TOFtimeout");
                }
            }
            public int TOFrefuse
            {
                get { return _TOFrefuse; }
                set
                {
                    _TOFrefuse = value;
                    this.NotifyPropertyChanged("TOFrefuse");
                }
            }
            public int TOFsuccess
            {
                get { return _TOFsuccess; }
                set
                {
                    _TOFsuccess = value;
                    this.NotifyPropertyChanged("TOFsuccess");
                }
            }
            public int TOFdistance
            {
                get { return _TOFdistance; }
                set
                {
                    _TOFdistance = value;
                    this.NotifyPropertyChanged("TOFdistance");
                }
            }
            public int RSSIdistance
            {
                get { return _RSSIdistance; }
                set
                {
                    _RSSIdistance = value;
                    this.NotifyPropertyChanged("RSSIdistance");
                }
            }
            public int TOFerror
            {
                get { return _TOFerror; }
                set
                {
                    _TOFerror = value;
                    this.NotifyPropertyChanged("TOFerror");
                }
            }
            public string TOFmac
            {
                get { return _TOFmac; }
                set
                {
                    _TOFmac = value;
                    this.NotifyPropertyChanged("TOFmac");
                }
            }
            public string ReaderAdd
            {
                get { return _ReaderAdd; }
                set
                {
                    _ReaderAdd = value;
                    this.NotifyPropertyChanged("ReaderAdd");

                }
            }

             public string TagAdd
            {
                get { return _TagAdd; }
                set
                {
                    _TagAdd = value;
                    this.NotifyPropertyChanged("TagAdd");
                }
            }

            public int RxLQI
            {
                get { return _RxLQI; }
                set
                {
                    _RxLQI = value;
                    this.NotifyPropertyChanged("RxLQI");
                }
            }


            private void NotifyPropertyChanged(string name)
            {

                
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(name));

                
            }
       
        }

        //public class Tag2
        //{
        //    public string TagAdd { get; set; }
        //    public int TTL { get; set; }           // tag time to live

        //    public int PktLength;
        //    public int PktSequence;
        //    public int PktEvent;
        //    public int PktTemp;
        //    public int Volt;
        //    public int PktLqi;
        //    public int BrSequ;
        //    public int BrCmd;
        //    public int TOFping;
        //    public int TOFtimeout;
        //    public int TOFrefuse;
        //    public int TOFsuccess;
        //    public int TOFdistance;
        //    public int RSSIdistance;
        //    public int TOFerror;
        //    public string TOFmac;
        //    public string ReaderAdd;
        //    public int RxLQI;
            
        //}

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0x11)
            {
                object value = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                string MAC = value.ToString();
                textBox1.Text = MAC.Insert(8, ":");

            }
        }

        private void tbHistDbLoc_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
           // trackingDataBaseUpDate();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

    }
}
