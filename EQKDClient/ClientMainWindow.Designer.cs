namespace EQKDClient
{
    partial class ClientMainWindow
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.btn_ConnectToServer = new System.Windows.Forms.Button();
            this.textBox_TestFile = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btn_OpenTestFile = new System.Windows.Forms.Button();
            this.textBox_Log = new System.Windows.Forms.TextBox();
            this.textBox_ServerIP = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox_ServerPort = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.checkBox_Compress = new System.Windows.Forms.CheckBox();
            this.textBox_CountrateTest = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btn_ConnectToServer
            // 
            this.btn_ConnectToServer.Location = new System.Drawing.Point(54, 118);
            this.btn_ConnectToServer.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btn_ConnectToServer.Name = "btn_ConnectToServer";
            this.btn_ConnectToServer.Size = new System.Drawing.Size(88, 43);
            this.btn_ConnectToServer.TabIndex = 0;
            this.btn_ConnectToServer.Text = "Connect to server";
            this.btn_ConnectToServer.UseVisualStyleBackColor = true;
            this.btn_ConnectToServer.Click += new System.EventHandler(this.btn_ConnectToServer_Click);
            // 
            // textBox_TestFile
            // 
            this.textBox_TestFile.Location = new System.Drawing.Point(54, 76);
            this.textBox_TestFile.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBox_TestFile.Name = "textBox_TestFile";
            this.textBox_TestFile.Size = new System.Drawing.Size(336, 20);
            this.textBox_TestFile.TabIndex = 1;
            this.textBox_TestFile.Text = "C:\\Users\\Christian\\Dropbox\\Coding\\EQKD\\Testfiles\\RL_correct.dat";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(52, 59);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(44, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Test file";
            // 
            // btn_OpenTestFile
            // 
            this.btn_OpenTestFile.Location = new System.Drawing.Point(318, 55);
            this.btn_OpenTestFile.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btn_OpenTestFile.Name = "btn_OpenTestFile";
            this.btn_OpenTestFile.Size = new System.Drawing.Size(71, 18);
            this.btn_OpenTestFile.TabIndex = 3;
            this.btn_OpenTestFile.Text = "open";
            this.btn_OpenTestFile.UseVisualStyleBackColor = true;
            this.btn_OpenTestFile.Click += new System.EventHandler(this.btn_OpenTestFile_Click);
            // 
            // textBox_Log
            // 
            this.textBox_Log.Location = new System.Drawing.Point(54, 176);
            this.textBox_Log.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBox_Log.Multiline = true;
            this.textBox_Log.Name = "textBox_Log";
            this.textBox_Log.ReadOnly = true;
            this.textBox_Log.Size = new System.Drawing.Size(265, 86);
            this.textBox_Log.TabIndex = 5;
            // 
            // textBox_ServerIP
            // 
            this.textBox_ServerIP.Location = new System.Drawing.Point(54, 39);
            this.textBox_ServerIP.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBox_ServerIP.Name = "textBox_ServerIP";
            this.textBox_ServerIP.Size = new System.Drawing.Size(130, 20);
            this.textBox_ServerIP.TabIndex = 6;
            this.textBox_ServerIP.Text = "140.78.89.72";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(52, 23);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(51, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "Server IP";
            // 
            // textBox_ServerPort
            // 
            this.textBox_ServerPort.Location = new System.Drawing.Point(188, 39);
            this.textBox_ServerPort.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBox_ServerPort.Name = "textBox_ServerPort";
            this.textBox_ServerPort.Size = new System.Drawing.Size(50, 20);
            this.textBox_ServerPort.TabIndex = 8;
            this.textBox_ServerPort.Text = "4242";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(187, 23);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(26, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Port";
            // 
            // checkBox_Compress
            // 
            this.checkBox_Compress.AutoSize = true;
            this.checkBox_Compress.Location = new System.Drawing.Point(154, 118);
            this.checkBox_Compress.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.checkBox_Compress.Name = "checkBox_Compress";
            this.checkBox_Compress.Size = new System.Drawing.Size(99, 17);
            this.checkBox_Compress.TabIndex = 10;
            this.checkBox_Compress.Text = "GZip TimeTags";
            this.checkBox_Compress.UseVisualStyleBackColor = true;
            // 
            // textBox_CountrateTest
            // 
            this.textBox_CountrateTest.Location = new System.Drawing.Point(311, 116);
            this.textBox_CountrateTest.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBox_CountrateTest.Name = "textBox_CountrateTest";
            this.textBox_CountrateTest.Size = new System.Drawing.Size(79, 20);
            this.textBox_CountrateTest.TabIndex = 11;
            this.textBox_CountrateTest.Text = "0";
            // 
            // ClientMainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(423, 298);
            this.Controls.Add(this.textBox_CountrateTest);
            this.Controls.Add(this.checkBox_Compress);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBox_ServerPort);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBox_ServerIP);
            this.Controls.Add(this.textBox_Log);
            this.Controls.Add(this.btn_OpenTestFile);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBox_TestFile);
            this.Controls.Add(this.btn_ConnectToServer);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "ClientMainWindow";
            this.Text = "JKU EQKD - Client";
            this.Load += new System.EventHandler(this.ClientMainWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btn_ConnectToServer;
        private System.Windows.Forms.TextBox textBox_TestFile;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btn_OpenTestFile;
        private System.Windows.Forms.TextBox textBox_Log;
        private System.Windows.Forms.TextBox textBox_ServerIP;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBox_ServerPort;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox checkBox_Compress;
        private System.Windows.Forms.TextBox textBox_CountrateTest;
    }
}

