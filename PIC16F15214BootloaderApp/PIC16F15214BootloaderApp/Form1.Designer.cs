namespace PIC16F15214BootloaderApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.bSelectSerial = new System.Windows.Forms.Button();
            this.bDownload = new System.Windows.Forms.Button();
            this.lState = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.tbFilename = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // bSelectSerial
            // 
            this.bSelectSerial.Location = new System.Drawing.Point(39, 12);
            this.bSelectSerial.Name = "bSelectSerial";
            this.bSelectSerial.Size = new System.Drawing.Size(75, 23);
            this.bSelectSerial.TabIndex = 0;
            this.bSelectSerial.Text = "Select Serial";
            this.bSelectSerial.UseVisualStyleBackColor = true;
            this.bSelectSerial.Click += new System.EventHandler(this.button1_Click);
            // 
            // bDownload
            // 
            this.bDownload.Enabled = false;
            this.bDownload.Location = new System.Drawing.Point(39, 116);
            this.bDownload.Name = "bDownload";
            this.bDownload.Size = new System.Drawing.Size(75, 23);
            this.bDownload.TabIndex = 1;
            this.bDownload.Text = "Download";
            this.bDownload.UseVisualStyleBackColor = true;
            this.bDownload.Click += new System.EventHandler(this.bDownload_Click);
            // 
            // lState
            // 
            this.lState.AutoSize = true;
            this.lState.Location = new System.Drawing.Point(39, 155);
            this.lState.Name = "lState";
            this.lState.Size = new System.Drawing.Size(0, 15);
            this.lState.TabIndex = 2;
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(39, 196);
            this.progressBar1.Maximum = 8191;
            this.progressBar1.Minimum = 768;
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(212, 23);
            this.progressBar1.TabIndex = 3;
            this.progressBar1.Value = 768;
            // 
            // tbFilename
            // 
            this.tbFilename.Location = new System.Drawing.Point(39, 62);
            this.tbFilename.Multiline = true;
            this.tbFilename.Name = "tbFilename";
            this.tbFilename.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbFilename.Size = new System.Drawing.Size(232, 48);
            this.tbFilename.TabIndex = 4;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(301, 252);
            this.Controls.Add(this.tbFilename);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.lState);
            this.Controls.Add(this.bDownload);
            this.Controls.Add(this.bSelectSerial);
            this.Name = "Form1";
            this.Text = "Downloader";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bSelectSerial;
        private System.Windows.Forms.Button bDownload;
        private System.Windows.Forms.Label lState;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.TextBox tbFilename;
    }
}

